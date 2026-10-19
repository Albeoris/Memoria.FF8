using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDeathResourcesReader
{
    public static NativeBattleDeathResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x162C95C, 0x3FCC);
        int[] sections = Enumerable.Range(0, 5).Select(index => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(4 + index * 4))).ToArray();
        if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan()) != 4 || !sections.SequenceEqual(new[] { 0x18, 0x568, 0x2E28, 0x3FCC, 0x3FCC }))
        {
            throw new InvalidDataException("Native Death requires its original four-section reaper container.");
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        NativeBattleEffectOwnedModelResource model = new(0x162C95C, bytes, pose, NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount));
        return new(new("FF8_EN:162B97C", NativeBattleEffectStaticResourcesReader.Read(memory, 0x162B97C, 0xFE0)), model, NativeBattleEffectStaticResourcesReader.Read(memory, 0x1801FD0, 0x49F4), NativeBattleEffectStaticResourcesReader.Read(memory, 0x18069C4, 0x25EC), Tim(0x1808FB0), Tim(0x18113D0), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x162B86Cu), NativeBattleEffectStaticResourcesReader.Read(memory, 0x1630928, 0x80), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x162B864, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x162B868, 4)));

        NativeBattleEffectHeapAllocation Tim(uint address)
        {
            ReadOnlySpan<byte> header = memory.Read(address, 12);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 16)
            {
                throw new InvalidDataException("Native Death requires its original complete TIM headers.");
            }
            int first = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
            int length = checked(8 + first);
            if ((BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) & 8) != 0)
            {
                length = checked(length + BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(address + (uint)length), 4)));
            }
            if (first < 12 || length is < 20 or > 0x100000)
            {
                throw new InvalidDataException("Native Death TIM blocks exceed their original allocation bounds.");
            }
            return new($"FF8_EN:{address:X}", NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDeathResources? nativeDeathResources;

    public NativeBattleDeathResources ReadNativeDeathResources()
    {
        return nativeDeathResources ??= NativeBattleDeathResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
