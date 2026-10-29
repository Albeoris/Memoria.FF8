using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect140Fields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect140MirroredStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context152, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect140Meshes
{
    public static NativeBattleEffect140Frame InitializeW(NativeBattleEffect140Frame frame, uint source, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        (NativeBattleEffect140State state, NativeBattleFileStaging staging) = frame.State.BindSources(RequireStaging(frame), resources);
        frame = frame with { State = state, Staging = staging };
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(source);
        (frame, uint temporary) = Allocate(frame, 704);
        int count = I(ReadBytes(frame, source + 4, 4), 0);
        for (int index = 0; index < 87; index++)
        {
            uint vertex = source + 8u + (uint)(index * 8);
            frame = WriteBytes(frame, vertex + 6, Word(NativeBattleEffectArena.Clear(), 0, 4096 - 47 * index), 2);
            frame = WriteBytes(frame, temporary + (uint)(index * 8), ReadBytes(frame, vertex, 8), 8);
        }
        uint faces = source + U(ReadBytes(frame, source, 4), 0) + 28;
        int polygons = I(ReadBytes(frame, faces, 4), 0);
        uint selected = source + 8;
        for (int vertex = 87; vertex < count; vertex++)
        {
            uint destination = source + 8u + (uint)(vertex * 8);
            frame = WriteBytes(frame, temporary + 696, ReadBytes(frame, destination, 8), 8);
            int minimum = int.MaxValue;
            for (int polygon = 0; polygon < polygons; polygon++)
            {
                NativeBattleEffectArena face = ReadBytes(frame, faces + 4u + (uint)(polygon * 36), 36);
                int[] indices = Enumerable.Range(0, 4).Select(corner => unchecked((ushort)S(face, 4 + corner * 2))).Select(value => (int)value).ToArray();
                if (!indices.Contains(vertex))
                {
                    continue;
                }
                foreach (int candidate in indices)
                {
                    if (candidate >= 87)
                    {
                        continue;
                    }
                    NativeBattleEffectArena first = ReadBytes(frame, temporary + (uint)(candidate * 8), 8);
                    NativeBattleEffectArena second = ReadBytes(frame, temporary + 696, 8);
                    NativeBattleGeometryRegisterStep registers = Registers(frame);
                    registers = registers with { Colors = registers.Colors with { Ir = Enumerable.Range(0, 3).Select(axis => (int?)unchecked((int)(U(first, axis * 2) - U(second, axis * 2)))).ToImmutableArray() } };
                    registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
                    int distance = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
                    frame = Publish(frame, registers);
                    if (distance < minimum)
                    {
                        minimum = distance;
                        selected = temporary + (uint)(candidate * 8);
                    }
                }
            }
            frame = WriteBytes(frame, destination + 6, ReadBytes(frame, selected + 6, 2), 2);
        }
        return Release(frame, 704);
    }

    public static NativeBattleEffect140Frame DrawMirrored(NativeBattleEffect140Frame frame, uint temporary152, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena context = Context(frame, temporary152, 152);
        uint source = U(context, 0);
        NativeBattleStaticMeshResource template = resources.ReadMesh(source);
        NativeBattleEffect140MirroredStep drawn = DrawLoaded(frame.Memory, template, inputs.ReadProjection(frame), context, inputs.Colors, offset => ReadBytes(frame, source + 8u + (uint)offset, 8), pilotIdentity);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary152, drawn.Context152, 152);
        return inputs.Execute(frame, new NativeBattleEffect140DrawBatch(drawn.Batch, Cursor));
    }

    public static NativeBattleEffect140MirroredStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, Func<int, NativeBattleEffectArena>? readCurrentVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (U(context, 0) != mesh.Address || U(context, 4) != mesh.Address + 8)
        {
            throw new InvalidDataException("Effect140 mirrored mesh requires its current addressed source and vertices.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect140 requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect140 requires current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect140 requires current projection controls.");
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 28;
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
        cursor += 4;
        context = Put(context, 0x24, mesh.Address + (uint)cursor);
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int polygon = 0; polygon < count; polygon++)
        {
            ReadOnlySpan<byte> face = mesh.Bytes.AsSpan().Slice(cursor, 36);
            for (int corner = 0; corner < 4; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2));
                NativeBattleEffectArena row = readCurrentVertex is null ? NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(8 + index * 4, 8)) : readCurrentVertex(index * 4);
                context = NativeBattleEffectNodeContextMemory.Copy(row, 0, context, 0x3C + corner * 8, 8);
                int brightness = unchecked(S(context, 0x42 + corner * 8) + I(context, 0x38));
                uint rgb = brightness <= 0 ? corner == 0 ? 0x3E000000u : 0 : BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(corner == 0 ? 0 : 20 + corner * 4));
                if (brightness > 0 && unchecked(brightness << 4) < 4096)
                {
                    registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(brightness << 4)) } }, (byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16));
                    registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
                    rgb = (rgb & 0xFF000000) | (byte)registers.Colors.Ir[0]!.Value | (uint)(byte)registers.Colors.Ir[1]!.Value << 8 | (uint)(byte)registers.Colors.Ir[2]!.Value << 16;
                }
                context = Put(context, 0x5C + corner * 4, rgb);
            }
            context = context.Write(0x6C, face.Slice(12, 8)).Write(0x74, face.Slice(20, 4));
            int[] sums = new int[4];
            for (int corner = 0; corner < 4; corner++)
            {
                sums[corner] = context.Read(0x6C + (corner < 2 ? corner * 4 : 8 + (corner - 2) * 2), 1)[0] + unchecked((ushort)S(context, 0x10));
            }
            int mask = sums.Aggregate(0, (value, sum) => value | sum);
            for (int corner = 0; corner < 4; corner++)
            {
                int at = 0x6C + (corner < 2 ? corner * 4 : 8 + (corner - 2) * 2);
                context = context.Write(at, [(byte)(sums[corner] - (mask > 255 ? context.Read(0x20, 1)[0] : 0))]);
            }
            for (int mirror = 0; mirror < 2; mirror++)
            {
                DrawFace(mirror);
                if (mirror == 0)
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        context = Word(context, 0x3C + corner * 8, -S(context, 0x3C + corner * 8));
                    }
                }
            }
            cursor += 36;
            context = Put(context, 0x24, mesh.Address + (uint)cursor);
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, context, new(attempts.ToImmutable(), allocated));

        void DrawFace(int mirror)
        {
            NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
            System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
            void Capture(int corner)
            {
                if (world is not null)
                {
                    world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix!, Vector(context, 0x3C + corner * 8)), projection);
                }
            }
            byte[] packet = new byte[52];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[52];
            Store(0, 0x0C000000);
            Store(12, U(context, 0x6C));
            Store(24, U(context, 0x70));
            Store(36, U(context, 0x74));
            Store(48, U(context, 0x74) >> 16);
            for (int corner = 0; corner < 3; corner++)
            {
                registers = NativeBattleGeometry.LoadVector(registers, corner, Vector(context, 0x3C + corner * 8), S(context, 0x42 + corner * 8));
                Capture(corner);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint flags = registers.Colors.Flags!.Value;
            context = Put(context, 0x34, flags);
            if ((flags & 0x60000) != 0)
            {
                Reject();
                return;
            }
            uint[] screens = new uint[4];
            int[] depths = new int[4];
            for (int corner = 0; corner < 3; corner++)
            {
                screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                depths[corner] = (int)registers.Geometry.Sz[corner + 1]!.Value;
                Store(8 + corner * 12, screens[corner]);
            }
            Capture(3);
            registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, Vector(context, 0x54), S(context, 0x5A)), projection, controls.Dqa, controls.Dqb);
            screens[3] = registers.Geometry.Sxy[2]!.Value;
            depths[3] = (int)registers.Geometry.Sz[3]!.Value;
            Store(44, screens[3]);
            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect140 requires its current AVSZ4 factor."));
            context = Put(context, 0x30, registers.Geometry.Otz!.Value);
            if (screens.All(value => unchecked((short)value) is < 0 or > 2560) || screens.All(value => unchecked((short)(value >> 16)) is < 0 or > 1728))
            {
                Reject();
                return;
            }
            for (int corner = 0; corner < 4; corner++)
            {
                Store(4 + corner * 12, U(context, 0x5C + corner * 4));
            }
            int bucket = unchecked(17 + ((int)registers.Geometry.Otz!.Value >> 2));
            ImmutableArray<NativeBattleGfPacketByteState> written = mask.ToImmutableArray();
            ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4))).ToImmutableArray();
            attempts.Add(Packet(allocated + 52, [0x02000000, Window(0x14), 0], bucket, []));
            attempts.Add(new(allocated, packet.ToImmutableArray(), written, new(bucket, words, depths.ToImmutableArray()) { PhysicalWriteMask = written, PilotGeometry = pilotIdentity is { } identity && world is not null ? new(identity with { Primitive = unchecked((identity.Primitive * 65537 + cursor) * 2 + mirror) }, world[0], world[1], world[2], world[3], 4) { SourceDepths = new(depths[0], depths[1], depths[2], depths[3]), UseSourceDepths = true } : null }));
            attempts.Add(Packet(allocated + 64, [0x02000000, Window(0x1C), 0], bucket, []));
            allocated += 76;

            void Store(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
                if (offset == 0)
                {
                    Array.Fill(mask, NativeBattleGfPacketByteState.Untouched, 0, 3);
                }
            }
            void Reject() => attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), null));
        }
        uint Window(int offset)
        {
            uint value = unchecked((uint)(((ushort)S(context, offset + 2) & 0xF8) | unchecked((int)0xFFFE2000)));
            value = unchecked((value << 5) | ((uint)(ushort)S(context, offset) & 0xF8));
            value = unchecked((value << 5) | ((uint)~((ushort)S(context, offset + 6) - 1) & 0xF8));
            return unchecked((value << 2) | ((uint)(~((ushort)S(context, offset + 4) - 1) >> 3) & 0x1F));
        }
    }
}
