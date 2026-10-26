using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218ModelTests
{
    [Fact]
    public void Rotation_initialization_preserves_translation_and_matrix_padding()
    {
        NativeBattleEffectArena original = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(20, BitConverter.GetBytes(1234));
        NativeBattleEffectArena matrix = NativeBattleEffect218Model.InitializeRotation634040(original);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(matrix.Read(index * 2, 2))));
        Assert.Equal(1234, BinaryPrimitives.ReadInt32LittleEndian(matrix.Read(20, 4)));
        Assert.DoesNotContain(true, matrix.ReadSnapshot(18, 2).Known);
        Assert.DoesNotContain(true, matrix.ReadSnapshot(24, 8).Known);
    }

    [Fact]
    public void Model_construction_preserves_retained_pose_flags_and_descriptor_header()
    {
        byte[] bytes = new byte[0xBCC];
        int[] directory = [4, 0x18, 0x88, 0x73C, 0xBCC, 0xBCC];
        for (int index = 0; index < directory.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), directory[index]);
        }
        bytes[0x18] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x28), -1);
        NativeBattlePoseSource pose = new(bytes.AsSpan(0x18, 0x70).ToArray().ToImmutableArray(), bytes.AsSpan(0x73C).ToArray().ToImmutableArray());
        NativeBattleEffectOwnedModelResource resource = new(0xF1DD54, bytes.ToImmutableArray(), pose, []);
        NativeBattleEffectArena model = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x90).Write(0x6D, [8]);
        NativeBattleEffectArena descriptor = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x34).Write(0, [0xA5, 0x5A]);
        NativeBattleEffect218ModelBuild built = NativeBattleEffect218Model.Initialize633FB0(model, descriptor, 0x24F3338, resource, 16);
        Assert.Equal(new byte[] { 0xA5, 0x5A, 16, 0 }, built.Descriptor.Read(0, 4));
        Assert.Equal((byte)8, built.Model.Read(0x6D, 1)[0]);
        Assert.DoesNotContain(true, built.Model.ReadSnapshot(0x74, 4).Known);
        Assert.DoesNotContain(true, built.Descriptor.ReadSnapshot(0x18, 0x18).Known);
        Assert.Equal(0xF1DD6Cu, BinaryPrimitives.ReadUInt32LittleEndian(built.Descriptor.Read(12, 4)));
        Assert.Equal(0xF1E920u, BinaryPrimitives.ReadUInt32LittleEndian(built.Descriptor.Read(0x30, 4)));
    }
}
