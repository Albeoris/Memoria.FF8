using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105Triangle(ImmutableArray<NativeBattleVector> Vertices, ImmutableArray<ushort> Uv, ImmutableArray<uint> Colors, ushort TexturePage);
public sealed record NativeBattleThundaga105DebrisSpawn(NativeBattleEffectMemory Memory, NativeCrtRandomState Random, int? Row);
public sealed record NativeBattleThundaga105DebrisUpdate(NativeBattleEffectMemory Memory, int Survivors);

public static class NativeBattleThundaga105Debris
{
    public const int ArenaOffset = 0x2A24;
    public const int Capacity = 310;
    public const int Stride = 0x50;

    public static NativeBattleEffectMemory ClearAlive(NativeBattleEffectMemory memory)
    {
        using NativeBattleEffectArenaBatch arena = new(RequireArena(memory));
        for (int row = 0; row < Capacity; row++)
        {
            arena.Write(ArenaOffset + row * Stride + 6, new byte[2]);
        }
        return memory with { Arena = arena.Publish() };
    }

    public static NativeBattleThundaga105DebrisSpawn Spawn6D5CF0(NativeBattleEffectMemory memory, NativeCrtRandomState random, NativeBattleThundaga105Triangle triangle, NativeBattleVector position, NativeBattleVector velocity, NativeBattleVector acceleration, NativeBattleActionMatrix rotation)
    {
        NativeBattleEffectArena arena = RequireArena(memory);
        int free = 0;
        while (free < Capacity && ReadWord(arena, ArenaOffset + free * Stride + 6) != 0)
        {
            free++;
        }
        if (free == Capacity)
        {
            return new(memory, random, null);
        }
        if (triangle.Vertices.IsDefault || triangle.Vertices.Length != 3 || triangle.Uv.IsDefault || triangle.Uv.Length != 3 || triangle.Colors.IsDefault || triangle.Colors.Length != 3)
        {
            throw new InvalidDataException("Native Thundaga debris requires its actual triangle operands.");
        }
        NativeBattleVector[] points = triangle.Vertices.Select(point => NativeBattleGfMatrices.RotateShort56C4F0(rotation, point)).ToArray();
        NativeBattleVector center = new(unchecked((short)((points[0].X + points[1].X + points[2].X) / 3)), unchecked((short)((points[0].Y + points[1].Y + points[2].Y) / 3)), unchecked((short)((points[0].Z + points[1].Z + points[2].Z) / 3)));
        byte[] bank = new byte[Stride];
        int offset = 0;
        Vector(bank, offset, Add(position, center));
        Vector(bank, offset + 8, Add(velocity, new(unchecked((short)(center.X >> 1)), center.Y, unchecked((short)(center.Z >> 1)))));
        W(bank, offset + 0xE, triangle.TexturePage);
        Vector(bank, offset + 0x10, new(0, acceleration.Y, 0));
        W(bank, offset + 0x16, triangle.TexturePage);
        for (int corner = 0; corner < 3; corner++)
        {
            Vector(bank, offset + 0x18 + corner * 8, Subtract(points[corner], center));
            W(bank, offset + 0x1E + corner * 8, triangle.Uv[corner]);
            BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(offset + 0x30 + corner * 4), triangle.Colors[corner]);
        }
        bank[offset + 0x33] = 0x34;
        short[] angles = new short[3];
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw(out ushort value);
            angles[axis] = unchecked((short)((value - 16384) >> 7));
        }
        NativeBattleActionMatrix spin = NativeBattleGfMatrices.Euler56CD50(new(angles[0], angles[1], angles[2]));
        for (int index = 0; index < 9; index++)
        {
            W(bank, offset + 0x3C + index * 2, spin.Rotation[index]);
        }
        W(bank, offset + 6, 4096);
        random = random.Draw(out ushort decay);
        W(bank, offset + 0x4E, -100 - (decay & 127));
        return new(memory with { Arena = arena.Write(ArenaOffset + free * Stride, bank) }, random, free);
    }

    public static NativeBattleThundaga105DebrisUpdate Update6D40E0(NativeBattleEffectMemory memory)
    {
        using NativeBattleEffectArenaBatch arena = new(RequireArena(memory));
        int survivors = 0;
        for (int row = 0; row < Capacity; row++)
        {
            int offset = ArenaOffset + row * Stride;
            short oldScale = ReadWord(arena, offset + 6);
            if (oldScale == 0)
            {
                continue;
            }
            NativeBattleActionMatrix rotation = Rotation(arena.Read(offset + 0x3C, 18), 0);
            for (int corner = 0; corner < 3; corner++)
            {
                int vertexOffset = offset + 0x18 + corner * 8;
                NativeBattleVector point = V(arena.Read(vertexOffset, 6), 0);
                byte[] changed = new byte[6];
                Vector(changed, 0, NativeBattleGfMatrices.Transform460840(rotation, point));
                arena.Write(vertexOffset, changed);
            }
            short scale = unchecked((short)(oldScale + ReadWord(arena, offset + 0x4E)));
            byte[] scaleBytes = new byte[2];
            W(scaleBytes, 0, scale <= 1024 ? 0 : scale);
            arena.Write(offset + 6, scaleBytes);
            if (scale <= 1024)
            {
                continue;
            }
            byte[] position = arena.Read(offset, 6);
            byte[] velocity = arena.Read(offset + 8, 6);
            byte[] acceleration = arena.Read(offset + 0x10, 6);
            for (int axis = 0; axis < 3; axis++)
            {
                W(position, axis * 2, S(position, axis * 2) + (S(velocity, axis * 2) >> 4));
            }
            if (S(position, 2) >= 0)
            {
                W(velocity, 2, -S(velocity, 2));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                W(velocity, axis * 2, S(velocity, axis * 2) + S(acceleration, axis * 2));
            }
            arena.Write(offset, position);
            arena.Write(offset + 8, velocity);
            survivors++;
        }
        return new(memory with { Arena = arena.Publish() }, survivors);
    }

    public static NativeBattleGfGeometryBatch Draw6D41E0(NativeBattleEffectMemory memory, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleEffectArena arena = RequireArena(memory);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int row = 0; row < Capacity; row++)
        {
            int rowOffset = ArenaOffset + row * Stride;
            short scale = ReadWord(arena, rowOffset + 6);
            if (scale == 0)
            {
                continue;
            }
            byte[] bank = new byte[Stride];
            arena.Read(rowOffset, 6).CopyTo(bank, 0);
            arena.Read(rowOffset + 0xE, 2).CopyTo(bank, 0xE);
            arena.Read(rowOffset + 0x16, 2).CopyTo(bank, 0x16);
            arena.Read(rowOffset + 0x18, 24).CopyTo(bank, 0x18);
            arena.Read(rowOffset + 0x30, 12).CopyTo(bank, 0x30);
            int offset = 0;
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, 0, 0, 4096));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, V(bank, offset));
            matrix = matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
            NativeBattleModuleProjectedVertex[] vertices = Enumerable.Range(0, 3).Select(corner => NativeBattleModuleProjection.Project45FE10(matrix, V(bank, offset + 0x18 + corner * 8), projection)).ToArray();
            SkinWrites packet = new(40, allocated);
            packet.Word(0, 0x09000000);
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Word(4 + corner * 12, BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan(offset + 0x30 + corner * 4)));
                packet.Word(8 + corner * 12, vertices[corner].Screen);
                packet.Word16(12 + corner * 12, unchecked((ushort)S(bank, offset + 0x1E + corner * 8)));
            }
            packet.Word16(14, unchecked((ushort)S(bank, offset + 0xE)));
            packet.Word16(26, unchecked((ushort)S(bank, offset + 0x16)));
            int depth = unchecked((vertices[0].Depth + vertices[1].Depth + vertices[2].Depth) * 0x155) >> 12;
            if (depth <= 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            //The fourth payload component is unconsumed canonical padding for this triangle caller.
            NativeBattleGeometryPrimitive primitive = new(17 + (depth >> 2), packet.Words(), [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0]);
            attempts.Add(packet.Attempt(primitive));
            allocated = checked(allocated + 40);
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static NativeBattleEffectArena RequireArena(NativeBattleEffectMemory memory) => memory.Arena ?? throw new NotSupportedException("Native Thundaga debris requires its actual arena owner.");
    private static short ReadWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static short ReadWord(NativeBattleEffectArenaBatch arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static short S(byte[] data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
    private static void W(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), unchecked((short)value));
    private static NativeBattleVector V(byte[] data, int offset) => new(S(data, offset), S(data, offset + 2), S(data, offset + 4));
    private static void Vector(byte[] data, int offset, NativeBattleVector value)
    {
        W(data, offset, value.X);
        W(data, offset + 2, value.Y);
        W(data, offset + 4, value.Z);
    }
    private static NativeBattleVector Add(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
    private static NativeBattleVector Subtract(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X - second.X)), unchecked((short)(first.Y - second.Y)), unchecked((short)(first.Z - second.Z)));
    private static NativeBattleActionMatrix Rotation(byte[] bank, int offset) => new(Enumerable.Range(0, 9).Select(index => S(bank, offset + index * 2)).ToImmutableArray(), 0, 0, 0, 4096);
}
