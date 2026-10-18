using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectModelHelperTests
{
    [Fact]
    public void Target_and_caster_select_different_original_models_and_keep_angle_separate_from_anchor_Y()
    {
        NativeBattlePoseSource source = Source(200, new(1, -3, 2));
        NativeBattleGraphicsModel[] models = [Model(0, new(100, 200, 300), -333), Model(1, new(-100, -200, -300), 777)];
        models = models.Select(model => NativeBattleBodyBoneMatrices.Publish508C90(model, source)).Select(model => model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source) }).ToArray();
        NativeBattleModulePoolTask task = Task().WriteByte(0x2D, 0);
        NativeBattleModulePoolTask target = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models, _ => source);
        Assert.Equal(new short[] { 116, -333, 332 }, Words(target, 0x30));
        Assert.Equal(new short[] { 116, 152, 482 }, Words(target, 0x38));
        Assert.Equal(new short[] { 116, 152, 632 }, Words(target, 0x40));
        NativeBattleEffectParameters parameters = new(1, 0, 0, 0, 0, 0);
        NativeBattleModulePoolTask caster = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, models, _ => source);
        Assert.Equal(new short[] { -84, 777, -268 }, Words(caster, 0x30));
        Assert.Equal(new short[] { -84, -248, -118 }, Words(caster, 0x38));
        Assert.Equal(new short[] { -84, -248, 32 }, Words(caster, 0x40));
    }

    [Fact]
    public void Midpoints_use_signed_truncation_and_unloaded_models_do_not_read_pose_or_modify_retained_fields()
    {
        NativeBattleModulePoolTask task = Task().WriteByte(0x2D, 0).WriteWord(0x32, 123);
        NativeBattleGraphicsModel model = Model(0, new(0, 0, 0), 0);
        NativeBattlePoseSource source = Source(-101, new(0, 0, -1));
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source) };
        NativeBattleModulePoolTask loaded = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, [model], _ => source);
        Assert.Equal((short)-16, unchecked((short)loaded.ReadWord(0x34)));
        Assert.Equal((short)-17, unchecked((short)loaded.ReadWord(0x44)));
        Assert.Equal((short)-16, unchecked((short)loaded.ReadWord(0x3C)));
        NativeBattleModulePoolTask unchanged = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, [model with { Flags0 = 0, Transform = null, SequenceState = null }], _ => throw new InvalidOperationException("Unloaded model must not read a resource."));
        Assert.Same(task, unchanged);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectModelHelpers.UpdateTarget8DC610(task.WriteByte(0x2D, 7), [model], _ => Source()));
    }

    [Fact]
    public void Local_bounds_use_posed_bone_endpoints_without_model_world_translation()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleDecodedPose pose = new(new(1, -3, 2), [new(default, new(1024, 1024, 1024)), new(default, new(1024, 1024, 1024))], false);
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.LocalBounds8DC870(source, pose);
        Assert.Equal(new(16, -48, -68), bounds.Minimum);
        Assert.Equal(new(16, -48, 32), bounds.Maximum);
        NativeBattleModulePoolTask updated = NativeBattleEffectModelHelpers.UpdateBounds8DC870(Task(), source, pose);
        Assert.Equal(new short[] { 16, -48, -68 }, Words(updated, 0x48));
        Assert.Equal(new short[] { 16, -48, 32 }, Words(updated, 0x50));
        pose = pose with { Bones = pose.Bones.SetItem(1, pose.Bones[1] with { Rotation = new(0, 1024, 0) }) };
        bounds = NativeBattlePoseMatrices.LocalBounds8DC870(source, pose);
        Assert.Equal(new(-184, -48, 32), bounds.Minimum);
        Assert.Equal(new(16, -48, 132), bounds.Maximum);
    }

    [Fact]
    public void Bounds_negate_bone_length_as_a_WORD_and_require_every_actual_matrix()
    {
        NativeBattlePoseSource source = Source(short.MinValue);
        NativeBattleDecodedPose pose = new(default, [new(default, new(1024, 1024, 1024)), new(default, new(1024, 1024, 1024))], false);
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.LocalBounds8DC870(source, pose);
        Assert.Equal((short)-32668, bounds.Minimum.Z);
        Assert.Equal((short)0, bounds.Maximum.Z);
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseMatrices.LocalBounds8DC870(source, pose with { Bones = [] }));
    }

    private static NativeBattleGraphicsModel Model(int index, NativeBattleVector position, short angle)
    {
        return new(index, 3) { ScaleVectorAddress48 = 0, ModelId4 = (byte)index, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(position, position, 0, angle, 16384), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
    }

    private static NativeBattleModulePoolTask Task() => NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1).Slots[0];

    private static short[] Words(NativeBattleModulePoolTask task, int offset) => Enumerable.Range(0, 3).Select(index => unchecked((short)task.ReadWord(offset + 2 * index))).ToArray();

    private static NativeBattlePoseSource Source(short childLength = 200, NativeBattleVector translation = default)
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        skeleton[4] = 0;
        skeleton[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), childLength);
        List<int> bits = [];
        void Write(int value, int length)
        {
            for (int bit = 0; bit < length; bit++)
            {
                bits.Add(value >> bit & 1);
            }
        }
        foreach (short value in new[] { translation.X, translation.Y, translation.Z })
        {
            Write(3, 2);
            Write(value, 16);
        }
        Write(0, 1);
        Write(0, 6);
        byte[] animations = new byte[9 + (bits.Count + 7) / 8];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 1;
        for (int bit = 0; bit < bits.Count; bit++)
        {
            animations[9 + bit / 8] |= (byte)(bits[bit] << (bit & 7));
        }
        return new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
    }
}
