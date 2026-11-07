namespace PZModManager.Core;

public sealed class CuratedPick
{
    public string Id { get; set; } = "";
    public string Group { get; set; } = "";   // core | essential | popular
    public string Note { get; set; } = "";
}

/// <summary>
/// Seleção fixa para o Project Viewpoint. O resto da aba vem ao vivo do Workshop
/// (itens que dependem do Viewpoint, ordenados por inscritos).
/// </summary>
public static class Recommendations
{
    public const string ViewpointId = "3809306528";

    public static readonly List<CuratedPick> Curated = new()
    {
        new() { Id = ZombieBuddy.WorkshopId, Group = "core", Note = "Obrigatório. Carregador Java que o Viewpoint usa." },
        new() { Id = ViewpointId, Group = "core", Note = "O mod principal: primeira e terceira pessoa com motor gráfico 3D." },
        new() { Id = "3810302175", Group = "essential", Note = "Pacote oficial de modelos 3D para móveis e objetos (indicado na página do Viewpoint)." },
        new() { Id = "3814398576", Group = "essential", Note = "Corrige superfícies (mesas, balcões) em 3D." },
        new() { Id = "3812151347", Group = "essential", Note = "Corrige portas em 3D." },
        new() { Id = "3811344119", Group = "essential", Note = "Corrige luzes e lâmpadas." },
        new() { Id = "3815033191", Group = "essential", Note = "Corrige barricadas em 3D." },
        new() { Id = "3811256204", Group = "essential", Note = "Melhorias de qualidade de vida para jogar em primeira pessoa." },
        new() { Id = "3812616893", Group = "essential", Note = "Interface adaptada à primeira pessoa." },
    };
}
