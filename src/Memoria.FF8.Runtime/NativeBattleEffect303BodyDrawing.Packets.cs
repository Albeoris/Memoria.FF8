using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303BodyDrawing
{
    private static void EmitFaces303(byte[] source, bool quad, NativeBattleEffectArena[] corners, NativeBattleEffectArena temporary188, NativeBattleGeometryRegisters geometry, int bucket, int commonOffset, int effectOffset, ImmutableArray<NativeBattleEffect303BodyWrite>.Builder writes, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyList<System.Numerics.Vector3>? pilotWorld = null)
    {
        int length = quad ? 40 : 32;
        Window(0x94, effectOffset);
        SkinWrites overlay = new(length, commonOffset);
        overlay.Word(0, quad ? 0x09000000u : 0x07000000u);
        overlay.Word(4, U32(temporary188, 0x7C) | (quad ? 0x2E000000u : 0x26000000u));
        int[] us = Adjust303(corners.Select(corner => (int)corner.Read(7, 1)[0]).ToArray(), unchecked((int)U32(temporary188, 0x88)), temporary188.Read(0x8C, 1)[0]);
        int[] vs = Adjust303(corners.Select(corner => (int)corner.Read(8, 1)[0]).ToArray(), unchecked((int)U32(temporary188, 0x84)), temporary188.Read(0x90, 1)[0]);
        for (int corner = 0; corner < corners.Length; corner++)
        {
            overlay.Word(8 + corner * 8, U32(corners[corner], 0));
            overlay.Byte(12 + corner * 8, unchecked((byte)us[corner]));
            overlay.Byte(13 + corner * 8, unchecked((byte)vs[corner]));
        }
        overlay.Word16(14, U16(temporary188, 0x80));
        overlay.Word16(22, U16(temporary188, 0x82));
        Link(NativeBattleEffect303BodyBank.Common, overlay);
        Window(0xA0, effectOffset + 12);
        SkinWrites original = new(length, effectOffset + 24);
        original.Word(0, quad ? 0x09000000u : 0x07000000u);
        original.Word(4, U32(temporary188, 0x34) | (quad ? 0x2C000000u : 0x24000000u));
        for (int corner = 0; corner < corners.Length; corner++)
        {
            original.Word(8 + corner * 8, U32(corners[corner], 0));
        }
        original.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
        original.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12)));
        original.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)));
        if (quad)
        {
            original.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)));
        }
        Link(NativeBattleEffect303BodyBank.Effect, original);

        void Window(int offset, int destination)
        {
            SkinWrites packet = new(12, destination);
            packet.Word(0, U32(temporary188, offset));
            packet.Word(4, U32(temporary188, offset + 4));
            packet.Word(8, U32(temporary188, offset + 8));
            Link(NativeBattleEffect303BodyBank.Effect, packet);
        }

        void Link(NativeBattleEffect303BodyBank bank, SkinWrites packet)
        {
            ImmutableArray<uint> words = packet.Words();
            uint opcode = words[1] >> 24 & 0xFC;
            ImmutableArray<int> payload = [0, 0, 0, 0];
            if (opcode is >= 0x20 and <= 0x3C)
            {
                payload = (opcode & 8) != 0 ? geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect303 quad insertion requires its current depth FIFO.")))).ToImmutableArray() : [Depth(1), Depth(2), Depth(3), 0];
            }
            NativeBattleGeometryPrimitive primitive = new(bucket, words, payload);
            if (opcode is >= 0x20 and <= 0x3C)
            {
                primitive = NativeBattleEffect32Presentation.Capture(primitive, pilotIdentity, pilotWorld);
            }
            writes.Add(new(bank, packet.Attempt(primitive)));
        }

        int Depth(int index) => unchecked((int)(geometry.Sz[index] ?? throw new NotSupportedException("Effect303 triangle insertion requires its current depth FIFO.")));
    }

    private static int[] Adjust303(int[] coordinates, int offset, byte period)
    {
        int minimum = coordinates.Min();
        if (coordinates.Aggregate(0, (result, value) => result | (value - minimum)) > 128)
        {
            coordinates = coordinates.Select(value => (value - 128) & 255).ToArray();
        }
        coordinates = coordinates.Select(value => unchecked(value + offset)).ToArray();
        if (coordinates.Aggregate(0, (result, value) => result | value) > 255)
        {
            coordinates = coordinates.Select(value => unchecked((byte)(value - period))).Select(value => (int)value).ToArray();
        }
        return coordinates;
    }
}
