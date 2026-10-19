using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltrasonic152Resources(ImmutableArray<byte> Ring, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<uint> Colors, NativeBattleEffectHeapAllocation Camera, uint Sound);

public sealed record NativeBattleUltrasonic152State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] NativeBattleVector Anchor, [property: JsonRequired] NativeBattleVector Target, [property: JsonRequired] NativeBattleActionMatrix Rotation, [property: JsonRequired] int BufferOffset)
{
    public static NativeBattleUltrasonic152State FromColdProcess() => new(new byte[100 * 24].ToImmutableArray(), default, default, new(new short[9].ToImmutableArray(), 0, 0, 0, 0), 0);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltrasonic152State? Ultrasonic152 { get; init; }
}

public static class NativeBattleUltrasonic152
{
    public static NativeBattleGfGeometryBatch Ring60BA00(ImmutableArray<byte> source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint color, int frame, NativeBattleSummonColors colors)
    {
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan());
        if (BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor)) != 0 || BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor + 4)) != 0 || BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor + 8)) != 0)
        {
            throw new InvalidDataException("Native Ultrasonic Waves requires its original flat-quad ring lists.");
        }
        cursor += 12;
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
        cursor += 4;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> face = source.AsSpan().Slice(cursor + index * 24, 24);
            NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[4];
            for (int corner = 0; corner < 4; corner++)
            {
                int vertexOffset = 8 + BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2)) * 4;
                ReadOnlySpan<byte> vertex = source.AsSpan().Slice(vertexOffset, 6);
                vertices[corner] = NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(4))), projection);
            }
            NativeBattleMeshPacketContext context = new(frame < 16 ? 0x33u : 0xF3u, 0, (frame - 16) << 10, null, null, 0, 0, 17, 2);
            NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, vertices, 3, context, colors, allocated, null, new(color, null, false, true));
            attempts.Add(attempt);
            if (attempt.Primitive is not null)
            {
                allocated += 40;
            }
        }
        int tail = cursor + count * 24;
        for (int group = 0; group < 4; group++)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(tail + group * 4)) != 0)
            {
                throw new InvalidDataException("Native Ultrasonic Waves ring has an unexpected trailing polygon list.");
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        long offset = (long)address - 0x244BE78 - 14;
        if (pool.DescriptorAddress != 0x244BE98 || pool.BankAddress != 0x244BE78 || pool.Stride != 16 || offset < 0 || offset % 16 != 0 || offset / 16 >= pool.Slots.Length)
        {
            throw new InvalidDataException("Native Ultrasonic Waves completion leaves its physical root bank.");
        }
        int slot = (int)(offset / 16);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if ((task.Flags & 1) == 0 || task.Callback != 0x60C960)
        {
            throw new InvalidDataException("Native Ultrasonic Waves completion lost its live restore wait task.");
        }
        task = task.WriteWord(14, (ushort)((task.ReadWord(14) & 0xFF00) | value));
        return effect with { Pools = effect.Pools.SetItem(0, pool with { Slots = pool.Slots.SetItem(slot, task) }) };
    }
}
