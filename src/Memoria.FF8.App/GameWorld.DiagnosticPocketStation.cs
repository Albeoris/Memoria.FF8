using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticPocketStationPreparation([property: JsonRequired] bool Available, [property: JsonRequired] bool Away, [property: JsonRequired] byte Level, [property: JsonRequired] byte AttackVariant)
{
    internal void Validate()
    {
        if (Level is < 1 or > 100 || AttackVariant > 3)
        {
            throw new InvalidDataException("Diagnostic PocketStation preparation requires an original level1..100 and attack variant0..3.");
        }
    }
}

internal sealed record DiagnosticPocketStationPreparationResult(NativePocketStationState State, ImmutableArray<DiagnosticPreparationChange> Changes);

internal sealed partial class GameWorld
{
    internal static DiagnosticPocketStationPreparationResult PrepareDiagnosticPocketStation(NativePocketStationState source, DiagnosticPocketStationPreparation recipe)
    {
        source.Validate();
        recipe.Validate();
        byte[] raw = source.Raw.ToArray();
        raw[0] = (byte)((raw[0] & ~3) | (recipe.Available ? 1 : 0) | (recipe.Away ? 2 : 0));
        raw[1] = recipe.Level;
        raw[45] = recipe.AttackVariant;
        NativePocketStationState prepared = source with { Raw = raw.ToImmutableArray() };
        prepared.Validate();
        ImmutableArray<DiagnosticPreparationChange> changes = source.Raw.SequenceEqual(prepared.Raw) ? [] : [new("pocket-station", 0, JsonSerializer.SerializeToElement(source), JsonSerializer.SerializeToElement(prepared))];
        return new(prepared, changes);
    }
}
