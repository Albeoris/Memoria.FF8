using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278CreatureTests
{
    [Fact]
    public void Constructor_writes_only_original_fields_and_absolute_section_pointers()
    {
        NativeBattleEffectOwnedModelResource resource = Resource();
        NativeBattleModulePoolTask source = new(1, 7, null, 0x681630, Enumerable.Repeat((byte)0xA5, 0x5E0 - 12).ToImmutableArray());
        NativeBattleModulePoolTask task = NativeBattleCarbuncle278Creature.Initialize6EC060(source, 0x20E235C, resource);
        Assert.Equal((ushort)2, task.ReadWord(0x20));
        Assert.Equal(uint.MaxValue, task.ReadDword(0x9C));
        Assert.Equal(0u, task.ReadDword(0xAC));
        Assert.Equal(0x20E2418u, task.ReadDword(0x84));
        Assert.Equal(task.ReadDword(0x84), task.ReadDword(0x88));
        Assert.Equal(new uint[] { 0x10B6FAC, 0x10B755C, 0x10BBAD4, 0x10BCE98 }, Enumerable.Range(0, 4).Select(index => task.ReadDword(0xBC + index * 4)));
        Assert.Equal(0x808080u, task.ReadDword(0x48));
        Assert.Equal((byte)0xA5, task.ReadByte(0x82));
        Assert.Equal((byte)0xA5, task.ReadByte(0x8C));
        Assert.Equal((byte)0, task.ReadByte(0x8D));
        Assert.Equal((byte)0xA5, task.ReadByte(0x8E));
        Assert.Equal((byte)0xA5, source.ReadByte(0x8D));
    }

    [Fact]
    public void Executable_pose_preserves_padding_unknown_masks_and_independent_restore()
    {
        NativeBattleCarbuncleExecutableMemory source = NativeBattleCarbuncleExecutableMemory.FromOriginal(Resource());
        NativeBattleDecodedPose pose = new(new(10, -20, 30), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 0, 0), new(1024, 1024, 1024))], false);
        NativeBattleCarbuncleExecutableMemory result = source.SelectFirstPose(new(pose, 1, 2));
        Assert.Equal((short)-20, BinaryPrimitives.ReadInt16LittleEndian(result.Read(NativeBattleCarbuncleExecutableMemory.Address + 10, 2)));
        Assert.Equal(new byte[] { 0, 0 }, result.Read(NativeBattleCarbuncleExecutableMemory.Address + 50, 2));
        Assert.Equal(new byte[] { 0, 0 }, result.Read(NativeBattleCarbuncleExecutableMemory.Address + 98, 2));
        string json = JsonSerializer.Serialize(result);
        NativeBattleCarbuncleExecutableMemory restored = JsonSerializer.Deserialize<NativeBattleCarbuncleExecutableMemory>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(new byte[] { 0, 0 }, source.Read(NativeBattleCarbuncleExecutableMemory.Address + 10, 2));
    }

    [Fact]
    public void Executable_reads_and_writes_cannot_borrow_adjacent_original_regions()
    {
        NativeBattleCarbuncleExecutableMemory source = NativeBattleCarbuncleExecutableMemory.FromOriginal(Resource());
        Assert.Throws<InvalidDataException>(() => source.Read(NativeBattleCarbuncleExecutableMemory.Address - 1, 1));
        Assert.Throws<InvalidDataException>(() => source.Write(NativeBattleCarbuncleExecutableMemory.Address + NativeBattleCarbuncleExecutableMemory.Length, [1]));
        Assert.Throws<InvalidDataException>(() => source.Read(NativeBattleCarbuncleExecutableMemory.Address, -1));
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().CarbuncleExecutableMemory);
        Assert.Throws<InvalidDataException>(() => source.SelectFirstPose(new(new(new(0, 0, 0), [], false), 0, 0)));
    }

    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] bytes = new byte[0x5F04];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] sections = [0x18, 0x5C8, 0x4B40, 0x5F04, 0x5F04];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        bytes[0x18] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x1A), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x28), -1);
        bytes[0x4A] = 0xCC;
        bytes[0x4B] = 0xCC;
        return new(0x10B6F94, bytes.ToImmutableArray(), new([], []), []);
    }
}
