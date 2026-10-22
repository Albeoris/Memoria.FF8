using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindowedMeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleWindowedMesh
{
    public static NativeBattleWindowedMeshStep Draw(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource source, NativeBattleEffectArena context, NativeBattleCameraProjection projection, NativeBattleWindowedMeshLayout layout, Func<uint, (NativeBattleVector Vertex, short? W)> readVertex, int tableBaseBucket = 17, byte depthShift = 2)
    {
        context.Validate();
        if (source.Bytes.IsDefault || source.Bytes.Length < 8 || Dword(context, 0) != source.Address || depthShift > 31 || context.Regions.Any(region => region.Offset < 0 || region.Offset > layout.ContextBytes - region.Data.Length) || context.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > layout.ContextBytes - region.Length))
        {
            throw new InvalidDataException("Windowed mesh requires its actual bounded mesh,typed context and ordering shift.");
        }
        int vertexCount = BinaryPrimitives.ReadInt32LittleEndian(Read(4, 4));
        int faceStart = BinaryPrimitives.ReadInt32LittleEndian(Read(0, 4));
        int cursor = checked(faceStart + 12);
        context = Put(context, layout.FaceCursor, checked(source.Address + (uint)cursor));
        NativeBattleGeometryRegisterStep? registers = null;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach ((int group, int skip) in layout.Groups)
        {
            cursor = checked(cursor + skip);
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context = Put(context, layout.FaceCursor, checked(source.Address + (uint)cursor));
            int faceBytes = group == 7 ? 36 : group == 4 ? 20 : 24;
            bool quad = group != 4;
            bool textured = group is 3 or 7;
            int polygonBytes = group switch { 3 => 40, 4 => 28, 5 => 36, _ => 52 };
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, faceBytes).ToArray();
                NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Windowed mesh requires its current projection controls.");
                registers ??= new(memory.GeometryRegisters ?? throw new NotSupportedException("Windowed mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Windowed mesh requires its current color registers."));
                NativeBattleGeometryRegisterStep step = registers;
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector vertex, short? w) = Vertex(face, corner);
                    step = NativeBattleGeometry.LoadVector(step, corner, vertex, w);
                }
                step = NativeBattleGeometry.ProjectTriple(step, projection, controls.Dqa, controls.Dqb);
                uint flags = step.Colors.Flags ?? throw new InvalidDataException("Windowed mesh projection did not publish FLAG.");
                context = Put(context, layout.Flags, flags);
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                NativeWrites writes = new(polygonBytes, allocated);
                writes.Dword(0, (uint)(polygonBytes / 4 - 1) << 24);
                writes.Dword(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
                if (textured)
                {
                    writes.Dword(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)));
                    writes.Dword(group == 3 ? 20 : 24, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)));
                    uint tail = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20));
                    writes.Dword(group == 3 ? 28 : 36, tail);
                    writes.Dword(group == 3 ? 36 : 48, tail >> 16);
                }
                if ((flags & 0x60000) != 0)
                {
                    attempts.Add(writes.Attempt(null));
                    registers = step;
                    cursor = checked(cursor + faceBytes);
                    continue;
                }
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(step.Geometry.Sxy[corner] ?? throw new InvalidDataException("Windowed mesh requires actual SXY output."), unchecked((ushort)(step.Geometry.Sz[corner + 1] ?? throw new InvalidDataException("Windowed mesh requires actual SZ output."))), flags);
                    writes.Dword(ScreenOffset(group, corner), corners[corner].Screen);
                }
                if (quad)
                {
                    (NativeBattleVector vertex, short? w) = Vertex(face, 3);
                    step = NativeBattleGeometry.LoadVector(step, 0, vertex, w);
                    step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
                    corners[3] = new(step.Geometry.Sxy[2] ?? throw new InvalidDataException("Windowed mesh requires its fourth SXY output."), unchecked((ushort)(step.Geometry.Sz[3] ?? throw new InvalidDataException("Windowed mesh requires its fourth SZ output."))), step.Colors.Flags ?? throw new InvalidDataException("Windowed mesh requires its fourth FLAG output."));
                    writes.Dword(ScreenOffset(group, 3), corners[3].Screen);
                    step = NativeBattleGeometry.Average45E610(step, controls.Zsf4 ?? throw new NotSupportedException("Windowed mesh requires its actual ZSF4."));
                }
                else
                {
                    step = NativeBattleGeometry.Average45E5C0(step, controls.Zsf3 ?? throw new NotSupportedException("Windowed mesh requires its actual ZSF3."));
                }
                registers = step;
                if (corners.All(corner => Outside(unchecked((short)corner.Screen), 0xA00)) || corners.All(corner => Outside(unchecked((short)(corner.Screen >> 16)), 0x6C0)))
                {
                    attempts.Add(writes.Attempt(null));
                    cursor = checked(cursor + faceBytes);
                    continue;
                }
                uint depth = step.Geometry.Otz ?? throw new InvalidDataException("Windowed mesh requires its actual average depth output.");
                context = Put(context, layout.Depth, depth);
                if (textured)
                {
                    int offset = Word(context, layout.UOffset);
                    int[] uOffsets = [12, 16, 20, 22];
                    int[] u = uOffsets.Select(index => face[index] + offset).ToArray();
                    bool overflow = u.Aggregate(0, (combined, value) => combined | value) > 255;
                    int subtract = overflow ? context.Read(layout.WindowB + 4, 1)[0] : 0;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        face[uOffsets[corner]] = unchecked((byte)(u[corner] - subtract));
                    }
                }
                // Native admission and GTE publication are complete. This call only packs the current polygon layout.
                NativeBattleMeshPacketContext packing = new(group >= 4 ? 0x20u : 0x10u, null, null, null, null, 0, 0, tableBaseBucket, depthShift);
                NativeBattleMeshPacketVariant variant = new(null, null, false, false) { SkipOffscreenRejection = true };
                NativeBattleGfGeometryAttempt packed = NativeBattleGfGeometryPackets.EmitAttempt(face, corners, group, packing, null, allocated, variant: variant);
                NativeBattleGeometryPrimitive polygon = (packed.Primitive ?? throw new InvalidDataException("Windowed mesh admitted polygon was rejected by its layout encoder.")) with { Bucket = checked(tableBaseBucket + (unchecked((int)depth) >> depthShift)), VertexPayload4 = [] };
                if (!textured)
                {
                    attempts.Add(packed with { Primitive = polygon });
                    allocated = checked(allocated + polygonBytes);
                }
                else
                {
                    attempts.Add(packed with { Primitive = null });
                    attempts.Add(Environment(checked(allocated + polygonBytes), Window(context, layout.WindowA), polygon.Bucket));
                    attempts.Add(packed with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, polygonBytes).ToImmutableArray(), Primitive = polygon });
                    attempts.Add(Environment(checked(allocated + polygonBytes + 12), Window(context, layout.WindowB), polygon.Bucket));
                    allocated = checked(allocated + polygonBytes + 24);
                }
                cursor = checked(cursor + faceBytes);
            }
            context = Put(context, layout.FaceCursor, checked(source.Address + (uint)cursor));
        }
        if (registers is not null)
        {
            memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        }
        return new(memory, context, new(attempts.ToImmutable(), allocated));

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > source.Bytes.Length - length)
            {
                throw new InvalidDataException("Windowed mesh geometry read leaves its original addressed source.");
            }
            return source.Bytes.AsSpan().Slice(offset, length);
        }

        (NativeBattleVector Vertex, short? W) Vertex(byte[] face, int corner)
        {
            ushort index = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2));
            int offset = checked(index * 4);
            if ((index & 1) != 0 || vertexCount < 0 || offset > checked(vertexCount * 8) - 8)
            {
                throw new InvalidDataException("Windowed mesh face requires its actual packed vertex row.");
            }
            return readVertex(checked(Dword(context, layout.SelectedVertices) + (uint)offset));
        }
    }

    private static bool Outside(short value, int upper) => value < 0 || value > upper;
    private static int ScreenOffset(int group, int corner) => 8 + corner * (group == 7 ? 12 : 8);
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));

    private static uint Window(NativeBattleEffectArena context, int offset)
    {
        uint x = context.Read(offset, 1)[0];
        uint y = context.Read(offset + 2, 1)[0];
        ushort width = Word(context, offset + 4);
        ushort height = Word(context, offset + 6);
        uint value = (y & 0xF8) | 0xFFFE2000u;
        value = unchecked(value << 5) | (x & 0xF8);
        value = unchecked(value << 5) | (unchecked((uint)~(height - 1)) & 0xF8);
        value = unchecked(value << 2) | (unchecked((uint)((short)~(width - 1) >> 3)) & 0x1F);
        return value;
    }

    private static NativeBattleGfGeometryAttempt Environment(int offset, uint command, int bucket)
    {
        ImmutableArray<uint> words = [0x02000000, command, 0];
        byte[] bytes = new byte[12];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 12).ToImmutableArray(), new(bucket, words, []));
    }

    private sealed class NativeWrites(int length, int offset)
    {
        private readonly byte[] bytes = new byte[length];
        private readonly NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];

        public void Dword(int position, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(position), value);
            mask.AsSpan(position, 4).Fill(NativeBattleGfPacketByteState.Known);
        }

        public NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive) => new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }
}
