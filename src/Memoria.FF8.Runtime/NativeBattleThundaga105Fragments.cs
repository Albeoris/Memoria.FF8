using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105Bounds(NativeBattleVector Minimum, NativeBattleVector Maximum)
{
    public static NativeBattleThundaga105Bounds Begin6D5FA0() => new(new(32767, 32767, 32767), new(-32767, -32767, -32767));
}
public sealed record NativeBattleThundaga105FragmentInitialization(NativeBattleModulePoolTask Task, NativeBattleStaticMeshResource Mesh, NativeBattleThundaga105Bounds Bounds, NativeCrtRandomState Random);
public sealed record NativeBattleThundaga105FragmentStep(NativeBattleModulePoolTask Task, bool Complete);
public sealed record NativeBattleThundaga105FragmentEmission(NativeBattleEffectMemory Memory, NativeCrtRandomState Random, int Allocated);

public static class NativeBattleThundaga105Fragments
{
    public static NativeBattleThundaga105FragmentInitialization Initialize6D5990(NativeBattleModulePoolTask allocatedTask, NativeBattleThundagaFragmentPair pair, NativeBattleStaticMeshResource currentMesh, NativeBattleVector anchor, int ownerScale, NativeBattleThundaga105Bounds globalBounds, NativeCrtRandomState random)
    {
        RequireTask(allocatedTask);
        if (currentMesh.Address != pair.MutableMesh.Address || currentMesh.Bytes.IsDefault || currentMesh.Bytes.Length < 8 || pair.Reference.Bytes.IsDefault || pair.Reference.Bytes.Length < 8)
        {
            throw new InvalidDataException("Native Thundaga fragment requires its actual retained mutable mesh and reference owner.");
        }
        byte[] mesh = currentMesh.Bytes.ToArray();
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan(4));
        if (count < 1 || count > (mesh.Length - 8) / 8 || count > (pair.Reference.Bytes.Length - 8) / 8)
        {
            throw new InvalidDataException("Native Thundaga fragment vertex extent leaves its actual mesh owners.");
        }
        short scale = unchecked((short)ownerScale);
        NativeBattleActionMatrix matrix = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, 0, 0, 4096);
        short[] minimum = new short[3];
        short[] maximum = new short[3];
        for (int vertex = 0; vertex < count; vertex++)
        {
            NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(matrix, ReadVector(pair.Reference.Bytes.AsSpan(), 8 + vertex * 8));
            WriteVector(mesh, 8 + vertex * 8, transformed);
            short[] coordinates = [transformed.X, transformed.Y, transformed.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                minimum[axis] = vertex == 0 ? coordinates[axis] : Math.Min(minimum[axis], coordinates[axis]);
                maximum[axis] = vertex == 0 ? coordinates[axis] : Math.Max(maximum[axis], coordinates[axis]);
            }
        }
        short[] low = [globalBounds.Minimum.X, globalBounds.Minimum.Y, globalBounds.Minimum.Z];
        short[] high = [globalBounds.Maximum.X, globalBounds.Maximum.Y, globalBounds.Maximum.Z];
        short[] center = new short[3];
        for (int axis = 0; axis < 3; axis++)
        {
            center[axis] = unchecked((short)((minimum[axis] + maximum[axis]) >> 1));
            if (minimum[axis] < low[axis])
            {
                low[axis] = minimum[axis];
            }
            else if (maximum[axis] > high[axis])
            {
                high[axis] = maximum[axis];
            }
        }
        for (int vertex = 0; vertex < count; vertex++)
        {
            int offset = 8 + vertex * 8;
            for (int axis = 0; axis < 3; axis++)
            {
                Word(mesh, offset + axis * 2, Signed(mesh, offset + axis * 2) - center[axis]);
            }
        }
        NativeBattleModulePoolTask task = allocatedTask.ClearPayload(0x30);
        random = random.Draw(out ushort countdown);
        random = random.Draw(out ushort accelerationY);
        random = random.Draw(out ushort velocityY);
        task = task.WriteWord(0xC, (ushort)(8 + (countdown & 15))).WriteWord(0x10, unchecked((ushort)(anchor.X + center[0]))).WriteWord(0x12, unchecked((ushort)(anchor.Y + center[1]))).WriteWord(0x14, unchecked((ushort)(anchor.Z + center[2])));
        task = task.WriteWord(0x1A, unchecked((ushort)(-1000 - (velocityY & 1023)))).WriteWord(0x20, unchecked((ushort)(center[0] >> 5))).WriteWord(0x22, (ushort)(30 + (accelerationY & 31))).WriteWord(0x24, unchecked((ushort)(center[0] >> 5))).WriteDword(0x38, currentMesh.Address);
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw(out ushort angularVelocity);
            task = task.WriteWord(0x30 + axis * 2, unchecked((ushort)((angularVelocity & 127) - 64)));
        }
        return new(task, currentMesh with { Bytes = mesh.ToImmutableArray() }, new(new(low[0], low[1], low[2]), new(high[0], high[1], high[2])), random);
    }

    public static NativeBattleGfGeometryBatch Draw6D5AD0(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        RequireTask(task);
        RequireMesh(task, mesh);
        NativeBattleActionMatrix local = Rotation(task) with { X = S(task, 0x10), Y = S(task, 0x12), Z = S(task, 0x14) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        return NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, new(0, 0, 0, null, null, null, 17, 2), colors);
    }

    //Caller emits Draw6D5AD0 first, then this step; completion emits debris without integrating the fragment.
    public static NativeBattleThundaga105FragmentStep Step6D5AD0(NativeBattleModulePoolTask task)
    {
        RequireTask(task);
        task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) - 1)));
        if (task.ReadWord(0xC) == 0)
        {
            return new(task, true);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x10 + axis * 2, unchecked((ushort)(S(task, 0x10 + axis * 2) + (S(task, 0x18 + axis * 2) >> 4))));
            task = task.WriteWord(0x18 + axis * 2, unchecked((ushort)(S(task, 0x18 + axis * 2) + S(task, 0x20 + axis * 2))));
            task = task.WriteWord(0x28 + axis * 2, unchecked((ushort)(S(task, 0x28 + axis * 2) + S(task, 0x30 + axis * 2))));
        }
        return new(task, false);
    }

    public static NativeBattleThundaga105FragmentEmission EmitDebris6D5C00(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, NativeCrtRandomState random)
    {
        RequireTask(task);
        RequireMesh(task, mesh);
        NativeBattleActionMatrix rotation = Rotation(task);
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Read(0, 4)));
        Skip(2);
        int allocated = 0;
        for (int list = 0; list < 2; list++)
        {
            bool gouraud = list != 0;
            if (gouraud)
            {
                Skip(3);
            }
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            int length = gouraud ? 28 : 20;
            if (count < 0 || count > mesh.Bytes.Length / length)
            {
                throw new InvalidDataException("Native Thundaga fragment face list leaves its current mesh.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, length).ToArray();
                NativeBattleVector[] points = new NativeBattleVector[3];
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertexOffset = checked(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    if ((vertexOffset & 7) != 0)
                    {
                        throw new InvalidDataException("Native Thundaga fragment face requires its actual vertex-byte index.");
                    }
                    points[corner] = ReadVector(Read(8 + vertexOffset, 8), 0);
                }
                uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face);
                ImmutableArray<uint> colors = gouraud ? [firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(24))] : [firstColor, firstColor, firstColor];
                NativeBattleThundaga105Triangle triangle = new(points.ToImmutableArray(), [BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(16)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(10))], colors, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(18)));
                NativeBattleThundaga105DebrisSpawn spawned = NativeBattleThundaga105Debris.Spawn6D5CF0(memory, random, triangle, new(S(task, 0x10), S(task, 0x12), S(task, 0x14)), new(S(task, 0x18), S(task, 0x1A), S(task, 0x1C)), new(S(task, 0x20), S(task, 0x22), S(task, 0x24)), rotation);
                memory = spawned.Memory;
                random = spawned.Random;
                if (spawned.Row is not null)
                {
                    allocated++;
                }
                cursor = checked(cursor + length);
            }
        }
        return new(memory, random, allocated);

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (mesh.Bytes.IsDefault || offset < 0 || length < 0 || offset > mesh.Bytes.Length - length)
            {
                throw new InvalidDataException("Native Thundaga fragment access leaves its retained mesh.");
            }
            return mesh.Bytes.AsSpan(offset, length);
        }
        void Skip(int groups)
        {
            for (int group = 0; group < groups; group++)
            {
                if (BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4)) != 0)
                {
                    throw new NotSupportedException("Native Thundaga fragments require their original empty skipped face groups.");
                }
                cursor = checked(cursor + 4);
            }
        }
    }

    private static NativeBattleActionMatrix Rotation(NativeBattleModulePoolTask task) => NativeBattleGfMatrices.Euler56CD50(new(S(task, 0x28), S(task, 0x2A), S(task, 0x2C)));
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static void RequireTask(NativeBattleModulePoolTask task)
    {
        if (task.Callback != 0x6D5AD0 || task.PayloadLength != 0x30)
        {
            throw new InvalidDataException("Native Thundaga fragment requires its actual secondary task.");
        }
    }
    private static void RequireMesh(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh)
    {
        if (task.ReadDword(0x38) != mesh.Address)
        {
            throw new InvalidDataException("Native Thundaga fragment task selects another mutable mesh owner.");
        }
    }
    private static NativeBattleVector ReadVector(ReadOnlySpan<byte> bytes, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 2)..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 4)..]));
    private static short Signed(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void Word(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
    private static void WriteVector(byte[] bytes, int offset, NativeBattleVector point)
    {
        Word(bytes, offset, point.X);
        Word(bytes, offset + 2, point.Y);
        Word(bytes, offset + 4, point.Z);
    }
}
