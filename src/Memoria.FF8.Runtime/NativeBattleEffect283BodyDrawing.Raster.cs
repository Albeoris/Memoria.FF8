using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect283BodyDrawing
{
    private static void EmitFaces(byte[] source, bool quad, NativeBattleEffectArena[] corners, NativeBattleEffectArena temporary124, NativeBattleGeometryRegisters geometry, int bucket, int commonOffset, ImmutableArray<NativeBattleEffect283BodyWrite>.Builder writes)
    {
        int texturedLength = quad ? 40 : 32;
        int flatLength = quad ? 24 : 20;
        SkinWrites textured = new(texturedLength, commonOffset);
        textured.Word(0, quad ? 0x09000000u : 0x07000000u);
        uint opcode = (quad ? 0x2C000000u : 0x24000000u) | ((uint)(BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(14)) & 0x200) << 16);
        uint color = U32(temporary124, 0x34);
        textured.Word(4, color | opcode);
        for (int corner = 0; corner < corners.Length; corner++)
        {
            textured.Word(8 + corner * 8, U32(corners[corner], 0));
        }
        textured.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
        textured.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12)) & 0xFDFFFFFFu);
        textured.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)));
        if (quad)
        {
            textured.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)));
        }
        Link(textured);
        SkinWrites flat = new(flatLength, commonOffset + texturedLength);
        flat.Word(0, quad ? 0x05000000u : 0x04000000u);
        flat.Word(4, color | (quad ? 0x2A000000u : 0x22000000u));
        for (int corner = 0; corner < corners.Length; corner++)
        {
            flat.Word(8 + corner * 4, U32(corners[corner], 0));
        }
        Link(flat);
        SkinWrites mode = new(8, commonOffset + texturedLength + flatLength);
        mode.Word(0, 0x01000000);
        mode.Word(4, 0xE1000240);
        Link(mode);

        void Link(SkinWrites packet)
        {
            ImmutableArray<uint> words = packet.Words();
            uint command = words[1] >> 24 & 0xFC;
            ImmutableArray<int> payload = [0, 0, 0, 0];
            if (command is >= 0x20 and <= 0x3C)
            {
                payload = (command & 8) != 0 ? geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect283 quad insertion requires its current depth FIFO.")))).ToImmutableArray() : [Depth(1), Depth(2), Depth(3), 0];
            }
            writes.Add(new(NativeBattleEffect283BodyBank.Common, packet.Attempt(new(bucket, words, payload))));
        }

        int Depth(int index) => unchecked((int)(geometry.Sz[index] ?? throw new NotSupportedException("Effect283 triangle insertion requires its current depth FIFO.")));
    }
}
