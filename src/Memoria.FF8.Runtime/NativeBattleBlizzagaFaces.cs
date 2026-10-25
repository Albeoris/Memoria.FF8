using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? BlizzagaCompanion { get; init; }
}

public sealed record NativeBattleBlizzagaFaceInitialization(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task);
public sealed record NativeBattleBlizzagaFaceStep(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeCrtRandomState Random);

/// <summary>Blizzaga's physical morph/expanded bank, mutable geometry clones and shared companion indices.</summary>
public static class NativeBattleBlizzagaFaces
{
    public const int FaceCount = 320;
    public const int VertexOffset = 0x4628;
    public const int ExpandedLength = FaceCount * 3 * 8;
    public const int MeshLength = 0x2328;

    public static int MeshOffset(int sequence) => sequence is >= 0 and < 3 ? 0x11FB0 + sequence * MeshLength : throw new NotSupportedException("Native Blizzaga owns exactly three header-indexed geometry clones.");
    public static ImmutableArray<byte> ReadPrimaryMesh(NativeBattleEffectMemory memory, int sequence) => Arena(memory).Read(MeshOffset(sequence), MeshLength).ToImmutableArray();
    public static ImmutableArray<byte> Companion(NativeBattleEffectMemory memory) => memory.BlizzagaCompanion is { } bytes && !bytes.IsDefault && bytes.Length == 0x1928 ? bytes : throw new NotSupportedException("Native Blizzaga requires its retained shared companion geometry.");

    public static ImmutableArray<byte> ExpandedVertices(NativeBattleModulePoolTask task, int phase)
    {
        RequireTask(task);
        return task.Payload.AsSpan().Slice(VertexOffset - 12, phase <= 32 ? 177 * 8 : ExpandedLength).ToArray().ToImmutableArray();
    }

    public static (short Minimum, short Span) Extents(NativeBattleBlizzagaResources resources)
    {
        resources.ReferenceVertices.Validate();
        ReadOnlySpan<byte> vertices = resources.ReferenceVertices.Bytes.AsSpan();
        short minimum = BinaryPrimitives.ReadInt16LittleEndian(vertices[12..]);
        short maximum = minimum;
        for (int index = 1; index < 177; index++)
        {
            short value = BinaryPrimitives.ReadInt16LittleEndian(vertices[(10 + index * 8)..]);
            if (value < minimum)
            {
                minimum = value;
            }
            else if (value > maximum)
            {
                maximum = value;
            }
        }
        short difference = unchecked((short)(maximum - minimum));
        return (minimum, unchecked((short)(difference + (difference >> 4))));
    }

    public static NativeBattleBlizzagaFaceInitialization Initialize(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, int sequence, NativeBattleBlizzagaResources resources)
    {
        RequireTask(task);
        resources.PrimaryMesh.Validate(6, 28);
        resources.PartnerMesh.Validate(2, 20);
        byte[] mesh = resources.PrimaryMesh.Bytes.ToArray();
        (int first, _) = NativeBattleIceMeshVertices.Group(mesh, 6);
        for (int face = 0; face < FaceCount; face++)
        {
            mesh[first + face * 28 + 0x17] = 0;
            mesh[first + face * 28 + 0x1B] = 64;
        }
        return new(memory with { Arena = Arena(memory).Write(MeshOffset(sequence), mesh), BlizzagaCompanion = memory.BlizzagaCompanion ?? resources.PartnerMesh.Bytes }, task);
    }

