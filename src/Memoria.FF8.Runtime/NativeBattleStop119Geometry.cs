using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleStop119Geometry
{
    public const int ShardCount = 514;
    public const int ShardSize = 56;
    public const int ShardBytes = ShardCount * ShardSize;
    private static readonly int[] Records = [3, 5, 7, 9, 11, 13, 15];
    private static readonly int[] Models = [1, 2, 4, 5, 6, 7, 8];
    private static readonly int[] Counts = [29, 76, 232, 52, 30, 37, 58];
    private static readonly int[] Strides = [12, 12, 20, 24, 20, 24, 28, 36];

    public static int Group(int record) => Array.IndexOf(Records, record);
    public static int GeometryIndex(int group) => Models[group];
    public static int GroupCount(int group) => Counts[group];
    public static int GroupOffset(int group) => Counts.Take(group).Sum() * ShardSize;

    public static NativeBattleActionMatrix Rotation(NativeBattleGfAnimationNode node)
    {
        if ((node.Header & 0x400) != 0)
        {
            return NativeBattleGfMatrices.Euler701430(node.Vector16);
        }
        if ((node.Header & 0x20000) != 0)
        {
            NativeBattleVector angles = node.Vector16;
            NativeBattleActionMatrix x = NativeBattleGfMatrices.Euler701310(new(angles.X, 0, 0));
            NativeBattleActionMatrix y = NativeBattleGfMatrices.Euler701310(new(0, angles.Y, 0));
            NativeBattleActionMatrix z = NativeBattleGfMatrices.Euler701310(new(0, 0, angles.Z));
            return NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(x, z), y);
        }
        return NativeBattleGfMatrices.Euler701310(node.Vector16);
    }

    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        NativeBattleActionMatrix rotation = Rotation(node);
        (int x, int y, int z) = (node.Header & 0x200) != 0 ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
        NativeBattleActionMatrix matrix = (node.Header & 0x200) != 0 ? rotation : NativeBattleGfMatrices.Multiply56C090(context, rotation);
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(x + context.X), Y = unchecked(y + context.Y), Z = unchecked(z + context.Z) };
    }

    public static ImmutableArray<byte> Partition(ImmutableArray<byte> retained, int group, ImmutableArray<byte> source, int geometry, ImmutableArray<byte> selected, NativeBattleActionMatrix matrix, NativeBattleVector center, ref NativeCrtRandomState random)
    {
        Require(retained);
        byte[] shards = retained.ToArray();
        int output = GroupOffset(group);
        int cursor = geometry + Read32(source, geometry);
        int count = 0;
        for (int kind = 0; kind < 8; kind++)
        {
            int faces = Read32(source, cursor);
            cursor += 4;
            if (faces < 0 || faces > 4096 || faces != 0 && kind is 0 or 1 or 4 or 5)
            {
                throw new InvalidDataException("Native Stop clock pieces require their original textured polygon partition.");
            }
            for (int face = 0; face < faces; face++)
            {
                int corners = (kind & 1) == 0 ? 3 : 4;
                NativeBattleVector[] world = new NativeBattleVector[corners];
                for (int corner = 0; corner < corners; corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(cursor + 4 + corner * 2)) * 4;
                    world[corner] = NativeBattleGfMatrices.Transform460840(matrix, Vector(selected.AsSpan(), vertex));
                }
                NativeBattleVector position = new((short)(world.Sum(v => (int)v.X) / corners), (short)(world.Sum(v => (int)v.Y) / corners), (short)(world.Sum(v => (int)v.Z) / corners));
                if (++count > Counts[group])
                {
                    throw new InvalidDataException("Native Stop piece exceeds its assigned shard segment.");
                }
                Span<byte> shard = shards.AsSpan(output, ShardSize);
                Store(shard, 0, position);
                Word(shard, 6, corners == 4 ? 1 : 0);
                Store(shard, 8, new((short)((position.X - center.X) >> 4), (short)((position.Y - center.Y) >> 4), (short)((position.Z - center.Z) >> 4)));
                Word(shard, 14, 0);
                for (int corner = 0; corner < corners; corner++)
                {
                    Store(shard, 24 + corner * 8, new(unchecked((short)(world[corner].X - position.X)), unchecked((short)(world[corner].Y - position.Y)), unchecked((short)(world[corner].Z - position.Z))));
                }
                output += ShardSize;
                cursor += Strides[kind];
            }
        }
        if (count != Counts[group])
        {
            throw new InvalidDataException("Native Stop piece lacks its complete original polygon partition.");
        }
        for (int index = 0; index < count; index++)
        {
            Span<byte> shard = shards.AsSpan(GroupOffset(group) + index * ShardSize, ShardSize);
            for (int axis = 0; axis < 3; axis++)
            {
                random = random.Draw(out ushort value);
                Word(shard, 16 + axis * 2, (value - 16384) >> 8);
            }
            Word(shard, 22, 0);
        }
        return shards.ToImmutableArray();
    }

    public static ImmutableArray<byte> Advance(ImmutableArray<byte> retained)
    {
        Require(retained);
        byte[] shards = retained.ToArray();
        for (int index = 0; index < ShardCount; index++)
        {
            Span<byte> shard = shards.AsSpan(index * ShardSize, ShardSize);
            NativeBattleVector position = Vector(shard, 0);
            NativeBattleVector velocity = Vector(shard, 8);
            Store(shard, 0, new(unchecked((short)(position.X + velocity.X)), unchecked((short)(position.Y + velocity.Y)), unchecked((short)(position.Z + velocity.Z))));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(Vector(shard, 16));
            int corners = Signed(shard, 6) != 0 ? 4 : 3;
            for (int corner = 0; corner < corners; corner++)
            {
                Store(shard, 24 + corner * 8, NativeBattleGfMatrices.Transform460840(rotation, Vector(shard, 24 + corner * 8)));
            }
            Word(shard, 14, Math.Min(Signed(shard, 14) + 256, 4096));
        }
        return shards.ToImmutableArray();
    }

    public static NativeBattleGfGeometryBatch Draw(ImmutableArray<byte> shards, NativeBattleGfAnimationSource source, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null, Func<int, long>? readGroupGeneration = null)
    {
        Require(shards);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 7; group++)
        {
            int geometry = Read32(source.Data, 8 + Models[group] * 4);
            int cursor = geometry + Read32(source.Data, geometry);
            int shardIndex = GroupOffset(group);
            for (int kind = 0; kind < 8; kind++)
            {
                int count = Read32(source.Data, cursor);
                cursor += 4;
                if (count < 0 || count > 4096 || count != 0 && kind is 0 or 1 or 4 or 5)
                {
                    throw new InvalidDataException("Native Stop shard draw requires its original textured groups.");
                }
                for (int face = 0; face < count; face++)
                {
                    ReadOnlySpan<byte> shard = shards.AsSpan().Slice(shardIndex, ShardSize);
                    NativeBattleVector position = Vector(shard, 0);
                    int corners = (kind & 1) == 0 ? 3 : 4;
                    NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[corners];
                    System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[corners] : null;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        NativeBattleVector offset = Vector(shard, 24 + corner * 8);
                        NativeBattleVector point = new(unchecked((short)(offset.X + position.X)), unchecked((short)(offset.Y + position.Y)), unchecked((short)(offset.Z + position.Z)));
                        vertices[corner] = NativeBattleModuleProjection.Project45FE10(camera, point, projection);
                        if (world is not null)
                        {
                            world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(camera, point), projection);
                        }
                    }
                    int fade = Signed(shard, 14);
                    NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(source.Data.AsSpan().Slice(cursor, Strides[kind]), vertices, kind, new(0x30u | (fade != 0 ? 0xC0u : 0), 0, fade, null, null, 0, 0, 17, 2), colors, allocated);
                    if (pilotIdentity is { } identity && world is not null && attempt.Primitive is { } primitive)
                    {
                        attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + shardIndex / ShardSize), ResourceGeneration = readGroupGeneration?.Invoke(group) ?? identity.ResourceGeneration }, world[0], world[1], world[2], corners == 4 ? world[3] : default, checked((byte)corners)) } };
                    }
                    attempts.Add(attempt);
                    if (attempt.Primitive is not null)
                    {
                        allocated += attempt.PacketLength;
                    }
                    shardIndex += ShardSize;
                    cursor += Strides[kind];
                }
            }
            if (shardIndex != GroupOffset(group) + Counts[group] * ShardSize)
            {
                throw new InvalidDataException("Native Stop shard draw changed its original model owner.");
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static void Require(ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault || bytes.Length != ShardBytes)
        {
            throw new InvalidDataException("Native Stop requires its complete shared514-shard buffer.");
        }
    }
    private static int Read32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static short Signed(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
    private static NativeBattleVector Vector(ReadOnlySpan<byte> bytes, int offset) => new(Signed(bytes, offset), Signed(bytes, offset + 2), Signed(bytes, offset + 4));
    private static void Word(Span<byte> bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes[offset..], unchecked((short)value));
    private static void Store(Span<byte> bytes, int offset, NativeBattleVector value)
    {
        Word(bytes, offset, value.X);
        Word(bytes, offset + 2, value.Y);
        Word(bytes, offset + 4, value.Z);
    }
}
