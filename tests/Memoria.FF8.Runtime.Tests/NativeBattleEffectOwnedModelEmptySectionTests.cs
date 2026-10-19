using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectOwnedModelEmptySectionTests
{
    [Fact]
    public void Declared_empty_fourth_section_retains_end_pointer_without_adding_readable_bytes()
    {
        NativeBattleEffectOwnedModelResource resource = Resource();
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(Task(), 0x2000, resource, 3, 0x808080);
        Assert.Equal(0x1064u, initialized.Task.ReadDword(0x10C));
        Assert.Equal(100, resource.Bytes.Length);
        NativeBattleEffectOwnedModel.Validate(initialized.Task, initialized.State, resource);
        Assert.Throws<ArgumentOutOfRangeException>(() => resource.Bytes.Slice(100, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Only_the_actual_empty_fourth_section_may_use_the_declared_end(int invalid)
    {
        NativeBattleEffectOwnedModelResource resource = Resource();
        byte[] bytes = resource.Bytes.ToArray();
        if (invalid == 4)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 96);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + invalid * 4), 100);
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOwnedModel.Initialize8DD0F0(Task(), 0x2000, resource with { Bytes = bytes.ToImmutableArray() }, 3, 0x808080));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 1, new byte[0x140 - 12].ToImmutableArray());

    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] bytes = new byte[100];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] offsets = [24, 88, 96, 100, 100];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), offsets[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        NativeBattlePoseSource pose = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
        return new(0x1000, bytes.ToImmutableArray(), pose, []);
    }
}
