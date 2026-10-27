using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaSecondInputs(NativeBattleShivaFinalInputs Geometry, ImmutableArray<int> ClippingSelectors, short Zsf4, short Dqa, int Dqb, Func<NativeBattleShivaFinalFrame, NativeBattleShivaFinalMeshDraw, NativeBattleShivaFinalFrame> DrawCommonMesh);

public static class NativeBattleShivaSecondShatter
{
    public static NativeBattleShivaFinalFrame Append5C2DE0(NativeBattleShivaFinalFrame frame) => Append(frame, 0x5C2DE0, [(0x0E, 0)]);

    public static NativeBattleShivaFinalStep Advance5C2DE0(NativeBattleShivaFinalFrame frame, int slot, uint graphicsFlags)
    {
        NativeBattleModulePoolTask task = Task(frame, slot, 0x5C2DE0);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, new([], 0), 0);
        }
        int counter = unchecked((short)task.ReadWord(0x0C));
        if (counter == 0)
        {
            frame = Append(frame, 0x5C2E90, [(0x10, 0), (0x14, 40), (0x12, 1828), (0x1C, 2304), (0x20, 2816), (0x22, 365)]);
        }
        if (counter is 14 or 31)
        {
            frame = Append(frame, 0x5C4140, [(0x10, 0), (0x12, 0), (0x14, 40), (0x1C, 2304), (0x20, 2816)]);
        }
        return Increment(frame, slot, 89);
    }

    public static NativeBattleShivaFinalStep Advance5C4140(NativeBattleShivaFinalFrame frame, int slot, uint graphicsFlags, NativeBattleShivaFinalInputs inputs, Func<NativeBattleShivaFinalFrame, NativeBattleShivaScrollingDraw, NativeBattleShivaFinalFrame> drawCommonScroll)
    {
        NativeBattleModulePoolTask task = Task(frame, slot, 0x5C4140);
        RequireMesh(inputs.MeshB, 0xD2D600);
        int counter = unchecked((short)task.ReadWord(0x0C));
        NativeBattleActionMatrix matrix = Basis(task, inputs.ViewWorld, counter, false);
        int? cue = counter < 4 ? (4 - counter) << 10 : counter < 8 ? null : (counter - 8) << 10;
        frame = drawCommonScroll(frame, new(inputs.MeshB, matrix, new(cue is null ? 0x30u : 0xF0u, cue, 0, counter * 8, 17, 3, new(0, 0, 64, 128))));
        return (graphicsFlags & 0x201) != 0 ? new(frame, new([], 0), 0) : Increment(frame, slot, 13);
    }

    public static NativeBattleShivaFinalStep Advance5C2E90(NativeBattleShivaFinalFrame frame, int slot, uint graphicsFlags, NativeBattleShivaSecondInputs inputs)
    {
        NativeBattleModulePoolTask task = Task(frame, slot, 0x5C2E90);
        NativeBattleShivaFinalInputs geometry = inputs.Geometry;
        RequireMesh(geometry.MeshA, 0xD2BA3C);
        int counter = unchecked((short)task.ReadWord(0x0C));
        bool paused = (graphicsFlags & 0x201) != 0;
        NativeBattleActionMatrix matrix = Basis(task, geometry.ViewWorld, counter, true);
        NativeBattleGfGeometryBatch packets = new([], 0);
        if (counter < 5)
        {
            NativeBattleShivaPlane plane = NativeBattleShivaPlaneClipping.CreatePlane5C3130(new(0, unchecked((short)(532 * counter - 2660)), 0), new(0, -4096, 0));
            packets = EmitStatic3170(geometry.MeshA, matrix, geometry.Projection, plane, inputs.ClippingSelectors, geometry.Zsf3, inputs.Zsf4, inputs.Dqa, inputs.Dqb, geometry.ObserveDepth);
        }
        else if (counter < 64)
        {
            frame = inputs.DrawCommonMesh(frame, new(geometry.MeshA, matrix, new(0x30, 0, 0, null, null, 0, 17, 2)));
        }
        else
        {
            if (counter == 64)
            {
                frame = Initialize3B10(frame, geometry.MeshA);
            }
            NativeBattleShivaFinalStep drawn = NativeBattleShivaFinalShatter.RenderAnimated3D10(frame, geometry.MeshA, 0x2E000, 287, counter + 14, matrix, paused, 3, 51, 6, geometry);
            frame = drawn.Frame;
            packets = drawn.CommonPackets;
        }
        if (paused)
        {
            return new(frame, packets, 0);
        }
        task = frame.Pool.Slots[slot];
        task = task.WriteWord(0x12, counter < 5 ? unchecked((ushort)(task.ReadWord(0x12) - task.ReadWord(0x22))) : (ushort)0);
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
        return new(frame, packets, unchecked((short)task.ReadWord(0x0C)) >= 89 && packets.AllocationBytes == 0 ? 2 : 0);
    }

    public static NativeBattleShivaFinalFrame Initialize3B10(NativeBattleShivaFinalFrame frame, NativeBattleStaticMeshResource mesh)
    {
        RequireMesh(mesh, 0xD2BA3C);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva radial fragments require their current physical arena.");
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 287; index++)
        {
            random = random.Draw(out ushort sample);
            NativeBattleVector[] vertices = Vertices(mesh, index);
            short x = (short)(vertices.Sum(vertex => vertex.X) / 3);
            short y = (short)(vertices.Sum(vertex => vertex.Y) / 3);
            short z = (short)(vertices.Sum(vertex => vertex.Z) / 3);
            int square = unchecked(x * x + y * y + z * z);
            if (square < 0)
            {
                throw new NotSupportedException("Native Shiva radial fragments exceed the admitted signed square-root input.");
            }
            int distance = (int)Math.Sqrt(square);
            var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            int speed = sample % 135 + 95;
            int record = 0x2E000 + index * 32;
            Put(0, x);
            Put(2, y);
            Put(4, z);
            Put(6, unchecked(distance * 14) / 2660);
            Put(0x10, unchecked((short)normalized.X) * speed >> 12);
            Put(0x12, unchecked((short)normalized.Y) * speed >> 12);
            Put(0x14, unchecked((short)normalized.Z) * speed >> 12);
            Put(0x0E, 0);
            Put(0x0A, 0);
            Put(0x18, 0);
            Put(0x1C, 0);
            Put(0x1A, (sample & 7) << 5);
            Put(0x1E, ((sample >> 3) & 7) << 3);

            void Put(int offset, int value)
            {
                Span<byte> bytes = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
                arena = arena.Write(record + offset, bytes);
            }
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Random = random };
    }

    public static NativeBattleGfGeometryBatch EmitStatic3170(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleShivaPlane plane, ImmutableArray<int> selectors, short zsf3, short zsf4, short dqa, int dqb, Action<ushort>? observeDepth = null)
    {
        RequireMesh(mesh, 0xD2BA3C);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int first = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) + 12);
        for (int index = 0; index < 287; index++)
        {
            NativeBattleVector[] vertices = Vertices(mesh, index);
            NativeBattleModuleProjectedVertex[] projected = vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(matrix, vertex, projection)).ToArray();
            foreach (NativeBattleModuleProjectedVertex vertex in projected)
            {
                observeDepth?.Invoke(vertex.Depth);
            }
            if (projected.Any(vertex => (vertex.Flags & 0x60000) != 0))
            {
                continue;
            }
            ReadOnlySpan<byte> face = mesh.Bytes.AsSpan().Slice(first + index * 20, 20);
            ImmutableArray<NativeBattleShivaClipVertex> clipped = NativeBattleShivaPlaneClipping.Clip5C34B0([new(vertices[0], BinaryPrimitives.ReadUInt16LittleEndian(face[12..])), new(vertices[1], BinaryPrimitives.ReadUInt16LittleEndian(face[16..])), new(vertices[2], BinaryPrimitives.ReadUInt16LittleEndian(face[10..]))], plane, selectors);
            if (clipped.IsEmpty)
            {
                continue;
            }
            NativeBattleGfGeometryAttempt attempt = NativeBattleShivaPlaneClipping.Emit5C3410(clipped, matrix, projection, BinaryPrimitives.ReadUInt32LittleEndian(face) & 0x02FFFFFFu, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]), BinaryPrimitives.ReadUInt32LittleEndian(face[16..]), zsf3, zsf4, dqa, dqb, allocated, observeDepth);
            attempts.Add(attempt);
            allocated += clipped.Length == 3 ? 32 : 40;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static NativeBattleActionMatrix Basis(NativeBattleModulePoolTask task, NativeBattleActionMatrix viewWorld, int counter, bool offset)
    {
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 64, 0)), unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x20)), unchecked((short)task.ReadWord(0x1C))) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) + (offset && counter is >= 52 and <= 61 ? 70 : 0) };
        return NativeBattlePoseMatrices.Compose56C2F0(viewWorld, local);
    }

    private static NativeBattleShivaFinalFrame Append(NativeBattleShivaFinalFrame frame, uint callback, (int Offset, ushort Value)[] words)
    {
        NativeBattleEffectTaskPool pool = frame.Pool;
        (pool, int slot) = pool.Append(callback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0);
        foreach (var word in words)
        {
            task = task.WriteWord(word.Offset, word.Value);
        }
        return frame with { Pool = pool.WriteSlot(slot, task) };
    }

    private static NativeBattleShivaFinalStep Increment(NativeBattleShivaFinalFrame frame, int slot, int threshold)
    {
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
        frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
        return new(frame, new([], 0), unchecked((short)task.ReadWord(0x0C)) >= threshold ? 2 : 0);
    }

    private static NativeBattleModulePoolTask Task(NativeBattleShivaFinalFrame frame, int slot, uint callback)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || (uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot].Callback != callback || (frame.Pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva secondary geometry requires its actual allocated child task.");
        }
        return frame.Pool.Slots[slot];
    }

    private static NativeBattleVector[] Vertices(NativeBattleStaticMeshResource mesh, int index)
    {
        int header = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int face = header + 12 + index * 20;
        NativeBattleVector[] vertices = new NativeBattleVector[3];
        for (int corner = 0; corner < 3; corner++)
        {
            int source = 8 + 4 * BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(face + 4 + corner * 2));
            if (source > header - 6)
            {
                throw new InvalidDataException("Native Shiva secondary face leaves its original vertex bank.");
            }
            vertices[corner] = new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(source)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(source + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(source + 4)));
        }
        return vertices;
    }

    private static void RequireMesh(NativeBattleStaticMeshResource mesh, uint address)
    {
        if (mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != 0x1BC4 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 166)
        {
            throw new InvalidDataException("Native Shiva secondary geometry requires its original166-vertex source.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)));
            if (count != (group == 2 ? 287 : 0))
            {
                throw new InvalidDataException("Native Shiva secondary geometry requires exactly287 original FT3 records.");
            }
            cursor += 4 + count * 20;
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Native Shiva secondary geometry differs from its complete original extent.");
        }
    }
}
