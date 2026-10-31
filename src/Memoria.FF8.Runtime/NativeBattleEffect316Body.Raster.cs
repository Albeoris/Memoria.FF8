using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect316Body
{
    public static bool UniformGray(IReadOnlyList<byte> gray, bool expanded)
    {
        int count = expanded ? gray.Count : 3;
        int combined = 0xFF;
        for (int index = 0; index < count; index++)
        {
            combined &= gray[index];
        }
        return combined == 0x80;
    }

    public static NativeBattleEffect316Frame Raster661720(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint context, int ordering, int shift) => RasterFaces(frame, resources, inputs, context, ordering, shift, false);
    public static NativeBattleEffect316Frame Raster661EB0(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint context, int ordering, int shift) => RasterFaces(frame, resources, inputs, context, ordering, shift, true);

    private static NativeBattleEffect316Frame RasterFaces(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint contextAddress, int ordering, int shift, bool expanded)
    {
        int length = expanded ? 0x90 : 0x64;
        NativeBattleEffectArena context = inputs.ReadArena(frame, contextAddress, length);
        uint faces = Dword(context, 0);
        uint vertices = Dword(context, 4);
        int cursorOffset = expanded ? 0x88 : 0x5C;
        uint common = Dword(context, cursorOffset);
        uint secondary = Dword(context, cursorOffset + 4);
        int vertexStride = expanded ? 16 : 8;
        for (int shape = 0; shape < 2; shape++)
        {
            int corners = shape == 0 ? 3 : 4;
            int faceLength = shape == 0 ? 16 : 20;
            int count = unchecked((ushort)Word(context, 8 + shape * 2));
            for (int index = 0; index < count; index++, faces += (uint)faceLength)
            {
                NativeBattleEffectArena face = inputs.ReadArena(frame, faces, faceLength);
                NativeBattleEffectArena[] rows = new NativeBattleEffectArena[corners];
                byte[] gray = new byte[corners];
                short[] depths = new short[corners];
                uint[] screen = new uint[corners];
                int depthOr = 0;
                for (int corner = 0; corner < corners; corner++)
                {
                    int vertex = unchecked((ushort)Word(face, corner * 2)) & 0xFFF;
                    rows[corner] = inputs.ReadArena(frame, vertices + unchecked((uint)(vertex * vertexStride)), vertexStride);
                    context = Copy(rows[corner], 0, context, 0x1C + corner * vertexStride, vertexStride);
                    depths[corner] = Word(rows[corner], 4);
                    depthOr |= unchecked((ushort)depths[corner]);
                }
                if (unchecked((short)depthOr) <= 0)
                {
                    continue;
                }
                int tested = expanded ? corners : 3;
                int grayOr = 0;
                if (expanded)
                {
                    for (int corner = 0; corner < corners; corner++)
                    {
                        gray[corner] = rows[corner].Read(6, 1)[0];
                        grayOr |= gray[corner];
                    }
                    if (grayOr == 0)
                    {
                        continue;
                    }
                }
                NativeBattleGeometryRegisterStep registers = Registers(frame);
                for (int corner = 0; corner < 3; corner++)
                {
                    screen[corner] = Dword(rows[corner], 0);
                }
                registers = NativeBattleGeometry.Clip45EE10(registers with { Geometry = registers.Geometry with { Sxy = screen.Take(3).Select(value => (uint?)value).ToImmutableArray() } });
                int area = unchecked((int)(registers.Geometry.Mac0 ?? throw new NotSupportedException("The face requires its current clipping result.")));
                context = PutDword(context, expanded ? 0x5C : 0x3C, unchecked((uint)area));
                frame = Publish(frame, registers);
                if (area < 0)
                {
                    continue;
                }
                if (corners == 4)
                {
                    screen[3] = Dword(rows[3], 0);
                }
                uint?[] sz = registers.Geometry.Sz.ToArray();
                for (int corner = 0; corner < corners; corner++)
                {
                    sz[corner + (corners == 3 ? 1 : 0)] = unchecked((uint)(int)depths[corner]);
                }
                registers = registers with { Geometry = registers.Geometry with { Sz = sz.ToImmutableArray() } };
                NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("The face requires its actual depth-average controls.");
                registers = corners == 3 ? NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("The face requires ZSF3.")) : NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("The face requires ZSF4."));
                uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("The face requires its averaged ordering depth.");
                context = PutDword(context, expanded ? 0x64 : 0x44, depth);
                frame = Publish(frame, registers);
                int bucket = unchecked((int)depth) >> (shift & 31);
                int tableBucket = unchecked(ordering + bucket);
                if (!expanded)
                {
                    for (int corner = 0; corner < tested; corner++)
                    {
                        gray[corner] = rows[corner].Read(6, 1)[0];
                        grayOr |= gray[corner];
                    }
                }
                if (frame.PilotIdentity is { } owner)
                {
                    System.Numerics.Vector3 World(int corner)
                    {
                        int vertex = unchecked((ushort)Word(face, corner * 2)) & 0xFFF;
                        return frame.PresentationWorld[vertices + unchecked((uint)(vertex * vertexStride))];
                    }
                    NativeBattlePilotVisualIdentity identity = NativeBattleActor33Presentation.Resource(owner, faces, vertices)!.Value;
                    frame = frame with { PresentationFace = new(identity, World(0), World(1), World(2), corners == 4 ? World(3) : default, checked((byte)corners)) { DepthDivisor = 4 } };
                }
                bool uniform = UniformGray(gray, expanded);
                frame = Write(frame, inputs, contextAddress, context, length);
                if (!expanded && grayOr == 0 || uniform)
                {
                    Packet packet = Textured(face, screen, corners, false);
                    packet.Store32(4, Dword(context, 0x14) | (corners == 3 ? 0x24000000u : 0x2C000000u));
                    if (uniform)
                    {
                        packet.AddWord(0x16, Word(context, expanded ? 0x6E : 0x4E));
                        packet.AddWord(0x0E, Word(context, expanded ? 0x70 : 0x50));
                    }
                    frame = Emit(frame, inputs, packet, tableBucket, CommonCursorGlobal, depths);
                    common = unchecked(common + (uint)packet.Length);
                    continue;
                }
                if (expanded)
                {
                    Packet textured = Textured(face, screen, corners, true);
                    Packet untextured = new(corners == 3 ? 28 : 36);
                    for (int corner = 0; corner < corners; corner++)
                    {
                        uint color = (uint)gray[corner] * 0x010101;
                        textured.Store32(4 + corner * 12, color | (corner == 0 ? corners == 3 ? 0x36000000u : 0x3E000000u : 0));
                        untextured.Store32(4 + corner * 8, color | (corner == 0 ? corners == 3 ? 0x32000000u : 0x3A000000u : 0));
                        untextured.Store32(8 + corner * 8, screen[corner]);
                    }
                    textured.AddWord(0x1A, Word(context, 0x6E));
                    textured.AddWord(0x0E, Word(context, 0x70));
                    context = PutDword(context, cursorOffset + 4, unchecked(secondary + (uint)untextured.Length));
                    frame = Write(frame, inputs, contextAddress, context, length);
                    frame = Emit(frame, inputs, textured, tableBucket, CommonCursorGlobal, depths);
                    common = unchecked(common + (uint)textured.Length);
                    frame = Emit(frame, inputs, untextured, tableBucket, PrivateCursorGlobal, depths);
                    secondary = unchecked(secondary + (uint)untextured.Length);
                    Packet mode = new(8);
                    mode.Store32(4, 0xE10002C0);
                    context = PutDword(context, cursorOffset + 4, secondary + 8);
                    frame = Write(frame, inputs, contextAddress, context, length);
                    frame = Emit(frame, inputs, mode, tableBucket, PrivateCursorGlobal, []);
                    secondary += 8;
                    frame = NextRandom662570(frame, out int random);
                    if (random < 0x800)
                    {
                        NativeBattleEffectArena point = Copy(context, 0x7C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int sum = rows.Sum(row => (int)Word(row, 8 + axis * 2));
                            point = PutWord(point, axis * 2, sum / corners);
                        }
                        context = Copy(point, 0, context, 0x7C, 8);
                        frame = Write(frame, inputs, contextAddress, context, length);
                        frame = Birth662590(frame, inputs, Dword(context, 0x84), point);
                    }
                }
                else
                {
                    Packet ordinary = Textured(face, screen, corners, true);
                    Packet complement = Textured(face, screen, corners, true);
                    if (corners == 4)
                    {
                        gray[3] = rows[3].Read(6, 1)[0];
                    }
                    for (int corner = 0; corner < corners; corner++)
                    {
                        uint color = (uint)gray[corner] * 0x010101;
                        ordinary.Store32(4 + corner * 12, color | (corner == 0 ? corners == 3 ? 0x34000000u : 0x3C000000u : 0));
                        complement.Store32(4 + corner * 12, unchecked((corner == 0 ? corners == 3 ? 0x36808080u : 0x3E808080u : 0x808080u) - color));
                    }
                    context = PutDword(context, cursorOffset + 4, unchecked(secondary + (uint)complement.Length));
                    frame = Write(frame, inputs, contextAddress, context, length);
                    frame = Emit(frame, inputs, complement, tableBucket, PrivateCursorGlobal, depths);
                    secondary = unchecked(secondary + (uint)complement.Length);
                    ordinary.AddWord(0x1A, Word(context, 0x4E));
                    ordinary.AddWord(0x0E, Word(context, 0x50));
                    frame = Emit(frame, inputs, ordinary, tableBucket, CommonCursorGlobal, depths);
                    common = unchecked(common + (uint)ordinary.Length);
                }
            }
        }
        context = PutDword(PutDword(context, cursorOffset, common), cursorOffset + 4, secondary);
        return Write(frame, inputs, contextAddress, context, length);
    }

    private static Packet Textured(NativeBattleEffectArena face, uint[] screen, int corners, bool gouraud)
    {
        Packet packet = new(gouraud ? corners == 3 ? 40 : 52 : corners == 3 ? 32 : 40);
        int stride = gouraud ? 12 : 8;
        for (int corner = 0; corner < corners; corner++)
        {
            packet.Store32(8 + corner * stride, screen[corner]);
        }
        packet.Store32(12, Dword(face, 8));
        packet.Store32(12 + stride, Dword(face, 12));
        packet.Store16(12 + stride * 2, unchecked((ushort)Word(face, corners == 3 ? 6 : 16)));
        if (corners == 4)
        {
            packet.Store16(12 + stride * 3, unchecked((ushort)Word(face, 18)));
        }
        return packet;
    }

    private static NativeBattleEffect316Frame Emit(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, Packet packet, int bucket, uint cursor, IReadOnlyList<short> depths)
    {
        ImmutableArray<int> payload = depths.Count == 3 ? [depths[0], depths[1], depths[2], 0] : depths.Select(value => (int)value).ToImmutableArray();
        if (payload.Length != 4)
        {
            payload = [0, 0, 0, 0];
        }
        NativeBattleGeometryPrimitive primitive = new(bucket, Enumerable.Range(0, packet.Length / 4).Select(index => BitConverter.ToUInt32(packet.Data, index * 4)).ToImmutableArray(), payload);
        if (depths.Count is 3 or 4 && frame.PresentationFace is { } face)
        {
            primitive = primitive with { PilotGeometry = face with { Identity = face.Identity with { Primitive = unchecked((int)cursor) }, SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true } };
        }
        NativeBattleGfGeometryAttempt attempt = new(0, packet.Data.ToImmutableArray(), packet.Mask.ToImmutableArray(), primitive);
        return inputs.Execute(frame, new NativeBattleEffect316DrawBatch(new([attempt], packet.Length), cursor));
    }

    private sealed class Packet
    {
        public byte[] Data { get; }
        public NativeBattleGfPacketByteState[] Mask { get; }
        public int Length => Data.Length;

        public Packet(int length)
        {
            Data = new byte[length];
            Mask = new NativeBattleGfPacketByteState[length];
            Store32(0, unchecked((uint)(length / 4 - 1) << 24));
        }

        public void Store16(int offset, ushort value)
        {
            BitConverter.GetBytes(value).CopyTo(Data, offset);
            Array.Fill(Mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }

        public void Store32(int offset, uint value)
        {
            BitConverter.GetBytes(value).CopyTo(Data, offset);
            Array.Fill(Mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }

        public void AddWord(int offset, int value)
        {
            Store16(offset, unchecked((ushort)(BitConverter.ToUInt16(Data, offset) + value)));
        }
    }
}
