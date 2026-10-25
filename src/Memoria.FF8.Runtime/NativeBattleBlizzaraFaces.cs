using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzaraFaceInitialization(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task);
public sealed record NativeBattleBlizzaraFaceStep(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeCrtRandomState Random);

/// <summary>Blizzara's three mutable mesh pairs,223 retained face rows and669 independent task-owned vertices.</summary>
public static class NativeBattleBlizzaraFaces
{
    public const int FaceCount = 223;
    public const int ExpandedOffset = 0x3EE4;
    public const int ExpandedLength = FaceCount * 3 * 8;
    public const int PrimaryMeshLength = 0x1CBC;
    public const int PartnerMeshLength = 0x1194;

    public static int PrimaryMeshOffset(int sequence) => sequence switch
    {
        0 => 0x10BE4,
        1 => 0x13A34,
        2 => 0x16884,
        _ => throw new NotSupportedException("Native Blizzara owns exactly three primary mesh clones.")
    };

    public static int PartnerMeshOffset(int sequence) => sequence switch
    {
        0 => 0x128A0,
        1 => 0x156F0,
        2 => 0x18540,
        _ => throw new NotSupportedException("Native Blizzara owns exactly three companion mesh clones.")
    };

    public static ImmutableArray<byte> ReadPrimaryMesh(NativeBattleEffectMemory memory, int sequence) => RequireArena(memory).Read(PrimaryMeshOffset(sequence), PrimaryMeshLength).ToImmutableArray();

    public static ImmutableArray<byte> ExpandedVertices(NativeBattleModulePoolTask task)
    {
        RequireTask(task);
        return task.Payload.AsSpan().Slice(ExpandedOffset - 12, ExpandedLength).ToArray().ToImmutableArray();
    }

