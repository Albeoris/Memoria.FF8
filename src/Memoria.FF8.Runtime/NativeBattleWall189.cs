using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWall189Resources(NativeBattleModuleResource Spark, NativeBattleStaticMeshResource Burst, NativeBattleStaticMeshResource Debris, NativeBattleStaticMeshResource Shield, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> ClearPacket, ImmutableArray<int> Selectors);

public sealed record NativeBattleWall189State([property: JsonRequired] ImmutableArray<byte> Sprites, [property: JsonRequired] ImmutableArray<byte> Meshes, [property: JsonRequired] ImmutableArray<byte> Environment, [property: JsonRequired] ImmutableArray<byte> Area, [property: JsonRequired] ImmutableArray<byte> Offset, [property: JsonRequired] ImmutableArray<byte> ClearPacket, [property: JsonRequired] short CaptureX, [property: JsonRequired] short CaptureY, [property: JsonRequired] NativeBattleEffectArena Packets)
{
    public static NativeBattleWall189State FromColdProcess() => new(new byte[150 * 24].ToImmutableArray(), new byte[100 * 24].ToImmutableArray(), new byte[64].ToImmutableArray(), new byte[12].ToImmutableArray(), new byte[8].ToImmutableArray(), new uint[] { 0x05000000, 0x28000000, 0, 256, 256u << 16, 256u << 16 | 256 }.SelectMany(value => BitConverter.GetBytes(value)).ToImmutableArray(), 0, 0, NativeBattleEffectArena.Clear());

    public void Validate()
    {
        if (Sprites.IsDefault || Sprites.Length != 150 * 24 || Meshes.IsDefault || Meshes.Length != 100 * 24 || Environment.IsDefault || Environment.Length != 64 || Area.IsDefault || Area.Length != 12 || Offset.IsDefault || Offset.Length != 8 || ClearPacket.IsDefault || ClearPacket.Length != 24 || Packets is null)
        {
            throw new InvalidDataException("Native Wall requires its complete retained particle, environment and packet banks.");
        }
        Packets.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWall189State? Wall189
    {
        get => effectState.Wall189;
        init => effectState = effectState with { Wall189 = value };
    }
}

public static class NativeBattleWall189Motion
{
    public static NativeBattleCameraProjection Capture(NativeBattleVector center)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, 0x1000000 / (0x1162000 / 256), 4096, 0x1000000 / (0xAF0000 / 256));
        matrix = matrix with { X = -1 - center.X / 18, Y = center.Z / 4 / 2 + 1, Z = 32767 };
        return new(matrix, 32767, 128, 128);
    }

    public static ImmutableArray<byte> ClearFlags(ImmutableArray<byte> source, int count)
    {
        if (source.IsDefault || source.Length != count * 24)
        {
            throw new InvalidDataException("Native Wall requires its actual static particle row bank.");
        }
        byte[] bytes = source.ToArray();
        for (int row = 0; row < count; row++)
        {
            bytes.AsSpan(row * 24, 4).Clear();
        }
        return bytes.ToImmutableArray();
    }

    public static ImmutableArray<byte> Mesh(ImmutableArray<byte> source)
    {
        byte[] bytes = Require(source);
        short age = Word(4);
        age = unchecked((short)(age + 1));
        Store(4, age);
        if (age >= 16)
        {
            bytes.AsSpan(0, 4).Clear();
        }
        else
        {
            if (age >= 12)
            {
                Store(6, unchecked((short)(Word(6) - (Word(6) >> 3))));
            }
            Store(10, unchecked((short)(Word(10) + Word(14))));
            Store(14, unchecked((short)(Word(14) - (Word(14) >> 3))));
            Store(18, unchecked((short)(Word(18) + Word(16))));
            Store(22, unchecked((short)(Word(22) + Word(20))));
        }
        return bytes.ToImmutableArray();

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
        void Store(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    }

    public static ImmutableArray<byte> Sprite(ImmutableArray<byte> source, bool ended)
    {
        byte[] bytes = Require(source);
        Store(4, unchecked((short)(Word(4) + 1)));
        if (ended)
        {
            bytes.AsSpan(0, 4).Clear();
        }
        else
        {
            Store(8, unchecked((short)(Word(8) + Word(16))));
            Store(12, unchecked((short)(Word(12) + Word(20))));
            Store(16, unchecked((short)(Word(16) - (Word(16) >> 4))));
            Store(20, unchecked((short)(Word(20) - (Word(20) >> 4))));
        }
        return bytes.ToImmutableArray();

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
        void Store(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    }

    private static byte[] Require(ImmutableArray<byte> source)
    {
        if (source.IsDefault || source.Length != 24)
        {
            throw new InvalidDataException("Native Wall particle requires its exact current twenty-four-byte row.");
        }
        return source.ToArray();
    }
}
