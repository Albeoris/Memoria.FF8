using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGravija214DeliverBatch(NativeBattleGfGeometryBatch Batch, ushort Flags) : NativeBattleGravija214Operation;

public static partial class NativeBattleGravija214Controller
{
    private static NativeBattleGravija214Frame Deliver214(NativeBattleGravija214Frame frame, NativeBattleGravija214Inputs inputs, byte[] bytes, NativeBattleGfPacketByteState[] mask, int bucket, ushort flags)
    {
        uint[] words = new uint[bytes.Length / 4];
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4));
        }
        NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutableArray(), flags == 0 ? [0, 0, 0, 0] : []);
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        NativeBattleGfGeometryBatch batch = new([attempt], bytes.Length);
        uint cursor = frame.State.ReadDword(0x2510114);
        frame = inputs.Execute(frame, new NativeBattleGravija214DeliverBatch(batch, flags));
        return frame with { State = frame.State.WriteDword(0x2510114, unchecked(cursor + (uint)bytes.Length)) };
    }

    private static NativeBattleGravija214Frame DrawMode214(NativeBattleGravija214Frame frame, NativeBattleGravija214Inputs inputs, int bucket, uint mode)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), mode);
        return Deliver214(frame, inputs, bytes, Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToArray(), bucket, 0);
    }

    private static NativeBattleGravija214Frame DrawDirectGroup214(NativeBattleGravija214Frame frame, uint temporary, bool quad, bool textured, int bucket, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 72);
        uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(16, 4));
        int count = checked((int)frame.State.ReadDword(cursor));
        cursor += 4;
        context = context.Write(16, BitConverter.GetBytes(cursor));
        int corners = quad ? 4 : 3;
        for (int face = 0; face < count; face++)
        {
            NativeBattleEffectArena[] rows = new NativeBattleEffectArena[corners];
            uint vertices = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4));
            for (int corner = 0; corner < corners; corner++)
            {
                uint row = vertices + frame.State.ReadWord(cursor + 4 + (uint)(corner * 2)) * 4u;
                rows[corner] = ReadBytes(frame, row, 12);
                context = NativeBattleEffectNodeContextMemory.Copy(rows[corner], 0, context, 24 + corner * 12, 12);
            }
            NativeBattleScreenMeshFace drawn = NativeBattleScreenMeshPackets.EmitFace(frame.Memory, frame.State.ReadView(cursor, quad ? 24 : 20), rows.ToImmutableArray(), quad, textured, textured ? unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(context.Read(8, 4))) : 0, textured ? unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(context.Read(12, 4))) : 0, bucket, inputs.Colors);
            frame = frame with { Memory = drawn.Memory };
            if (drawn.Attempt is { } attempt)
            {
                frame = PublishContext(frame, temporary, context, 72);
                frame = Deliver214(frame, inputs, attempt.Data.ToArray(), attempt.WriteMask.ToArray(), bucket, 1);
            }
            cursor += (uint)(quad ? 24 : 20);
            context = context.Write(16, BitConverter.GetBytes(cursor));
        }
        return PublishContext(frame, temporary, context, 72);
    }
}
