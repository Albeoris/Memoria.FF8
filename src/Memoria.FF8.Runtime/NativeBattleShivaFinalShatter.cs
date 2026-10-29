using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaFinalFrame(NativeBattleEffectMemory Memory, NativeBattleEffectTaskPool Pool, NativeCrtRandomState Random, int Phase22BD010, int PrivateDrawOffset);
public sealed record NativeBattleShivaFinalMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleGfGeometryContext Context);
public sealed record NativeBattleShivaFinalInputs(NativeBattleActionMatrix ViewWorld, NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleCameraProjection Projection, short Zsf3, NativeBattleSummonColors Colors, Func<NativeBattleShivaFinalFrame, NativeBattleGfGeometryBatch, NativeBattleShivaFinalFrame> SubmitPrivatePackets, Func<NativeBattleShivaFinalFrame, NativeBattleShivaFinalMeshDraw, NativeBattleShivaFinalFrame> DrawPrivateMesh, Func<NativeBattleShivaFinalFrame, NativeBattleShivaScrollingDraw, NativeBattleShivaFinalFrame> DrawPrivateScroll, Action<NativeBattleShivaShardGteOperation>? ObserveGte = null, Action<ushort>? ObserveDepth = null, Action<int>? ObserveDepthCue = null);
public sealed record NativeBattleShivaFinalStep(NativeBattleShivaFinalFrame Frame, NativeBattleGfGeometryBatch CommonPackets, int Result);

public static class NativeBattleShivaFinalShatter
{
    public static NativeBattleShivaFinalFrame Initialize5C7570(NativeBattleShivaFinalFrame frame, NativeBattleStaticMeshResource meshA, NativeBattleStaticMeshResource meshB)
    {
        ValidatePool(frame.Pool);
        RequireMesh(meshA, 0xD257CC);
        RequireMesh(meshB, 0xD27E6C);
        NativeBattleEffectTaskPool pool = frame.Pool;
        (pool, int slot) = pool.Append(0x5C77C0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x10, 0).WriteWord(0x12, 700).WriteWord(0x14, unchecked((ushort)-7000));
        frame = frame with { Pool = pool.WriteSlot(slot, task) };
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva final fragments require their retained physical arena.");
        NativeCrtRandomState random = frame.Random;
        Initialize(meshA, 0x20000);
        Initialize(meshB, 0x24000);
        return frame with { Memory = frame.Memory with { Arena = arena }, Random = random };

        void Initialize(NativeBattleStaticMeshResource mesh, int start)
        {
            int faces = FaceStart(mesh);
            for (int index = 0; index < 400; index++)
            {
                random = random.Draw(out ushort sample);
                NativeBattleVector[] vertices = Vertices(mesh, faces + index * 20);
                int x = vertices.Sum(vertex => vertex.X) / 3;
                int y = vertices.Sum(vertex => vertex.Y) / 3;
                int z = vertices.Sum(vertex => vertex.Z) / 3;
                int square = unchecked(x * x + y * y + (z + 5050) * (z + 5050));
                if (square < 0)
                {
                    throw new NotSupportedException("Native Shiva final fragment radius exceeds its admitted signed square-root input.");
                }
                int record = start + index * 32;
                Put(record, x);
                Put(record + 2, y);
                Put(record + 4, z);
                Put(record + 6, unchecked((int)Math.Sqrt(square) << 4) / 10800);
                foreach (int offset in new[] { 8, 10, 12, 14, 16, 20, 24, 28 })
                {
                    Put(record + offset, 0);
                }
                Put(record + 0x12, -10 - sample % 190);
                int angle = 24 * (sample & 7);
                Put(record + 0x1A, (sample & 1) != 0 ? -angle : angle);
                Put(record + 0x1E, ((sample >> 3) & 7) << 4);
            }
        }

        void Put(int address, int value) => arena = PutWord(arena, address, value);
    }

