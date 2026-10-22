namespace Memoria.FF8.Assets;

/// <summary>A searchable original resource address. Geometry is read only when selected.</summary>
public sealed record ModelCatalogEntry(string Id, string Name, string Category, string ResourcePath, string SearchText, int? FieldId = null, int ModelIndex = 0)
{
    public string? LoadError { get; init; }
}

/// <summary>Supplies optional models without introducing a mod dependency into resource consumers.</summary>
public interface IModelOverrideSource
{
    FieldModel Apply(string modelId, FieldModel original);
    BattleModel Apply(string modelId, BattleModel original);
    BattleStage Apply(string modelId, BattleStage original);
    WorldStaticModel Apply(string modelId, WorldStaticModel original);
}
