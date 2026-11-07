using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PZModManager.Core;

public sealed class RemoteItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Preview { get; set; }
    public long TimeUpdated { get; set; }
    public long Subscriptions { get; set; }
    public long FileSize { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? ShortDesc { get; set; }
    public bool Removed { get; set; }
    public long FetchedAt { get; set; }
}

public sealed class RequiredItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}

public sealed class WorkshopCache
{
    public Dictionary<string, RemoteItem> Items { get; set; } = new();
    public Dictionary<string, List<RequiredItem>> Required { get; set; } = new();
    public Dictionary<string, long> RequiredFetchedAt { get; set; } = new();
    public List<string> ViewpointAddons { get; set; } = new();
    public long ViewpointAddonsFetchedAt { get; set; }
}

public sealed class SteamWorkshop
{
    static readonly HttpClient Http = CreateClient();
    readonly string _cacheFile = Path.Combine(GamePaths.DataDir, "cache-workshop.json");
    readonly object _lock = new();
    WorkshopCache _cache;
    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public SteamWorkshop()
    {
        try
        {
            _cache = File.Exists(_cacheFile)
                ? JsonSerializer.Deserialize<WorkshopCache>(File.ReadAllText(_cacheFile)) ?? new()
                : new();
        }
        catch { _cache = new(); }
    }

    static HttpClient CreateClient()
    {
        var c = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        { Timeout = TimeSpan.FromSeconds(25) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) PZModManager/1.0");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.8");
        return c;
    }

    static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void Save()
    {
        lock (_lock)
        {
            try { File.WriteAllText(_cacheFile, JsonSerializer.Serialize(_cache, JsonOpts)); } catch { }
        }
    }

    public Dictionary<string, RemoteItem> CachedItems()
    {
        lock (_lock) return new(_cache.Items);
    }

    public Dictionary<string, List<RequiredItem>> CachedRequired()
    {
        lock (_lock) return new(_cache.Required);
    }

    /// <summary>Busca detalhes (título, imagem, data de atualização) pela API pública da Steam.</summary>
    public async Task FetchDetails(IEnumerable<string> ids, bool force, CancellationToken ct = default)
    {
        List<string> todo;
        lock (_lock)
            todo = ids.Distinct().Where(id => force || !_cache.Items.TryGetValue(id, out var it) || Now - it.FetchedAt > 1800).ToList();
        foreach (var chunk in todo.Chunk(100))
        {
            var form = new List<KeyValuePair<string, string>> { new("itemcount", chunk.Length.ToString()) };
            for (int i = 0; i < chunk.Length; i++) form.Add(new($"publishedfileids[{i}]", chunk[i]));
            using var resp = await Http.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                new FormUrlEncodedContent(form), ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("response", out var r) ||
                !r.TryGetProperty("publishedfiledetails", out var arr)) continue;
            lock (_lock)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var id = e.GetProperty("publishedfileid").GetString() ?? "";
                    var ok = e.TryGetProperty("result", out var res) && res.GetInt32() == 1;
                    var item = new RemoteItem { Id = id, FetchedAt = Now, Removed = !ok };
                    if (ok)
                    {
                        item.Title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        item.Preview = e.TryGetProperty("preview_url", out var pv) ? pv.GetString() : null;
                        item.TimeUpdated = e.TryGetProperty("time_updated", out var tu) ? tu.GetInt64() : 0;
                        item.Subscriptions = e.TryGetProperty("subscriptions", out var su) ? su.GetInt64() : 0;
                        if (e.TryGetProperty("file_size", out var fs))
                            item.FileSize = fs.ValueKind == JsonValueKind.String ? long.Parse(fs.GetString() ?? "0") : fs.GetInt64();
                        if (e.TryGetProperty("tags", out var tags))
                            item.Tags = tags.EnumerateArray().Select(x => x.GetProperty("tag").GetString() ?? "").ToList();
                        if (e.TryGetProperty("description", out var d)) item.ShortDesc = CleanBbcode(d.GetString() ?? "");
                    }
                    else if (_cache.Items.TryGetValue(id, out var old)) { old.FetchedAt = Now; old.Removed = true; continue; }
                    _cache.Items[id] = item;
                }
            }
        }
        if (todo.Count > 0) Save();
    }

    static string CleanBbcode(string s)
    {
        s = Regex.Replace(s, @"\[img\].*?\[/img\]", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        s = Regex.Replace(s, @"\[url=[^\]]*\](.*?)\[/url\]", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        s = Regex.Replace(s, @"\[/?[a-z0-9*]+(=[^\]]*)?\]", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 320 ? s[..320] + "…" : s;
    }

    /// <summary>Lê a seção "Required items" da página do item (a API pública não traz isso sem chave).</summary>
    public async Task FetchRequired(IEnumerable<string> ids, bool force, CancellationToken ct = default)
    {
        List<string> todo;
        lock (_lock)
            todo = ids.Distinct().Where(id => force || !_cache.RequiredFetchedAt.TryGetValue(id, out var t) || Now - t > 6 * 3600).ToList();
        using var sem = new SemaphoreSlim(6);
        var tasks = todo.Select(async id =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var html = await Http.GetStringAsync($"https://steamcommunity.com/sharedfiles/filedetails/?id={id}", ct);
                var list = ParseRequired(html);
                lock (_lock) { _cache.Required[id] = list; _cache.RequiredFetchedAt[id] = Now; }
            }
            catch { }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);
        if (todo.Count > 0) Save();
    }

    static List<RequiredItem> ParseRequired(string html)
    {
        var res = new List<RequiredItem>();
        int i = html.IndexOf("id=\"RequiredItems\"", StringComparison.Ordinal);
        if (i < 0) return res;
        int end = html.IndexOf("rightSectionTopTitle", i, StringComparison.Ordinal);
        if (end < 0) end = Math.Min(html.Length, i + 20000);
        var block = html[i..end];
        foreach (Match m in Regex.Matches(block,
                     @"filedetails/\?id=(\d+)"".*?class=""requiredItem"">\s*(.*?)\s*</div>", RegexOptions.Singleline))
        {
            var title = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<.*?>", "")).Trim();
            if (res.All(r => r.Id != m.Groups[1].Value)) res.Add(new RequiredItem { Id = m.Groups[1].Value, Title = title });
        }
        return res;
    }

    /// <summary>Addons que dependem do Project Viewpoint, ordenados por inscritos.</summary>
    public async Task<List<string>> FetchViewpointAddons(bool force, CancellationToken ct = default)
    {
        lock (_lock)
            if (!force && _cache.ViewpointAddons.Count > 0 && Now - _cache.ViewpointAddonsFetchedAt < 6 * 3600)
                return new(_cache.ViewpointAddons);
        var ids = new List<string>();
        foreach (var page in new[] { 1, 2, 3 })
        {
            try
            {
                var url = "https://steamcommunity.com/workshop/browse/?appid=108600&section=readytouseitems" +
                          $"&childpublishedfileid={Recommendations.ViewpointId}&browsesort=totaluniquesubscribers&numperpage=30&p={page}";
                var html = await Http.GetStringAsync(url, ct);
                foreach (Match m in Regex.Matches(html, @"filedetails/\?id=(\d+)"))
                    if (!ids.Contains(m.Groups[1].Value)) ids.Add(m.Groups[1].Value);
            }
            catch { break; }
        }
        if (ids.Count == 0) lock (_lock) return new(_cache.ViewpointAddons);
        lock (_lock) { _cache.ViewpointAddons = ids; _cache.ViewpointAddonsFetchedAt = Now; }
        Save();
        return ids;
    }
}
