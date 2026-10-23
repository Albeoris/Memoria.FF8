using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath129Mesh
{
    public static NativeBattleBreath129Frame Draw6B5A50(NativeBattleBreath129Frame frame, uint temporary, int bucket, int depthShift, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 92);
        uint source = Dword(context, 0);
        context = context.Write(36, BitConverter.GetBytes(unchecked(source + ReadDword(frame, source) + 28)));
        frame = PublishContext(frame, temporary, context, 92);
        return Draw6B5A80(frame, temporary, bucket, depthShift, inputs);
    }

    public static NativeBattleBreath129Frame Draw6B5A80(NativeBattleBreath129Frame frame, uint temporary, int bucket, int depthShift, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 92);
        uint faces = Dword(context, 36);
        int count = unchecked((int)ReadDword(frame, faces));
        faces = checked(faces + 4);
        frame = PublishContext(frame, temporary, context.Write(36, BitConverter.GetBytes(faces)), 92);
        uint cursor = frame.State.ReadDword(0x252117C);
        if (count <= 0)
        {
            return frame;
        }
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Breath GT4 requires its current projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Native Breath GT4 requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Breath GT4 requires its current data registers."));
        for (int faceIndex = 0; faceIndex < count; faceIndex++)
        {
            context = Context(frame, temporary, 92);
            NativeBattleEffectArena face = ReadBytes(frame, faces, 36);
            uint selected = Dword(context, 32);
            for (int corner = 0; corner < 4; corner++)
            {
                ushort index = Word(face, 4 + corner * 2);
                context = NativeBattleEffectNodeContextMemory.Copy(ReadBytes(frame, checked(selected + (uint)index * 4), 8), 0, context, 56 + corner * 8, 8);
            }
            frame = PublishContext(frame, temporary, context, 92);
            for (int corner = 0; corner < 3; corner++)
            {
                registers = Load(registers, corner, context, 56 + corner * 8);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            byte[] mask = new byte[52];
            NativeBattleEffectArena packet = ReadBytes(frame, cursor, 52);
            Store(0, BitConverter.GetBytes(0x0C000000u));
            Store(4, face.Read(0, 4));
            Store(12, face.Read(12, 4));
            Store(24, face.Read(16, 4));
            Store(36, face.Read(20, 4));
            Store(48, BitConverter.GetBytes(Dword(face, 20) >> 16));
            uint firstFlags = registers.Colors.Flags ?? throw new NotSupportedException("Native Breath GT4 lost its RTPT flag result.");
            context = context.Write(52, BitConverter.GetBytes(firstFlags));
            bool accepted = (firstFlags & 0x60000) == 0;
            if (accepted)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    Store(8 + corner * 12, BitConverter.GetBytes(registers.Geometry.Sxy[corner]!.Value));
                }
                registers = Load(registers, 0, context, 80);
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                Store(44, BitConverter.GetBytes(registers.Geometry.Sxy[2]!.Value));
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Native Breath GT4 requires its current ZSF4."));
                bool outsideX = Enumerable.Range(0, 4).All(corner => Outside(BinaryPrimitives.ReadInt16LittleEndian(packet.Read(8 + corner * 12, 2)), 2560));
                bool outsideY = Enumerable.Range(0, 4).All(corner => Outside(BinaryPrimitives.ReadInt16LittleEndian(packet.Read(10 + corner * 12, 2)), 1728));
                accepted = !outsideX && !outsideY;
                if (accepted)
                {
                    uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Native Breath GT4 lost its raw depth result.");
                    context = context.Write(48, BitConverter.GetBytes(depth));
                    uint cue = Dword(context, 8);
                    if (cue != 0)
                    {
                        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Breath GT4 requires its primitive process.");
                        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Native Breath GT4 requires its far-color registers.");
                        far = far with { FarRed = (uint)context.Read(4, 1)[0] << 4, FarGreen = (uint)context.Read(5, 1)[0] << 4, FarBlue = (uint)context.Read(6, 1)[0] << 4 };
                        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far } } };
                        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, Dword(face, 24), Dword(face, 28), Dword(face, 32)) with { Ir0 = cue };
                        colors = NativeBattleScriptedDpct.Transform45F4C0(colors, far, inputs.Colors);
                        for (int corner = 0; corner < 3; corner++)
                        {
                            Store(16 + corner * 12, BitConverter.GetBytes(colors.Rgb[corner]!.Value));
                        }
                        NativeBattleScriptedDpcsResult first = NativeBattleScriptedDpcs.Transform45F270(colors, Dword(packet, 4), cue, far, inputs.Colors);
                        Store(4, BitConverter.GetBytes(first.PackedRgb));
                        registers = registers with { Colors = first.Registers };
                    }
                    else
                    {
                        for (int corner = 0; corner < 3; corner++)
                        {
                            Store(16 + corner * 12, face.Read(24 + corner * 4, 4));
                        }
                    }
                    int[] v = Enumerable.Range(0, 4).Select(corner => packet.Read(13 + corner * 12, 1)[0] + Word(context, 14)).ToArray();
                    bool overflow = v.Aggregate(0, (left, right) => left | right) > 255;
                    int subtract = overflow ? context.Read(28, 1)[0] : 0;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Store(13 + corner * 12, new byte[] { unchecked((byte)(v[corner] - subtract)) });
                    }
                    int selectedBucket = unchecked(bucket + (unchecked((int)depth) >> (depthShift & 31)));
                    ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Native Breath GT4 requires its actual four depth FIFO DWORDs.")))).ToImmutableArray();
                    frame = Publish(frame, registers);
                    frame = PublishContext(frame, temporary, context, 92);
                    frame = AppendWindow(frame, checked(cursor + 52), context, 16, selectedBucket, inputs);
                    frame = Append(frame, cursor, packet, mask, selectedBucket, payload, true, inputs);
                    frame = AppendWindow(frame, checked(cursor + 64), context, 24, selectedBucket, inputs);
                    cursor = checked(cursor + 76);
                }
            }
            if (!accepted)
            {
                frame = Publish(frame, registers);
                frame = PublishContext(frame, temporary, context, 92);
                frame = Append(frame, cursor, packet, mask, 0, [], false, inputs);
            }
            faces = checked(faces + 36);
            frame = PublishContext(frame, temporary, Context(frame, temporary, 92).Write(36, BitConverter.GetBytes(faces)), 92);

            void Store(int offset, byte[] bytes)
            {
                packet = packet.Write(offset, bytes);
                Array.Fill(mask, (byte)1, offset, bytes.Length);
            }
        }
        frame = Publish(frame, registers);
        return frame with { State = frame.State.WriteDword(0x252117C, cursor) };
    }

    private static bool Outside(short value, int upper) => value < 0 || value > upper;
    private static NativeBattleBreath129Frame Publish(NativeBattleBreath129Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static NativeBattleGeometryRegisterStep Load(NativeBattleGeometryRegisterStep registers, int slot, NativeBattleEffectArena context, int offset)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 8);
        ImmutableArray<byte?> packed = bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
        return slot switch
        {
            0 => registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } },
            1 => registers with { Geometry = registers.Geometry with { V1 = packed } },
            2 => registers with { Geometry = registers.Geometry with { V2 = packed } },
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }
    private static NativeBattleBreath129Frame AppendWindow(NativeBattleBreath129Frame frame, uint address, NativeBattleEffectArena context, int offset, int bucket, NativeBattleBreath129Inputs inputs)
    {
        uint x = context.Read(offset, 1)[0];
        uint y = context.Read(offset + 2, 1)[0];
        uint w = Word(context, offset + 4);
        uint h = Word(context, offset + 6);
        uint window = unchecked(0xE2000000u | ((~(w - 1) >> 3) & 31) | ((~(h - 1) & 248) << 2) | ((x & 248) << 7) | ((y & 248) << 12));
        NativeBattleEffectArena packet = ReadBytes(frame, address, 12).Write(0, BitConverter.GetBytes(0x02000000u)).Write(4, BitConverter.GetBytes(window)).Write(8, new byte[4]);
        return Append(frame, address, packet, Enumerable.Repeat((byte)1, 12).ToArray(), bucket, [0, 0, 0, 0], true, inputs);
    }
    private static NativeBattleBreath129Frame Append(NativeBattleBreath129Frame frame, uint address, NativeBattleEffectArena packet, byte[] writes, int bucket, ImmutableArray<int> payload, bool insert, NativeBattleBreath129Inputs inputs)
    {
        frame = WriteBytes(frame, address, packet, writes.Length);
        (byte[] data, bool[] known) = packet.ReadSnapshot(0, writes.Length);
        NativeBattleGfPacketByteState[] mask = writes.Select(value => value == 0 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToArray();
        return inputs.Execute(frame, new NativeBattleBreath129AppendPacket(address, writes.Length, writes.Length, bucket, 0, data, known) { WriteMask = mask, VertexPayload4 = payload, Insert = insert });
    }
    private static uint ReadDword(NativeBattleBreath129Frame frame, uint address) => Dword(ReadBytes(frame, address, 4), 0);
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
