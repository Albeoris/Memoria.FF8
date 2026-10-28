using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105State([property: JsonRequired] bool ColdSourcesUnmaterialized, [property: JsonRequired] ImmutableArray<NativeBattleStaticMeshResource> MutableFragments, [property: JsonRequired] int? SecondaryWorkspaceArenaOffset)
{
    public static NativeBattleThundaga105State FromColdProcess() => new(true, [], null);

    public NativeBattleThundaga105State Materialize(NativeBattleThundaga105Resources resources)
    {
        if (ColdSourcesUnmaterialized)
        {
            if (!MutableFragments.IsEmpty || resources.FragmentPairs.IsDefault || resources.FragmentPairs.Length != 14)
            {
                throw new InvalidDataException("Native Thundaga requires its fourteen original cold fragment sources.");
            }
            return this with { ColdSourcesUnmaterialized = false, MutableFragments = resources.FragmentPairs.Select(pair => pair.MutableMesh).ToImmutableArray() };
        }
        if (MutableFragments.IsDefault || MutableFragments.Length != 14 || resources.FragmentPairs.Length != 14 || MutableFragments.Where((mesh, index) => mesh.Address != resources.FragmentPairs[index].MutableMesh.Address || mesh.Bytes.IsDefault || mesh.Bytes.Length != resources.FragmentPairs[index].MutableMesh.Bytes.Length).Any())
        {
            throw new NotSupportedException("Native Thundaga requires its retained current same-address mutable fragment bank.");
        }
        return this;
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleThundaga105State? Thundaga105
    {
        get => effectState.Thundaga105;
        init => effectState = effectState with { Thundaga105 = value };
    }
}
