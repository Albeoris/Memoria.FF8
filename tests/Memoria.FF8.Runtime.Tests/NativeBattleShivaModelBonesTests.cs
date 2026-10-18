using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaModelBonesTests
{
    [Fact]
    public void Raw_model_samples_its_matrix_and_current_pose_with_MAC_word_wrap()
    {
        var fixture = Fixture();
        Assert.Equal(new NativeBattleVector(-25536, 0, 0), NativeBattleShivaModelBones.Read502170(fixture.Model, fixture.Resource, 0, 6144));
        NativeBattleCactuarRawModelState advanced = fixture.Model with { DecodedPose = fixture.Model.DecodedPose! with { Translation = new(10, 0, 0) } };
        Assert.Equal(new NativeBattleVector(-25376, 0, 0), NativeBattleShivaModelBones.Read502170(advanced, fixture.Resource, 0, 6144));
    }

    [Fact]
    public void Fractional_bone_distance_is_stored_as_a_signed_word_before_transforming()
    {
        var fixture = Fixture();
        Assert.Equal(new NativeBattleVector(-25536, 0, -15386), NativeBattleShivaModelBones.Read502170(fixture.Model, fixture.Resource, 1, 6144));
        Assert.Equal(new NativeBattleVector(-25536, 0, 1000), NativeBattleShivaModelBones.Read502170(fixture.Model, fixture.Resource, 1, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaModelBones.Read502170(fixture.Model, fixture.Resource, 240, 0));
    }

    private static (NativeBattleCactuarRawModelState Model, NativeBattleEffectOwnedModelResource Resource) Fixture()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 1000);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 32767);
        byte[] bytes = new byte[148];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] sections = [24, 136, 140, 144, 148];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        skeleton.CopyTo(bytes, 24);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleEffectOwnedModelResource resource = new(0x20DFAB8, bytes.ToImmutableArray(), source, []);
        NativeBattleCactuarRawModelState model = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), resource, 0x20DFAB8, 3, NativeBattleShivaModelTask.Binding);
        byte[] raw = model.Model.ToArray();
        foreach (int offset in new[] { 0x40, 0x48, 0x50 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0x54), 40000);
        NativeBattleDecodedPose pose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 0, 0), new(1024, 1024, 1024))], false);
        return (model with { Model = raw.ToImmutableArray(), DecodedPose = pose }, resource);
    }
}