    public static NativeBattleShivaFinalStep Advance5C77C0(NativeBattleShivaFinalFrame frame, int slot, uint graphicsFlags, NativeBattleShivaFinalInputs inputs)
    {
        ValidatePool(frame.Pool);
        RequireMesh(inputs.MeshA, 0xD257CC);
        RequireMesh(inputs.MeshB, 0xD27E6C);
        ArgumentNullException.ThrowIfNull(inputs.SubmitPrivatePackets);
        ArgumentNullException.ThrowIfNull(inputs.DrawPrivateMesh);
        ArgumentNullException.ThrowIfNull(inputs.DrawPrivateScroll);
        if ((uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C77C0 } task || (task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva final worker requires its actual allocated task.");
        }
        int counter = unchecked((short)task.ReadWord(0x0C));
        bool paused = (graphicsFlags & 0x201) != 0;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 2048, 0)), 5888, 6144, 6144) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) };
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(inputs.ViewWorld, local);
        NativeBattleGfGeometryBatch common = new([], 0);
        if (counter < 16)
        {
            Draw(inputs.MeshA, 0x20000, 0, 2);
            Draw(inputs.MeshB, 0x24000, 3, 3);
        }
        else if (counter < 32)
        {
            frame = inputs.DrawPrivateMesh(frame, new(inputs.MeshA, basis, new(0, 0, 0, null, null, 0, 17, 2)));
            if (counter <= 26)
            {
                frame = inputs.DrawPrivateScroll(frame, new(inputs.MeshB, basis, new(counter < 18 ? 3u : 0xC3u, counter < 18 ? null : (counter - 18) << 9, 0, 12 * (counter - 18), 17, 3, new(64, 0, 64, 128))));
            }
        }
        else
        {
            frame = frame with { Phase22BD010 = frame.Phase22BD010 | 2 };
            NativeBattleShivaFinalStep drawn = RenderAnimated3D10(frame, inputs.MeshA, 0x20000, 400, counter - 32, basis, paused, 5, 24, 8, inputs, NativeBattlePilotVisualLifetimes.Identity(185, task));
            frame = drawn.Frame;
            common = drawn.CommonPackets;
        }
        if (paused)
        {
            return new(frame, common, 0);
        }
        task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
        frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
        return new(frame, common, unchecked((short)task.ReadWord(0x0C)) >= 64 ? 2 : 0);

        NativeBattleGfGeometryBatch Draw(NativeBattleStaticMeshResource mesh, int start, uint flags, int shift)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva final worker requires its current fragment records.");
            int faces = FaceStart(mesh);
            int allocated = 0;
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            inputs.ObserveGte?.Invoke(new(basis, 0));
            for (int index = 0; index < 400; index++)
            {
                int record = start + index * 32;
                if (BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + 6, 2)) >= counter && counter < 16)
                {
                    continue;
                }
                int faceOffset = faces + index * 20;
                byte[] face = mesh.Bytes.AsSpan().Slice(faceOffset, 20).ToArray();
                NativeBattleVector[] vertices = Vertices(mesh, faceOffset);
                if (!paused)
                {
                    arena = PutWord(arena, record + 8, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + 8, 2)) + 48);
                }
                int brightness = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + 8, 2));
                uint color = brightness < 97 ? unchecked(0x26000000u | (uint)(brightness << 16) | (uint)(brightness << 8) | (uint)brightness) : 0x24808080u;
                BinaryPrimitives.WriteUInt32LittleEndian(face, color | ((flags & 1) != 0 ? 0x02000000u : 0));
                NativeBattleGfGeometryAttempt attempt = NativeBattleShivaShardPackets.EmitTriangle5C2410(face, corner => NativeBattleModuleProjection.Project45FE10(basis, vertices[corner], inputs.Projection), false, false, new(flags, 0, 0, null, null, 0, 17, checked((byte)shift)), inputs.Zsf3, inputs.Colors, allocated, inputs.ObserveDepth, inputs.ObserveDepthCue);
                attempt = NativeBattleShivaVisualIdentity.Capture(attempt, NativeBattlePilotVisualLifetimes.Identity(185, task, unchecked((int)mesh.Address * 397 + faceOffset)), vertices, basis, inputs.Projection);
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated += 32;
                }
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            NativeBattleGfGeometryBatch batch = new(attempts.ToImmutable(), allocated);
            frame = inputs.SubmitPrivatePackets(frame, batch);
            return batch;
        }
    }

    public static NativeBattleShivaFinalStep RenderAnimated3D10(NativeBattleShivaFinalFrame frame, NativeBattleStaticMeshResource mesh, int start, int count, int phase, NativeBattleActionMatrix basis, bool paused, int dampingShift, int maximumLifetime, int scaleShift, NativeBattleShivaFinalInputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((dampingShift, maximumLifetime, scaleShift) is not ((5, 24, 8) or (3, 51, 6)) || count is not (400 or 287))
        {
            throw new NotSupportedException("Native Shiva fragment rendering requires its reached current animation parameters.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva fragments require their current physical arena.");
        int faces = FaceStart(mesh);
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        inputs.ObserveGte?.Invoke(new(null, 0));
        for (int index = 0; index < count; index++)
        {
            int record = start + index * 32;
            int lifetime = Word(14);
            if (lifetime < 0)
            {
                continue;
            }
            int faceOffset = faces + index * 20;
            byte[] face = mesh.Bytes.AsSpan().Slice(faceOffset, 20).ToArray();
            NativeBattleVector[] vertices = Vertices(mesh, faceOffset);
            NativeBattleActionMatrix matrix = basis;
            int cue = 0;
            if (Word(6) < phase)
            {
                int cx = Word(0);
                int cy = Word(2);
                int cz = Word(4);
                int x = cx + Word(8);
                int y = cy + Word(10);
                int z = cz + Word(12);
                for (int corner = 0; corner < 3; corner++)
                {
                    vertices[corner] = new(unchecked((short)(vertices[corner].X - cx)), unchecked((short)(vertices[corner].Y - cy)), unchecked((short)(vertices[corner].Z - cz)));
                }
                int scale = 4096 - (lifetime << scaleShift);
                cue = lifetime == 0 ? -4096 : 4096 / maximumLifetime * lifetime;
                if (!paused)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = Word(0x10 + axis * 2);
                        Put(8 + axis * 2, Word(8 + axis * 2) + velocity);
                        Put(0x10 + axis * 2, velocity - (velocity >> dampingShift));
                    }
                    Put(0x1C, Word(0x1C) + Word(0x1E));
                    Put(0x18, Word(0x18) + Word(0x1A));
                    int next = unchecked((short)(lifetime + 1));
                    Put(14, next >= maximumLifetime ? -1 : next);
                }
                NativeBattleActionMatrix fragment = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(Word(0x18), 0, Word(0x1C))), scale, scale, scale) with { X = x, Y = y, Z = z };
                matrix = NativeBattlePoseMatrices.Compose56C2F0(basis, fragment);
                BinaryPrimitives.WriteUInt32LittleEndian(face, BinaryPrimitives.ReadUInt32LittleEndian(face) | 0x02000000u);
            }
            inputs.ObserveGte?.Invoke(new(matrix, null));
            NativeBattleGfGeometryAttempt attempt = NativeBattleShivaShardPackets.EmitTriangle5C2410(face, corner => NativeBattleModuleProjection.Project45FE10(matrix, vertices[corner], inputs.Projection), false, true, new(0x33, 0, cue, null, null, 0, 17, 2), inputs.Zsf3, inputs.Colors, allocated, inputs.ObserveDepth, inputs.ObserveDepthCue);
            if (pilotIdentity is { } identity)
            {
                attempt = NativeBattleShivaVisualIdentity.Capture(attempt, identity with { Primitive = unchecked((int)mesh.Address * 397 + faceOffset) }, vertices, matrix, inputs.Projection);
            }
            attempts.Add(attempt);
            if (attempt.Primitive is not null)
            {
                allocated += 32;
            }

            short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + offset, 2));
            void Put(int offset, int value) => arena = PutWord(arena, record + offset, value);
        }
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, new(attempts.ToImmutable(), allocated), 0);
    }

    private static int FaceStart(NativeBattleStaticMeshResource mesh) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) + 12);

    private static NativeBattleVector[] Vertices(NativeBattleStaticMeshResource mesh, int face)
    {
        NativeBattleVector[] vertices = new NativeBattleVector[3];
        int end = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        for (int corner = 0; corner < 3; corner++)
        {
            int source = 8 + BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(face + 4 + corner * 2)) * 4;
            if (source > end - 6)
            {
                throw new InvalidDataException("Native Shiva final face leaves its original source vertices.");
            }
            vertices[corner] = new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(source)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(source + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(source + 4)));
        }
        return vertices;
    }

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int address, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(address, bytes);
    }

    private static void RequireMesh(NativeBattleStaticMeshResource mesh, uint address)
    {
        if (mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != 0x26A0 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 231)
        {
            throw new InvalidDataException("Native Shiva final shatter requires its complete original231-vertex source.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)));
            if (count != (group == 2 ? 400 : 0))
            {
                throw new InvalidDataException("Native Shiva final source requires exactly400 FT3 records.");
            }
            cursor += 4 + count * 20;
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Native Shiva final source extent differs from its original directory.");
        }
    }

    private static void ValidatePool(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        if (pool.DescriptorAddress != 0x22BC198 || pool.BankAddress != 0x22BC1A8 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Shiva final shatter requires its original child task pool.");
        }
    }
}
