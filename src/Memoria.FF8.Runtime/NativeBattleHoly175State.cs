using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175State([property: JsonRequired] bool ColdSourcesUnmaterialized, [property: JsonRequired] NativeBattleHoly175WarpState Warp, [property: JsonRequired] NativeBattleEffectArena BlackPackets, [property: JsonRequired] NativeBattleEffectArena Sprite31Rows, [property: JsonRequired] NativeBattleEffectArena Sprite4Rows, [property: JsonRequired] NativeBattleEffectArena Visibility, [property: JsonRequired] NativeBattleEffectArena MatrixBanks, [property: JsonRequired] NativeBattleEffectArena StaticMatrix, [property: JsonRequired] uint? WarpTextured, [property: JsonRequired] uint? WarpAlive, [property: JsonRequired] int? SelectedActor, [property: JsonRequired] int? CasterActor)
{
    [JsonRequired]
    public short? CaptureOffsetX { get; init; }
    [JsonRequired]
    public short? CaptureOffsetY { get; init; }

    public static NativeBattleHoly175State FromColdProcess() => new(true, NativeBattleHoly175WarpState.Unknown(), Unknown(48), Unknown(100 * 24), Unknown(80 * 24), Unknown(7 * 24), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), null, null, null, null);

    public NativeBattleHoly175State Materialize(NativeBattleHoly175Resources resources)
    {
        Validate();
        if (!ColdSourcesUnmaterialized)
        {
            return this;
        }
        NativeBattleEffectHeapAllocation source = resources.CaptureBlackPackets ?? throw new NotSupportedException("Native Holy requires its original mutable black-packet seed.");
        if (source.Data.IsDefault || source.Data.Length != 48)
        {
            throw new InvalidDataException("Native Holy black packets require their two complete 24-byte records.");
        }
        return this with { ColdSourcesUnmaterialized = false, BlackPackets = BlackPackets.Write(0, source.Data.AsSpan()) };
    }

    public void Validate()
    {
        Warp.Validate();
        Bound(BlackPackets, 48);
        Bound(Sprite31Rows, 100 * 24);
        Bound(Sprite4Rows, 80 * 24);
        Bound(Visibility, 7 * 24);
        Bound(MatrixBanks, 64);
        Bound(StaticMatrix, 32);
    }

    public NativeBattleHoly175State WriteMatrix(int index, NativeBattleActionMatrix matrix)
    {
        if ((uint)index >= 2)
        {
            throw new InvalidDataException("Native Holy matrix selector leaves its two retained physical owners.");
        }
        return this with { MatrixBanks = StoreMatrix(MatrixBanks, index * 32, matrix) };
    }

    public NativeBattleActionMatrix ReadMatrix(int index)
    {
        if ((uint)index >= 2)
        {
            throw new InvalidDataException("Native Holy matrix selector leaves its two retained physical owners.");
        }
        return LoadMatrix(MatrixBanks, index * 32);
    }

    public static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena owner, int offset, NativeBattleActionMatrix matrix)
    {
        matrix.Validate();
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
        }
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), matrix.Z);
        return owner.Write(offset, rotation).Write(offset + 20, translation);
    }

    public static NativeBattleActionMatrix LoadMatrix(NativeBattleEffectArena owner, int offset)
    {
        byte[] rotation = owner.Read(offset, 18);
        byte[] translation = owner.Read(offset + 20, 12);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    private static NativeBattleEffectArena Unknown(int length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length);
    private static void Bound(NativeBattleEffectArena owner, int length)
    {
        owner.Validate();
        owner.ValidateExtent(length);
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHoly175State? Holy175 { get; init; }
}
