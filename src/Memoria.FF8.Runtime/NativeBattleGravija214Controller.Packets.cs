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
            int commonClip = 255;
            foreach (NativeBattleEffectArena row in rows)
            {
                commonClip &= row.Read(8, 1)[0];
            }
            if (!textured || commonClip == 0)
            {
                int length = textured ? (quad ? 40 : 32) : (quad ? 36 : 28);
                byte[] bytes = new byte[length];
                NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
                void Store(int offset, ReadOnlySpan<byte> data)
                {
                    data.CopyTo(bytes.AsSpan(offset));
                    Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, data.Length);
                }
                void Dword(int offset, uint value) => Store(offset, BitConverter.GetBytes(value));
                Dword(0, (uint)(length / 4 - 1) << 24);
                if (textured)
                {
                    Dword(4, quad ? 0x2E808080u : 0x26808080u);
                    int u0 = BinaryPrimitives.ReadUInt16LittleEndian(rows[0].Read(4, 2));
                    int ubase = u0 & 0xC0;
                    int x = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(context.Read(8, 4)) + u0);
                    int y = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(context.Read(12, 4)));
                    ushort page = unchecked((ushort)(((x >> 6) & 15) | (((y & 0x200) | 0x40) << 2) | ((y >> 4) & 0x10)));
                    Store(22, BitConverter.GetBytes(page));
                    for (int corner = 0; corner < corners; corner++)
                    {
                        Dword(8 + corner * 8, BinaryPrimitives.ReadUInt32LittleEndian(rows[corner].Read(0, 4)));
                        byte u = unchecked((byte)(BinaryPrimitives.ReadUInt16LittleEndian(rows[corner].Read(4, 2)) - ubase));
                        byte v = rows[corner].Read(6, 1)[0];
                        Store(12 + corner * 8, new byte[] { u, v });
                    }
                }
                else
                {
                    NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija color pass requires its current arithmetic owner.");
                    NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Gravija color pass requires its current far-color owner.");
                    NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Gravija color pass requires its current far-color controls.");
                    uint cue = colors.Ir0 ?? throw new NotSupportedException("Gravija color pass requires its current IR0.");
                    for (int corner = 0; corner < corners; corner++)
                    {
                        uint color = frame.State.ReadDword(cursor + (uint)(corner == 0 ? 0 : 8 + corner * 4));
                        colors = NativeBattleScriptedDpcs.Transform45F270(colors, color, cue, far, inputs.Colors).Registers;
                        uint shaded = colors.Rgb[2] ?? throw new InvalidDataException("Gravija DPCS did not publish RGB2.");
                        if (corner == 0)
                        {
                            shaded = (shaded & 0xFFFFFF) | (quad ? 0x3A000000u : 0x32000000u);
                        }
                        Dword(4 + corner * 8, shaded);
                        Dword(8 + corner * 8, BinaryPrimitives.ReadUInt32LittleEndian(rows[corner].Read(4, 4)));
                    }
                    frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = colors } };
                }
                frame = PublishContext(frame, temporary, context, 72);
                frame = Deliver214(frame, inputs, bytes, mask, bucket, 1);
            }
            cursor += (uint)(quad ? 24 : 20);
            context = context.Write(16, BitConverter.GetBytes(cursor));
        }
        return PublishContext(frame, temporary, context, 72);
    }
}