    public static NativeBattleBlizzaraFaceInitialization Initialize(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, int sequence, NativeBattleBlizzaraResources resources)
    {
        RequireTask(task);
        if (resources.PrimaryMesh.Bytes.Length != PrimaryMeshLength || resources.PartnerMesh.Bytes.Length != PartnerMeshLength)
        {
            throw new InvalidDataException("Native Blizzara requires its original complete mesh pair.");
        }
        byte[] primary = resources.PrimaryMesh.Bytes.ToArray();
        byte[] partner = resources.PartnerMesh.Bytes.ToArray();
        byte[] payload = task.Payload.ToArray();
        (int first, int count) = NativeBattleIceMeshVertices.Group(primary, 6);
        (int second, int partnerCount) = NativeBattleIceMeshVertices.Group(partner, 2);
        if (count != FaceCount || partnerCount != FaceCount || BinaryPrimitives.ReadUInt32LittleEndian(primary) != 0x438 || BinaryPrimitives.ReadUInt32LittleEndian(primary.AsSpan(4)) != 134)
        {
            throw new InvalidDataException("Native Blizzara's primary/companion face and vertex owners disagree.");
        }
        for (int index = 0; index < FaceCount; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0x32 + index * 0x48 - 12), 0);
            int face = first + index * 28;
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(primary.AsSpan(face)) & 0xFDFFFFFF;
            BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(face), command);
            primary[face + 0x17] = 0;
            primary[face + 0x1B] = 0;
            partner.AsSpan(second + index * 20, 3).Fill(255);
        }
        NativeBattleIceMeshVertices.ExpandGt3(primary, resources.PrimaryMesh.Bytes.AsSpan().Slice(8, 134 * 8), payload.AsSpan(ExpandedOffset - 12, ExpandedLength));
        NativeBattleEffectArena arena = RequireArena(memory).Write(PrimaryMeshOffset(sequence), primary).Write(PartnerMeshOffset(sequence), partner);
        return new(memory with { Arena = arena }, task with { Payload = payload.ToImmutableArray() });
    }

    public static NativeBattleBlizzaraFaceStep Advance(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, int sequence, int phase, NativeCrtRandomState random)
    {
        RequireTask(task);
        if (phase is < 30 or > 70)
        {
            throw new ArgumentOutOfRangeException(nameof(phase), "Native Blizzara face stepping is reached only during phases30..70.");
        }
        NativeBattleEffectArena arena = RequireArena(memory);
        byte[] primary = arena.Read(PrimaryMeshOffset(sequence), PrimaryMeshLength);
        byte[] partner = arena.Read(PartnerMeshOffset(sequence), PartnerMeshLength);
        byte[] payload = task.Payload.ToArray();
        (int first, int count) = NativeBattleIceMeshVertices.Group(primary, 6);
        (int second, int partnerCount) = NativeBattleIceMeshVertices.Group(partner, 2);
        if (count != FaceCount || partnerCount != FaceCount)
        {
            throw new InvalidDataException("Native Blizzara face stepping lost its initialized mesh clones.");
        }
        if (phase <= 50)
        {
            int threshold = 170 * (phase - 30) - 3400;
            for (int index = 0; index < FaceCount; index++)
            {
                int row = 0x2C + index * 0x48;
                if (Word(row + 6) != 0)
                {
                    continue;
                }
                int face = first + index * 28;
                int[] corners = Corners(face);
                if (corners.Any(vertex => Word(vertex + 2) > threshold))
                {
                    continue;
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    Set(row + axis * 2, (Word(corners[0] + axis * 2) + Word(corners[1] + axis * 2) + Word(corners[2] + axis * 2)) / 3);
                }
                Set(row + 6, 1);
                Set(row + 8, unchecked(-Word(row + 2) * Word(row)) >> 15);
                Set(row + 10, 0);
                Set(row + 12, unchecked(-Word(row + 4) * Word(row + 2)) >> 15);
                for (int axis = 0; axis < 3; axis++)
                {
                    Set(row + 0x10 + axis * 2, 0);
                    random = random.Draw(out ushort value);
                    Set(row + 0x18 + axis * 2, (value - 16384) >> 7);
                }
                uint command = BinaryPrimitives.ReadUInt32LittleEndian(primary.AsSpan(face)) | 0x02000000;
                BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(face), command);
                primary[face + 0x17] = 255;
                primary[face + 0x1B] = 0;
                for (int corner = 0; corner < 3; corner++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Set(row + 0x20 + corner * 8 + axis * 2, Word(corners[corner] + axis * 2) - Word(row + axis * 2));
                    }
                    int color = face + (corner == 0 ? 0 : corner == 1 ? 0x14 : 0x18);
                    for (int component = 0; component < 3; component++)
                    {
                        payload[row + 0x38 + corner * 4 + component - 12] = (byte)((primary[color + component] + 15) >> 3);
                    }
                }
                for (int component = 0; component < 3; component++)
                {
                    payload[row + 0x44 + component - 12] = (byte)((partner[second + index * 20 + component] + 15) >> 2);
                }
            }
        }
        for (int index = 0; index < FaceCount; index++)
        {
            int row = 0x2C + index * 0x48;
            if (Word(row + 6) == 0)
            {
                continue;
            }
            int face = first + index * 28;
            bool zero = Fade(primary, face, row + 0x38);
            if (zero)
            {
                zero = Fade(primary, face + 0x14, row + 0x3C);
                if (zero)
                {
                    zero = Fade(primary, face + 0x18, row + 0x40);
                }
            }
            if (zero)
            {
                Set(row + 6, 0);
                continue;
            }
            _ = Fade(partner, second + index * 20, row + 0x44);
            primary[face + 0x17] = (byte)Math.Max(0, primary[face + 0x17] - 32);
            Set(row + 10, Word(row + 10) - 5);
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
        arena = arena.Write(PrimaryMeshOffset(sequence), primary).Write(PartnerMeshOffset(sequence), partner);
        return new(memory with { Arena = arena }, task with { Payload = payload.ToImmutableArray() }, random);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset - 12));
        void Set(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset - 12), unchecked((ushort)value));
        int[] Corners(int face) => Enumerable.Range(0, 3).Select(corner => checked(ExpandedOffset + BinaryPrimitives.ReadUInt16LittleEndian(primary.AsSpan(face + 4 + corner * 2)) * 4)).ToArray();
        bool Fade(byte[] mesh, int color, int steps)
        {
            bool zero = true;
            for (int component = 0; component < 3; component++)
            {
                byte value = (byte)Math.Max(0, mesh[color + component] - payload[steps + component - 12]);
                mesh[color + component] = value;
                zero &= value == 0;
            }
            return zero;
        }
    }

    private static void RequireTask(NativeBattleModulePoolTask task)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 0x53CC - 12)
        {
            throw new InvalidDataException("Native Blizzara requires its complete physical53CC-byte primary task.");
        }
    }

    private static NativeBattleEffectArena RequireArena(NativeBattleEffectMemory memory) => memory.Arena ?? throw new NotSupportedException("Native ice mesh clones require the actual initialized effect arena.");
}
