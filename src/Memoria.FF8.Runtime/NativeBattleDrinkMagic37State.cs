using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrinkMagic37State([property: JsonRequired] NativeBattleCuragaState? Nodes, [property: JsonRequired] NativeBattleEffectArena OrbMesh, [property: JsonRequired] ImmutableArray<byte> CapturedAnchor8)
{
    [JsonRequired]
    public uint Counter26A8DA8 { get; init; }

    [JsonRequired]
    public uint Counter26A7D9C { get; init; }

    public static NativeBattleDrinkMagic37State FromColdProcess(NativeBattleDrinkMagic37Resources resources)
    {
        resources.Validate();
        return new(null, NativeBattleEffectArena.Clear().Write(0, resources.OrbInitial.AsSpan()), new byte[8].ToImmutableArray());
    }

    public void Validate()
    {
        if (OrbMesh is null || CapturedAnchor8.IsDefault || CapturedAnchor8.Length != 8)
        {
            throw new InvalidDataException("Drink Magic requires its retained mutable orb and complete captured anchor.");
        }
        OrbMesh.Validate();
        if (OrbMesh.Regions.Any(region => region.Offset > NativeBattleDrinkMagic37Profile.OrbLength - region.Data.Length) || OrbMesh.UnknownRegions.Any(region => region.Offset > NativeBattleDrinkMagic37Profile.OrbLength - region.Length))
        {
            throw new InvalidDataException("Drink Magic mutable mesh leaves its original physical bank.");
        }
        if (Nodes is { } nodes)
        {
            if (nodes.SourceArenaOffset < 0 || nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattleDrinkMagic37Profile.DrawStep || nodes.AuxiliaryCursor < 0 || nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Drink Magic cursors leave their original arena.");
            }
            nodes.CopiedCamera?.Validate();
        }
    }
}
