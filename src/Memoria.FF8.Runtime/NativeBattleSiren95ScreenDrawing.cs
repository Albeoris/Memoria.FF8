using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95ScreenDrawServices(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, NativeBattleCameraProjection Projection, NativeBattleScriptedGraphicsMemory? Registers, Func<NativeBattleSiren95Frame, NativeBattleGfGeometryBatch, NativeBattleSiren95Frame> Submit);

public static class NativeBattleSiren95ScreenDrawing
{
    public static NativeBattleSiren95Frame Draw745F50(NativeBattleSiren95Frame frame, uint address, uint row, NativeBattleSiren95ScreenDrawServices services)
    {
        NativeBattleSiren95State state = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren screen drawing requires its retained module bytes.");
        NativeBattleModulePoolTask task = ReadTask(frame, address);
        using NativeBattleEffectArenaTransaction? owned = state.Globals.Transaction is null ? new(state.Globals) : null;
        NativeBattleEffectArena currentBytes = owned?.View ?? state.Globals;
        using NativeBattleEffectArenaWriter writer = new(currentBytes);
        bool published = false;
        short count = unchecked((short)(Half(row + 0xCC) + 1));
        count = count > 10 ? (short)10 : count;
        WriteHalf(row + 0xCC, unchecked((ushort)count));
        uint scroll = unchecked(Word(row + 0xC0) - 4096) & 0x7F00;
        WriteWord(row + 0xC0, scroll);
        WriteWord(row + 0xC4, unchecked(scroll + 0x4000));
        WriteWord(row + 0xC8, unchecked(scroll + 0x8000));
        for (int column = 0; column < 3; column++)
        {
            foreach (int offset in new[] { 0x60, 0x6C, 0x78, 0x84, 0x90, 0x9C, 0xA8, 0xB4 })
            {
                uint target = unchecked(row + (uint)(offset + column * 4));
                WriteWord(target, Word(target) & 0xFFFF00FF);
            }
            foreach ((int offset, uint value) in new (int, uint)[] { (0x60, scroll), (0x78, scroll), (0x90, scroll + 0x4000), (0xA8, scroll + 0x4000), (0x6C, scroll + 0x4000), (0x84, scroll + 0x4000), (0x9C, scroll + 0x8000), (0xB4, scroll + 0x8000) })
            {
                uint target = unchecked(row + (uint)(offset + column * 4));
                WriteWord(target, Word(target) | value);
            }
        }
        int angleX = (Half(row + 0xCE) + 128) & 4095;
        int angleY = (Half(row + 0xD0) + 128) & 4095;
        WriteHalf(row + 0xCE, (ushort)angleX);
        WriteHalf(row + 0xD0, (ushort)angleY);
        bool expanded = Spawned() >= 5;
        int amplitudeStep = expanded ? 192 : 96;
        int scale = expanded ? 4096 : 512;
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, unchecked((short)Half(row + 0x3E)));
        local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, unchecked((short)Half(row + 0x3C)));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        for (int line = 0; line < count + 1; line++)
        {
            int x = unchecked(NativeBattleCameraMath.Cosine(angleX) * (line * amplitudeStep)) / 4096;
            int y = unchecked(NativeBattleCameraMath.Cosine(angleY) * (line * 64)) / 4096;
            for (int column = 0; column < 4; column++)
            {
                uint original = unchecked(0x258EE38u + (uint)((line * 4 + column) * 8));
                uint destination = unchecked(0x258EC70u + (uint)((line * 4 + column) * 8));
                var snapshot = currentBytes.ReadSnapshot(Offset(original, 8), 8);
                writer.Write(Offset(destination, 8), snapshot.Data);
                for (int index = 0; index < 8; index++)
                {
                    if (!snapshot.Known[index])
                    {
                        writer.MarkUnknown(Offset(destination + (uint)index, 1), 1);
                    }
                }
                NativeBattleVector input = Point(destination);
                input = input with { X = unchecked((short)(input.X + x)), Y = unchecked((short)(input.Y + y)) };
                NativeBattleVector rotated = NativeBattleCuragaMatrices.RotateVector45DF80(local, input);
                WriteHalf(destination, unchecked((ushort)(rotated.X + unchecked((short)task.ReadWord(0x1C)))));
                WriteHalf(destination + 2, unchecked((ushort)(rotated.Y + unchecked((short)task.ReadWord(0x1E)))));
                WriteHalf(destination + 4, unchecked((ushort)(rotated.Z + unchecked((short)task.ReadWord(0x20)))));
            }
            angleX = (angleX + 256) & 4095;
            angleY = (angleY + 512) & 4095;
        }
        for (int line = 0; line < count + 1; line++)
        {
            for (int column = 0; column < 4; column++)
            {
                int color = line == 0 || column == 0 || column == 3 ? 0x44 : line == 1 ? 0x48 : line == count ? 0x44 : line == count - 1 ? 0x48 : 0x4C;
                WriteWord(unchecked(0x2585E3Cu + (uint)((line * 4 + column) * 4)), Word(row + (uint)color));
            }
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int line = 0; line < count; line++)
        {
            for (int column = 0; column < 3; column++)
            {
                NativeBattleScriptedGraphicsMemory controls = services.Registers ?? throw new NotSupportedException("Native Siren screen projection requires its actual current GTE controls.");
                short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Native Siren screen AVSZ4 requires its initialized current ZSF4.");
                uint source = unchecked(0x258EC70u + (uint)((line * 4 + column) * 8));
                NativeBattleModuleProjectedVertex[] points =
                [
                    NativeBattleModuleProjection.Project45FE10(services.View, Point(source), services.Projection),
                    NativeBattleModuleProjection.Project45FE10(services.View, Point(source + 8), services.Projection),
                    NativeBattleModuleProjection.Project45FE10(services.View, Point(source + 32), services.Projection),
                    NativeBattleModuleProjection.Project45FAA0(services.View, Point(source + 40), services.Projection, controls.Dqa, controls.Dqb)
                ];
                uint depth = unchecked((uint)points.Sum(point => (int)point.Depth) * (uint)(int)zsf4) >> 12;
                int bucket = unchecked(17 + ((int)depth >> 2));
                uint color = unchecked(0x2585E3Cu + (uint)((line * 4 + column) * 4));
                uint uv = unchecked(row + (uint)((line & 1) != 0 ? 0x60 : 0x6C) + (uint)(column * 4));
                uint[] words = [Word(row), Word(color), points[0].Screen, Word(uv), Word(color + 4), points[1].Screen, Word(uv + 24), Word(color + 16), points[2].Screen, Word(uv + 48), Word(color + 20), points[3].Screen, Word(uv + 72)];
                attempts.Add(Packet(allocated + 52, bucket, [0x02000000, Window(row + 0x50), 0], []));
                System.Numerics.Vector3 World(uint pointer)
                {
                    NativeBattleVector position = Point(pointer);
                    return new(position.X, position.Y, position.Z);
                }
                NativeBattlePilotVisualGeometry visual = new(NativeBattlePilotVisualLifetimes.Identity(95, task, line * 3 + column), World(source), World(source + 8), World(source + 32), World(source + 40), 4);
                attempts.Add(Packet(allocated, bucket, words, points.Select(point => (int)point.Depth).ToImmutableArray(), visual));
                attempts.Add(Packet(allocated + 64, bucket, [0x02000000, Window(row + 0x58), 0], []));
                allocated = checked(allocated + 76);
            }
        }
        NativeBattleEffectArena updated = owned?.Freeze() ?? writer.Freeze();
        published = true;
        frame = frame with { Memory = frame.Memory with { Siren95 = state with { Globals = updated } } };
        frame = services.Submit(frame, new(attempts.ToImmutable(), allocated));
        task = ReadTask(frame, address);
        if (((task.ReadByte(0xDE) + task.ReadByte(0x24)) & 1) == 0 && count >= 4 && (byte)Word(row + 0x4C) >= 64)
        {
            var child = NativeBattleSiren95Actors.AppendTask(frame, 0x257FA90, 0x7466A0, 0xE0, address, services.Packets);
            frame = child.Frame;
            NativeBattleModulePoolTask payload = ReadTask(frame, child.Address);
            for (int corner = 0; corner < 4; corner++)
            {
                int column = Random() & 3;
                int line = Random() % (count + 1);
                line = Spawned() >= 5 ? count - line / 2 : line / 2;
                uint point = unchecked(0x258EC70u + (uint)((line * 4 + column) * 8));
                NativeBattleVector first = PhysicalPoint(point);
                int dx = unchecked((short)PhysicalHalf(point + 8)) - first.X;
                short x = unchecked((short)(first.X + unchecked(dx * (Random() & 15)) / 16));
                int dy = unchecked((short)PhysicalHalf(point + 34)) - first.Y;
                short y = unchecked((short)(first.Y + unchecked(dy * (Random() & 15)) / 16));
                int dz = unchecked((short)PhysicalHalf(point + 36)) - first.Z;
                short z = unchecked((short)(first.Z + unchecked(dz * (Random() & 15)) / 16));
                payload = payload.WriteWord(0x6C + corner * 8, unchecked((ushort)x)).WriteWord(0x6E + corner * 8, unchecked((ushort)y)).WriteWord(0x70 + corner * 8, unchecked((ushort)z));
                if (Spawned() >= 5)
                {
                    payload = payload.WriteWord(0x6E + corner * 8, unchecked((ushort)(payload.ReadWord(0x6E + corner * 8) + (Random() & 1023) - 512)));
                }
            }
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(child.Address, payload).Pools } };
        }
        return frame;

        byte[] Read(uint pointer, int length)
        {
            if (published)
            {
                return (frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren screen read requires its published module owner.")).Read(pointer, length);
            }
            var snapshot = currentBytes.ReadSnapshot(Offset(pointer, length), length);
            if (snapshot.Known.Any(known => !known))
            {
                throw new NotSupportedException($"Native Siren screen read requires current physical bytes at 0x{pointer:X8}.");
            }
            return snapshot.Data;
        }
        ushort Half(uint pointer) => BinaryPrimitives.ReadUInt16LittleEndian(Read(pointer, 2));
        uint Word(uint pointer) => BinaryPrimitives.ReadUInt32LittleEndian(Read(pointer, 4));
        void WriteHalf(uint pointer, ushort value) => writer.Write(Offset(pointer, 2), BitConverter.GetBytes(value));
        void WriteWord(uint pointer, uint value) => writer.Write(Offset(pointer, 4), BitConverter.GetBytes(value));
        NativeBattleVector Point(uint pointer) => new(unchecked((short)Half(pointer)), unchecked((short)Half(pointer + 2)), unchecked((short)Half(pointer + 4)));
        ushort PhysicalHalf(uint pointer) => BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleSiren95PhysicalMemory.Read(frame, pointer, 2, Read));
        NativeBattleVector PhysicalPoint(uint pointer) => new(unchecked((short)PhysicalHalf(pointer)), unchecked((short)PhysicalHalf(pointer + 2)), unchecked((short)PhysicalHalf(pointer + 4)));
        uint Window(uint pointer) => NativeBattleEffectTextureWindows.Encode(new(unchecked((short)Half(pointer)), unchecked((short)Half(pointer + 2)), unchecked((short)Half(pointer + 4)), unchecked((short)Half(pointer + 6))));
        short Spawned() => unchecked((short)Half((state.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren screen drawing requires its director pointer.")) + 0x40));
        ushort Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleGfGeometryAttempt Packet(int offset, int bucket, uint[] words, ImmutableArray<int> depths, NativeBattlePilotVisualGeometry? visual = null)
    {
        byte[] data = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutableArray(), depths) { PhysicalWriteMask = mask, PilotGeometry = visual };
        return new(offset, data.ToImmutableArray(), mask, primitive);
    }
    private static int Offset(uint pointer, int length)
    {
        long offset = (long)pointer - NativeBattleSiren95State.GlobalAddress;
        if (offset < 0 || length < 0 || offset > NativeBattleSiren95State.GlobalLength - length)
        {
            throw new InvalidDataException("Native Siren screen access leaves its addressed module span.");
        }
        return (int)offset;
    }
    private static NativeBattleModulePoolTask ReadTask(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
}
