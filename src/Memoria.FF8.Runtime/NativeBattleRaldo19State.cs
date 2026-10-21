using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRaldo19State([property: JsonRequired] NativeBattleCuragaState? Nodes, [property: JsonRequired] ImmutableArray<byte> Shared84, [property: JsonRequired] NativeBattleEffectArena BallC4, [property: JsonRequired] NativeBattleEffectArena BodyScratch, [property: JsonRequired] NativeBattleEffectArena Descriptor48, [property: JsonRequired] NativeBattleEffectArena VertexScratch)
{
    [JsonRequired]
    public uint Counter273048C { get; init; }

    [JsonRequired]
    public uint Counter272DDDC { get; init; }

    public static NativeBattleRaldo19State FromColdProcess(NativeBattleRaldo19Resources resources)
    {
        resources.Validate();
        return new(null, resources.ColdShared84, NativeBattleEffectArena.Clear().Write(0, resources.ColdBallC4.AsSpan()), NativeBattleEffectArena.Clear().Write(0, resources.ColdBodyScratch.AsSpan()), NativeBattleEffectArena.Clear().Write(0, resources.ColdDescriptor48.AsSpan()), NativeBattleEffectArena.Clear().Write(0, resources.ColdVertexScratch.AsSpan())) { Counter273048C = resources.ColdCounter273048C, Counter272DDDC = resources.ColdCounter272DDDC };
    }

    public void Validate()
    {
        if (Shared84.IsDefault || Shared84.Length != 84)
        {
            throw new InvalidDataException("Raldo requires its complete retained director block.");
        }
        Check(BallC4, 0xC4);
        Check(BodyScratch, NativeBattleRaldo19Profile.BodyScratchLength);
        Check(Descriptor48, 48);
        Check(VertexScratch, NativeBattleRaldo19Profile.VertexScratchLength);
        if (Nodes is { } nodes)
        {
            if (nodes.SourceArenaOffset < 0 || nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattleRaldo19Profile.DrawStep || nodes.AuxiliaryCursor < 0 || nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Raldo cursors leave their original arena.");
            }
            nodes.CopiedCamera?.Validate();
        }
    }

    public short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Shared84.AsSpan().Slice(offset, 2));

    public NativeBattleRaldo19State WithWord(int offset, int value)
    {
        byte[] bytes = Shared84.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
        return this with { Shared84 = bytes.ToImmutableArray() };
    }

    private static void Check(NativeBattleEffectArena arena, int length)
    {
        if (arena is null)
        {
            throw new InvalidDataException("Raldo requires its actual retained mutable memory owners.");
        }
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Raldo retained bytes exceed their original static owner.");
        }
    }
}