    public static NativeBattleBlizzagaFaceStep Step(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, int sequence, int phase, short minimum, short span, NativeCrtRandomState random, NativeBattleBlizzagaResources resources)
    {
        RequireTask(task);
        if (phase is < 28 or > 97)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (phase <= 30)
        {
            ImmutableArray<byte> morphed = NativeBattleGfMorphGeometry.Morph45E9D0(resources.MorphSourceVertices.Bytes, 8, resources.ReferenceVertices.Bytes, 8, 177, 682 + (phase - 28) * 1365, task.Payload, VertexOffset - 12);
            return new(memory, task with { Payload = morphed }, random);
        }
        byte[] payload = task.Payload.ToArray();
        if (phase == 31)
        {
            resources.ReferenceVertices.Bytes.AsSpan().Slice(8, 177 * 8).CopyTo(payload.AsSpan(VertexOffset - 12));
        }
        if (phase <= 32)
        {
            return new(memory, task with { Payload = payload.ToImmutableArray() }, random);
        }
        byte[] mesh = ReadPrimaryMesh(memory, sequence).ToArray();
        byte[] companion = Companion(memory).ToArray();
        (int first, int count) = NativeBattleIceMeshVertices.Group(mesh, 6);
        (int second, int companionCount) = NativeBattleIceMeshVertices.Group(companion, 2);
        if (count != FaceCount || companionCount != FaceCount)
        {
            throw new InvalidDataException("Native Blizzaga lost its complete320-face geometry pair.");
        }
        if (phase == 33)
        {
            NativeBattleIceMeshVertices.ExpandGt3(mesh, resources.ReferenceVertices.Bytes.AsSpan()[8..], payload.AsSpan(VertexOffset - 12, ExpandedLength));
            for (int index = 0; index < FaceCount; index++)
            {
                int face = first + index * 28;
                Set(0x28 + index * 0x38 + 6, 0);
                Set(0x28 + index * 0x38 + 14, 1);
                BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(face), BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan(face)) & 0xFDFFFFFF);
                mesh.AsSpan(face + 4, 6).CopyTo(companion.AsSpan(second + index * 20 + 4));
            }
        }
        if (phase >= 51)
        {
            int threshold = unchecked(minimum + (int)((uint)unchecked((phase - 51) * span) >> 5));
            for (int index = 0; index < FaceCount; index++)
            {
                int row = 0x28 + index * 0x38;
                if (Word(row + 6) != 0)
                {
                    continue;
                }
                int face = first + index * 28;
                int[] corners = Corners(face);
                if (phase != 82 && !corners.Any(corner => Word(corner + 2) <= threshold))
                {
                    continue;
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    Set(row + axis * 2, (Word(corners[0] + axis * 2) + Word(corners[1] + axis * 2) + Word(corners[2] + axis * 2)) / 3);
                }
                Set(row + 6, 1);
                Set(row + 8, unchecked(-Word(row + 2) * Word(row)) >> 15);
                Set(row + 10, Word(row + 2) >> 4);
                Set(row + 12, 0);
                for (int axis = 0; axis < 3; axis++)
                {
                    Set(row + 0x10 + axis * 2, 0);
                    random = random.Draw(out ushort value);
                    Set(row + 0x18 + axis * 2, (value - 16384) >> 7);
                }
                random = random.Draw(out ushort decay);
                Set(row + 0x36, 8 - (decay & 3));
                BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(face), BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan(face)) | 0x02000000);
                mesh[face + 0x17] = 128;
                mesh[face + 0x1B] = 0;
                for (int corner = 0; corner < 3; corner++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Set(row + 0x20 + corner * 8 + axis * 2, Word(corners[corner] + axis * 2) - Word(row + axis * 2));
                    }
                }
            }
        }
        for (int index = 0; index < FaceCount; index++)
        {
            int row = 0x28 + index * 0x38;
            if (Word(row + 6) == 0 || Word(row + 14) == 0)
            {
                continue;
            }
            int face = first + index * 28;
            bool allZero = true;
            for (int corner = 0; corner < 3; corner++)
            {
                int color = face + (corner == 0 ? 0 : corner == 1 ? 0x14 : 0x18);
                for (int component = 0; component < 3; component++)
                {
                    int next = mesh[color + component] - Word(row + 0x36);
                    mesh[color + component] = next <= 0 ? (byte)0 : unchecked((byte)next);
                    allZero &= next <= 0;
                }
            }
            if (allZero)
            {
                Set(row + 14, 0);
                continue;
            }
            Set(row + 12, Word(row + 12) + 8);
            for (int axis = 0; axis < 3; axis++)
            {
                Set(row + axis * 2, Word(row + axis * 2) + Word(row + 8 + axis * 2));
                Set(row + 0x10 + axis * 2, Word(row + 0x10 + axis * 2) + Word(row + 0x18 + axis * 2));
            }
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(Word(row + 0x10), Word(row + 0x12), Word(row + 0x14))) with { X = Word(row), Y = Word(row + 2), Z = Word(row + 4) };
            int[] selected = Corners(face);
            for (int corner = 0; corner < 3; corner++)
            {
                int local = row + 0x20 + corner * 8;
                NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(matrix, new(Word(local), Word(local + 2), Word(local + 4)));
                Set(selected[corner], transformed.X);
                Set(selected[corner] + 2, transformed.Y);
                Set(selected[corner] + 4, transformed.Z);
            }
        }
        return new(memory with { Arena = Arena(memory).Write(MeshOffset(sequence), mesh), BlizzagaCompanion = companion.ToImmutableArray() }, task with { Payload = payload.ToImmutableArray() }, random);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset - 12));
        void Set(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset - 12), unchecked((ushort)value));
        int[] Corners(int face) => Enumerable.Range(0, 3).Select(corner => checked(VertexOffset + BinaryPrimitives.ReadUInt16LittleEndian(mesh.AsSpan(face + 4 + corner * 2)) * 4)).ToArray();
    }

    private static void RequireTask(NativeBattleModulePoolTask task)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 0x6428 - 12)
        {
            throw new InvalidDataException("Native Blizzaga requires its complete physical6428-byte primary task.");
        }
    }

    private static NativeBattleEffectArena Arena(NativeBattleEffectMemory memory) => memory.Arena ?? throw new NotSupportedException("Native Blizzaga requires the actual initialized effect arena.");
}
