using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePsychoBlast23State([property: JsonRequired] NativeBattleCuragaState? Nodes, [property: JsonRequired] ImmutableArray<byte> Shared84, [property: JsonRequired] NativeBattleEffectArena Basis56, [property: JsonRequired] NativeBattleEffectArena MorphScratch)
{
    [JsonRequired]
    public uint Counter27044D0 { get; init; }
    [JsonRequired]
    public uint Counter2703654 { get; init; }
    [JsonIgnore]
    public int SourceArenaOffset => Nodes?.SourceArenaOffset ?? throw new NotSupportedException("Psycho Blast requires its executed source pointer.");
    [JsonIgnore]
    public int? PrimaryDrawCursor => Nodes?.PrimaryDrawCursor;
    [JsonIgnore]
    public int? SecondaryDrawCursor => Nodes?.SecondaryDrawCursor;

    public static NativeBattlePsychoBlast23State FromColdProcess(ImmutableArray<byte> shared84, ImmutableArray<byte> basis56, ImmutableArray<byte> morphScratch, uint counter27044D0, uint counter2703654)
    {
        if (basis56.IsDefault || basis56.Length != 56 || morphScratch.IsDefault || morphScratch.Length != NativeBattlePsychoBlast23Profile.MorphScratchLength)
        {
            throw new InvalidDataException("Psycho Blast cold state requires its complete actual retained loader bytes.");
        }
        NativeBattlePsychoBlast23State state = new(null, shared84, NativeBattleEffectArena.Clear().Write(0, basis56.AsSpan()), NativeBattleEffectArena.Clear().Write(0, morphScratch.AsSpan())) { Counter27044D0 = counter27044D0, Counter2703654 = counter2703654 };
        state.Validate();
        return state;
    }

    public void Validate()
    {
        if (Shared84.IsDefault || Shared84.Length != 84)
        {
            throw new InvalidDataException("Psycho Blast requires its complete retained director block.");
        }
        Check(Basis56, 56);
        Check(MorphScratch, NativeBattlePsychoBlast23Profile.MorphScratchLength);
        if (Nodes is { } nodes && (nodes.SourceArenaOffset < 0 || nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattlePsychoBlast23Profile.DrawStep || nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.AuxiliaryCursor < 0 || nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || nodes.CopiedCamera is not null))
        {
            throw new InvalidDataException("Psycho Blast requires bounded cursors and its canonical shared camera owner.");
        }
    }

    public short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Shared84.AsSpan().Slice(offset, 2));
    public NativeBattlePsychoBlast23State WithWord(int offset, int value)
    {
        byte[] bytes = Shared84.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
        return this with { Shared84 = bytes.ToImmutableArray() };
    }

    private static void Check(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Psycho Blast retained bytes exceed their physical owner.");
        }
    }
}
