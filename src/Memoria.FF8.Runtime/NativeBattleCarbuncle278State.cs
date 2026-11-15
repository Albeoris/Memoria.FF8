using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278State([property: JsonRequired] uint Pause8114, [property: JsonRequired] uint Random811C, [property: JsonRequired] uint Action8120, [property: JsonRequired] uint? Caster8124, [property: JsonRequired] uint PacketCursor81BC, [property: JsonRequired] uint AlternateCursor81C0, [property: JsonRequired] int Centroid81C4, [property: JsonRequired] int Base81CC, [property: JsonRequired] int Lift81D0, [property: JsonRequired] uint Flash81D4, [property: JsonRequired] byte Done81D8, [property: JsonRequired] uint ModelBuffer81F8)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleVector? MirrorPlane81F0 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectLocalBounds? Bounds81E0 { get; init; }

    public static NativeBattleCarbuncle278State FromColdProcess() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public void Validate()
    {
        if (Random811C > 0x7FFF || Action8120 is not (0 or 0x1D99A78) || Caster8124 is uint address && address != 0 && !NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(address, out _) || ModelBuffer81F8 != 0 && ModelBuffer81F8 != NativeBattleEffectNodeMemory.ArenaAddress || !Cursor(PacketCursor81BC) || !Cursor(AlternateCursor81C0))
        {
            throw new InvalidDataException("Native Carbuncle module publications leave their original CRT, action, actor or effect-arena owners.");
        }

        static bool Cursor(uint address) => address == 0 || address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length;
    }
}

public sealed record NativeBattleCarbuncle278Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random)
{
    internal bool ExpandedStorage { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCarbuncle278State? Carbuncle278
    {
        get => effectState.Carbuncle278;
        init => effectState = effectState with { Carbuncle278 = value };
    }
}
