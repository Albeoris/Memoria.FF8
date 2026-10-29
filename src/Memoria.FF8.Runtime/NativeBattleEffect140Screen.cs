using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect140Fields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect140Screen
{
    public static NativeBattleEffect140Frame Fade(NativeBattleEffect140Frame frame, int red, int green, int blue, int current, int maximum, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int remaining = unchecked(maximum - current);
        if (remaining <= 0)
        {
            return frame;
        }
        uint color = 0x2A000000u | (byte)(unchecked(red * remaining) / maximum) | (uint)(byte)(unchecked(green * remaining) / maximum) << 8 | (uint)(byte)(unchecked(blue * remaining) / maximum) << 16;
        NativeBattleGfGeometryAttempt attempt = Packet(0, [0x05000000, color, 0, 320, 216u << 16, 320u | 216u << 16], 8, [0, 0, 0, 0]);
        if (pilotIdentity is { } identity && attempt.Primitive is { } primitive)
        {
            attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity, new(0, 0, 0), new(320, 0, 0), new(0, 216, 0), new(320, 216, 0), 4, false) { ScreenSpace = true, ScreenScale = 8, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } } };
        }
        return inputs.Execute(frame, new NativeBattleEffect140DrawBatch(new([attempt], 24), Cursor, 1));
    }

    public static NativeBattleEffect140Frame Scroll(NativeBattleEffect140Frame frame, int amplitude, int angle, int bucket, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        byte side = frame.Memory.PrimitiveProcess?.Buffer80 ?? throw new NotSupportedException("Effect140 scan copies require the current display-buffer selector.");
        int baseX = side == 0 ? 320 : 0;
        int accumulator = 0;
        for (int y = 8; y < 220; y++)
        {
            int displacement = unchecked(NativeBattleCameraMath.Sine(angle) * accumulator) >> 20;
            angle = unchecked(angle + 128);
            uint cursor = frame.State.ReadDword(Cursor);
            frame = frame with { State = frame.State.WriteDword(Cursor, unchecked(cursor + 24)) };
            if (displacement != 0)
            {
                int sourceX = displacement < 0 ? baseX - displacement : 320;
                int sourceY = displacement < 0 ? y : 251;
                int width = displacement < 0 ? 320 + displacement : 320 - displacement;
                int destinationX = displacement < 0 ? baseX : baseX + displacement;
                frame = Copy(frame, cursor, sourceX, sourceY, width, 1, destinationX, y, bucket, inputs);
                if (displacement > 0)
                {
                    uint second = frame.State.ReadDword(Cursor);
                    frame = frame with { State = frame.State.WriteDword(Cursor, unchecked(second + 24)) };
                    frame = Copy(frame, second, baseX, y, 320, 1, 320, 251, bucket, inputs);
                }
            }
            accumulator = unchecked(accumulator + amplitude);
        }
        return frame;
    }

    private static NativeBattleEffect140Frame Copy(NativeBattleEffect140Frame frame, uint address, int sourceX, int sourceY, int width, int height, int destinationX, int destinationY, int bucket, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleGfGeometryAttempt attempt;
        if (unchecked((short)width) <= 0 || unchecked((short)height) <= 0)
        {
            byte[] bytes = new byte[24];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[24];
            mask[3] = NativeBattleGfPacketByteState.Known;
            attempt = new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, [0], []) { PhysicalWriteMask = mask.ToImmutableArray() });
        }
        else
        {
            attempt = Packet(0, [0x05000000, 0x01000000, 0x80000000, XY(sourceX, sourceY), XY(destinationX, destinationY), XY(width, height)], bucket, []);
        }
        uint retainedCursor = frame.State.ReadDword(Cursor);
        frame = frame with { State = frame.State.WriteDword(Cursor, address) };
        frame = inputs.Execute(frame, new NativeBattleEffect140DrawBatch(new([attempt], 24), Cursor));
        return frame with { State = frame.State.WriteDword(Cursor, retainedCursor) };
    }
}

internal static class NativeBattleEffect140Fields
{
    internal const uint Cursor = 0x2517B0C;
    internal const uint Pause = 0x2517AAC;
    internal static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    internal static int I(NativeBattleEffectArena arena, int offset) => unchecked((int)U(arena, offset));
    internal static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    internal static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    internal static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    internal static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(S(arena, offset), S(arena, offset + 2), S(arena, offset + 4));
    internal static uint XY(int x, int y) => unchecked((ushort)x | (uint)(ushort)y << 16);
    internal static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    internal static NativeBattleGfGeometryAttempt Packet(int offset, ImmutableArray<uint> words, int bucket, ImmutableArray<int> depths)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, bytes.Length).Select(index => index < 3 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToImmutableArray();
        return new(offset, bytes.ToImmutableArray(), mask, new(bucket, words, depths) { PhysicalWriteMask = mask });
    }
    internal static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectArena.Clear().Write(0, NativeBattlePackedMatrix32.FromMatrix(matrix, null).Bytes.AsSpan()).MarkUnknown(18, 2), 0, arena, offset, 32);
    }
}
