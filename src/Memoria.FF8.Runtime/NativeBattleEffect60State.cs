using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect60State([property: JsonRequired] NativeBattleCuragaState? Nodes, [property: JsonRequired] NativeBattleEffectArena Shared84, [property: JsonRequired] NativeBattleEffectArena SavedRotation8, [property: JsonRequired] NativeBattleEffectArena MorphScratch)
{
    [JsonRequired]
    public uint Counter2607050 { get; init; }
    [JsonRequired]
    public uint Counter2605DD4 { get; init; }
    [JsonIgnore]
    public int SourceArenaOffset => Nodes?.SourceArenaOffset ?? throw new NotSupportedException("Effect60 requires its executed source pointer.");
    [JsonIgnore]
    public int? PrimaryDrawCursor => Nodes?.PrimaryDrawCursor;
    [JsonIgnore]
    public int? SecondaryDrawCursor => Nodes?.SecondaryDrawCursor;

    public static NativeBattleEffect60State FromColdProcess(ImmutableArray<byte> shared84, ImmutableArray<byte> savedRotation8, ImmutableArray<byte> morphScratch, uint counter2607050, uint counter2605DD4)
    {
        if (shared84.IsDefault || shared84.Length != 84 || savedRotation8.IsDefault || savedRotation8.Length != 8 || morphScratch.IsDefault || morphScratch.Length != NativeBattleEffect60Profile.MorphScratchLength)
        {
            throw new InvalidDataException("Effect60 cold state requires its complete actual retained loader bytes.");
        }
        NativeBattleEffect60State state = new(null, NativeBattleEffectArena.Clear().Write(0, shared84.AsSpan()), NativeBattleEffectArena.Clear().Write(0, savedRotation8.AsSpan()), NativeBattleEffectArena.Clear().Write(0, morphScratch.AsSpan())) { Counter2607050 = counter2607050, Counter2605DD4 = counter2605DD4 };
        state.Validate();
        return state;
    }

    public void Validate()
    {
        Check(Shared84, 84);
        Check(SavedRotation8, 8);
        Check(MorphScratch, NativeBattleEffect60Profile.MorphScratchLength);
        if (Nodes is { } nodes && (nodes.SourceArenaOffset < 0 || nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattleEffect60Profile.DrawStep || nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.AuxiliaryCursor < 0 || nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || nodes.CopiedCamera is not null))
        {
            throw new InvalidDataException("Effect60 requires bounded cursors and its canonical shared camera owner.");
        }
    }

    public short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Shared84.Read(offset, 2));
    public NativeBattleEffect60State WithWord(int offset, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
        return this with { Shared84 = Shared84.Write(offset, bytes) };
    }

    private static void Check(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect60 retained bytes exceed their physical owner.");
        }
    }
}
