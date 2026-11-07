using System.Text;

namespace PZModManager.Core;

/// <summary>Parser mínimo do formato KeyValues (VDF/ACF) da Steam.</summary>
public sealed class VdfNode
{
    public string? Value { get; set; }
    public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

    public VdfNode? this[string key] => Children.TryGetValue(key, out var n) ? n : null;

    public string? Str(string key) => this[key]?.Value;

    public VdfNode? Path(params string[] keys)
    {
        VdfNode? cur = this;
        foreach (var k in keys)
        {
            cur = cur?[k];
            if (cur == null) return null;
        }
        return cur;
    }

    public static VdfNode? TryLoad(string file)
    {
        try { return File.Exists(file) ? Parse(File.ReadAllText(file)) : null; }
        catch { return null; }
    }

    public static VdfNode Parse(string text)
    {
        int i = 0;
        var root = new VdfNode();
        ParseInto(root, text, ref i);
        return root;
    }

    static void ParseInto(VdfNode node, string s, ref int i)
    {
        while (true)
        {
            var key = NextToken(s, ref i, out bool isBrace);
            if (key == null) return;
            if (isBrace && key == "}") return;
            if (isBrace) continue; // "{" solto: ignora
            var val = NextToken(s, ref i, out bool valBrace);
            if (val == null) return;
            var child = new VdfNode();
            if (valBrace && val == "{") ParseInto(child, s, ref i);
            else if (valBrace && val == "}") { node.Children[key] = child; return; }
            else child.Value = val;
            node.Children[key] = child;
        }
    }

    static string? NextToken(string s, ref int i, out bool isBrace)
    {
        isBrace = false;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue;
            }
            if (c == '{' || c == '}') { i++; isBrace = true; return c.ToString(); }
            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        char n = s[i + 1];
                        sb.Append(n switch { 'n' => '\n', 't' => '\t', _ => n });
                        i += 2; continue;
                    }
                    sb.Append(s[i++]);
                }
                i++;
                return sb.ToString();
            }
            // token sem aspas
            int start = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '{' && s[i] != '}' && s[i] != '"') i++;
            return s[start..i];
        }
        return null;
    }
}
