using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSonic139Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSonic139Warp
{
    public static NativeBattleSonic139Frame Draw6A9260(NativeBattleSonic139Frame frame, uint geometry, uint morph, int bucket, int coefficient, int amplitude, int phase, NativeBattleSonic139Inputs inputs, NativeBattleSonic139Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<byte> saved = frame.Memory.PrimitiveProcess?.Software?.EnvironmentBuffers?.CurrentGlobal ?? throw new NotSupportedException("Native Sonic warp requires its actual current drawing environment.");
        if (saved.Length != 92)
        {
            throw new InvalidDataException("Native Sonic warp requires a complete drawing environment.");
        }
        short opposite = BinaryPrimitives.ReadInt16LittleEndian(saved.AsSpan().Slice(8)) != 0 ? (short)0 : (short)320;
        uint first = Cursor(frame);
        frame = SetCursor(frame, checked(first + 12));
        NativeBattleEffectArena mode = ReadBytes(frame, first, 12).Write(3, new byte[] { 2 });
        uint e1 = 0xE1000000u | ((uint)BinaryPrimitives.ReadUInt16LittleEndian(saved.AsSpan().Slice(20)) & 0x9FF) | (saved[22] != 0 ? 0x200u : 0) | (saved[23] != 0 ? 0x400u : 0);
        mode = mode.Write(4, BitConverter.GetBytes(e1)).Write(8, BitConverter.GetBytes(0xE2000000u));
        frame = Append(frame, first, 12, 12, bucket, 0, mode, inputs, true);
        frame = BuildScreenRecords6A9260(frame, geometry, morph, amplitude, phase, resources);
        (frame, uint temporary) = Allocate(frame, 76);
        uint records = checked(Dword(frame, 0x2517B58) + 0x1584);
        uint faces = checked(geometry + Dword(frame, geometry) + 0x10);
        NativeBattleEffectArena context = Context(frame, temporary, 76).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(records)).Write(12, BitConverter.GetBytes(576)).Write(16, BitConverter.GetBytes(256)).Write(20, BitConverter.GetBytes(faces)).Write(24, BitConverter.GetBytes(checked(TableBase(frame) + (uint)bucket * 4)));
        frame = PublishContext(frame, temporary, context, 76);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Sonic warp requires its current primitive process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Native Sonic warp requires its current far-color controls.");
        far = far with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Sonic warp requires its current color registers.");
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = colors with { Ir0 = unchecked((uint)coefficient) } } };
        frame = DrawGouraud6A97C0(frame, temporary, inputs, pilotIdentity: pilotIdentity is { } gouraud ? gouraud with { Primitive = unchecked(gouraud.Primitive * 397) } : null);
        uint between = Cursor(frame);
        frame = SetCursor(frame, checked(between + 8));
        NativeBattleEffectArena midpoint = ReadBytes(frame, between, 8).Write(0, BitConverter.GetBytes(0x01000000u)).Write(4, BitConverter.GetBytes(0xE1000220u));
        frame = Append(frame, between, 8, 8, bucket, 0, midpoint, inputs);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 76).Write(20, BitConverter.GetBytes(faces)), 76);
        frame = DrawTextured6A9640(frame, temporary, inputs, pilotIdentity: pilotIdentity is { } textured ? textured with { Primitive = unchecked(textured.Primitive * 397 + 1) } : null);
        frame = Release(frame, 76);
        frame = Environment(frame, saved, opposite, BinaryPrimitives.ReadInt16LittleEndian(saved.AsSpan().Slice(10)), false, bucket, inputs);
        return Environment(frame, saved, 576, 256, true, 4385, inputs);
    }

    public static NativeBattleSonic139Frame BuildScreenRecords6A9260(NativeBattleSonic139Frame frame, uint geometry, uint morph, int amplitude, int phase, NativeBattleSonic139Resources resources)
    {
        int count = unchecked((int)Dword(frame, geometry + 4));
        if (count <= 0 || count > NativeBattleEffectArena.Length / 12 || resources.SineWords4096.Length != 4096)
        {
            throw new InvalidDataException("Native Sonic warp requires its bounded positive screen-record count and sine table.");
        }
        uint destination = checked(Dword(frame, 0x2517B58) + 0x1584);
        for (int index = 0; index < count; index++)
        {
            uint source = checked(morph + (uint)index * 8);
            uint metadata = Dword(frame, source + 4);
            int sine = resources.SineWords4096[unchecked((((int)metadata >> 18) & ~3) + phase) & 4095];
            int radial = unchecked(sine * amplitude) >> 12;
            int x = Signed(frame, source);
            int y = Signed(frame, source + 2);
            short warpedX = unchecked((short)(x + ((unchecked((int)(metadata << 22)) >> 22) * radial >> 12)));
            short warpedY = unchecked((short)(y + ((unchecked((int)(metadata << 12)) >> 22) * radial >> 12)));
            frame = TransformXY(frame, warpedX, warpedY);
            NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters!;
            short screenX = unchecked((short)colors.Ir[0]!.Value);
            short screenY = unchecked((short)colors.Ir[1]!.Value);
            uint target = checked(destination + (uint)index * 12);
            NativeBattleEffectArena record = ReadBytes(frame, target, 12).Write(0, BitConverter.GetBytes(screenX)).Write(2, BitConverter.GetBytes(screenY));
            if (screenX < 0)
            {
                record = record.Write(8, new byte[] { (byte)(record.Read(8, 1)[0] | 1) });
            }
            else if (screenX >= 320)
            {
                record = record.Write(8, new byte[] { (byte)(record.Read(8, 1)[0] | 2) });
            }
            else
            {
                record = record.Write(8, new byte[2]);
            }
            if (screenY < 0)
            {
                record = record.Write(8, new byte[] { (byte)(record.Read(8, 1)[0] | 4) });
            }
            else if (screenY >= 216)
            {
                record = record.Write(8, new byte[] { (byte)(record.Read(8, 1)[0] | 8) });
            }
            frame = TransformXY(frame, unchecked((short)x), unchecked((short)y));
            colors = frame.Memory.ScriptedColorRegisters!;
            short u = unchecked((short)colors.Ir[0]!.Value);
            short v = unchecked((short)colors.Ir[1]!.Value);
            if (u < 0)
            {
                u = 0;
            }
            else if (u >= 320)
            {
                u = 319;
            }
            else if (v < 0)
            {
                v = 0;
            }
            else if (v >= 216)
            {
                v = 215;
            }
            record = record.Write(4, BitConverter.GetBytes(u)).Write(6, BitConverter.GetBytes(v));
            frame = WriteBytes(frame, target, record, 12);
        }
        return frame;
    }

    private static NativeBattleSonic139Frame TransformXY(NativeBattleSonic139Frame frame, short x, short y)
    {
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Sonic transform requires its retained V0 input.");
        byte[] input = colors.NormalInput?.ToArray() ?? new byte[8];
        bool[] known = colors.NormalInputKnown?.ToArray() ?? Enumerable.Repeat(colors.NormalInput.HasValue, 8).ToArray();
        BitConverter.GetBytes(x).CopyTo(input, 0);
        BitConverter.GetBytes(y).CopyTo(input, 2);
        Array.Fill(known, true, 0, 4);
        colors = colors with { NormalInput = input.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() };
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.Transform(new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Native Sonic transform requires its current matrix."), colors));
        return frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
    }

    private static NativeBattleSonic139Frame Environment(NativeBattleSonic139Frame frame, ImmutableArray<byte> saved, short x, short y, bool restore, int bucket, NativeBattleSonic139Inputs inputs)
    {
        byte[] source = new byte[92];
        short[] rectangle = [x, y, 320, 216, x, y];
        for (int index = 0; index < rectangle.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(index * 2), rectangle[index]);
        }
        saved.AsSpan().Slice(20, 4).CopyTo(source.AsSpan(20));
        source[22] = restore ? saved[22] : (byte)0;
        source[24] = restore ? (byte)1 : (byte)0;
        uint address = Cursor(frame);
        frame = SetCursor(frame, checked(address + 64));
        NativeBattleEffectArena retained = ReadBytes(frame, address, 64);
        (byte[] bytes, _) = retained.ReadSnapshot(0, 64);
        ImmutableArray<byte> packet = NativeBattleDrawEnvironmentPackets.Restore45C0F0(bytes.ToImmutableArray(), source.ToImmutableArray());
        int written = restore ? 36 : 24;
        retained = retained.Write(3, packet.AsSpan().Slice(3, written - 3));
        return Append(frame, address, 64, written, bucket, 0, retained, inputs, true);
    }

    private static NativeBattleSonic139Frame Append(NativeBattleSonic139Frame frame, uint address, int allocation, int written, int bucket, ushort flags, NativeBattleEffectArena packet, NativeBattleSonic139Inputs inputs, bool preserveLink = false, NativeBattlePilotVisualGeometry? pilotGeometry = null)
    {
        frame = WriteBytes(frame, address, packet, allocation);
        (byte[] bytes, bool[] known) = packet.ReadSnapshot(0, allocation);
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, preserveLink ? 3 : 0, written - (preserveLink ? 3 : 0));
        if (flags == 1 && written == 32)
        {
            mask[14] = NativeBattleGfPacketByteState.Untouched;
            mask[15] = NativeBattleGfPacketByteState.Untouched;
            mask[30] = NativeBattleGfPacketByteState.Untouched;
            mask[31] = NativeBattleGfPacketByteState.Untouched;
        }
        return inputs.Execute(frame, new NativeBattleSonic139AppendPacket(address, allocation, written, bucket, flags, bytes, known) { WriteMask = mask, PilotGeometry = pilotGeometry });
    }

    private static uint Cursor(NativeBattleSonic139Frame frame) => Dword(frame, 0x2520BF4);
    private static NativeBattleSonic139Frame SetCursor(NativeBattleSonic139Frame frame, uint value) => WriteBytes(frame, 0x2520BF4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    private static uint Dword(NativeBattleSonic139Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address, 4).Read(0, 4));
    private static short Signed(NativeBattleSonic139Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, address, 2).Read(0, 2));
    private static uint TableBase(NativeBattleSonic139Frame frame) => checked(0x1D8E058u + 17544u * (frame.Memory.PrimitiveProcess?.Buffer80 ?? throw new NotSupportedException("Native Sonic packets require their actual ordering-table bank.")));
}
