using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectModelHelperTests
{
    private static readonly BattlePartyActorModel Target = new(new("target"));
    private static readonly BattlePartyActorModel Caster = new(new("caster"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Observer_receives_actual_F0_then_F1_producers_from_selected_target_or_caster(bool caster)
    {
        NativeBattlePoseSource source = Source();
        BattleActorModels models = PublishedModels(source);
        List<BattleModelOwner> sources = [];
        List<NativeBattleBoneAnchorProducer> observed = [];
        NativeBattlePoseSource ReadSource(BattleModelOwner actor)
        {
            sources.Add(actor);
            return source;
        }
        NativeBattleModulePoolTask task = TargetTask(Target);
        NativeBattleModulePoolTask updated = caster
            ? NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, Parameters(Caster), models.Get, ReadSource, observed.Add)
            : NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, ReadSource, observed.Add);
        BattleModelOwner selected = caster ? Caster : Target;
        NativeBattleGraphicsModel selectedModel = models.Get(selected);
        Assert.Equal(selected, Assert.Single(sources));
        Assert.Equal(2, observed.Count);
        Assert.Equal((short)0, observed[0].Distance);
        Assert.Equal((short)200, observed[1].Distance);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattlePoseMatrices.ResolveBoneAnchor502170(selectedModel, source, 240, 0))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(observed[0])));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattlePoseMatrices.ResolveBoneAnchor502170(selectedModel, source, 241, 0))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(observed[1])));
        Assert.Equal(selectedModel.Transform!.Angle36, unchecked((short)updated.ReadWord(0x32)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Host_caster_and_target_anchors_observe_typed_owners_and_preserve_unreached_bounds(bool caster)
    {
        BattleForeignPartyModel foreignTarget = new(new("external-target"));
        BattleForeignEnemyModel foreignCaster = new(new("external-caster"));
        BattleActorModels models = new([new(foreignTarget, Model(0, new(100, 200, 300), -333)), new(foreignCaster, Model(1, new(-100, -200, -300), 777))]);
        NativeBattleEffectActorGeometry geometry = new(new(-11, 53, 29), new(8, -24, 17), -999, new(new(-40, -50, -60), new(70, 80, 90)));
        NativeBattleModulePoolTask task = TargetTask(foreignTarget).WriteWord(0x48, 0xA55A).WriteWord(0x50, 0xB66B);
        List<(BattleModelOwner Actor, byte Selector)> observations = [];
        NativeBattlePoseSource ReadSource(BattleModelOwner actor) => throw new InvalidOperationException("A host pose must not require a native animation bank.");
        NativeBattleModulePoolTask updated = caster
            ? NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, Parameters(foreignCaster), models.Get, ReadSource, readForeign: _ => geometry, observeForeignAnchor: (actor, selector) => observations.Add((actor, selector)))
            : NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, ReadSource, readForeign: _ => geometry, observeForeignAnchor: (actor, selector) => observations.Add((actor, selector)));
        BattleModelOwner selected = caster ? foreignCaster : foreignTarget;
        NativeBattleGraphicsModel selectedModel = models.Get(selected);
        Assert.Equal(new (BattleModelOwner, byte)[] { (selected, 240), (selected, 241) }, observations);
        Assert.Equal(new short[] { 8, selectedModel.Transform!.Angle36, 17 }, Words(updated, 0x30));
        Assert.Equal(new short[] { -1, 14, 23 }, Words(updated, 0x38));
        Assert.Equal(new short[] { -11, 53, 29 }, Words(updated, 0x40));
        Assert.Equal((ushort)0xA55A, updated.ReadWord(0x48));
        Assert.Equal((ushort)0xB66B, updated.ReadWord(0x50));
        models = models.Set(selected, selectedModel with { Flags0 = 0 });
        observations.Clear();
        NativeBattleModulePoolTask disabled = caster
            ? NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, Parameters(foreignCaster), models.Get, ReadSource, readForeign: _ => throw new InvalidOperationException("Disabled actors do not read host anchors."))
            : NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, ReadSource, readForeign: _ => throw new InvalidOperationException("Disabled actors do not read host anchors."));
        Assert.Same(task, disabled);
        Assert.Empty(observations);
    }

    [Fact]
    public void Current_model_geometry_aliases_override_source_selectors_and_adjacent_WORDs_remain_retained()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(0, new(100, 200, 300), -333);
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source), SequenceState = model.SequenceState! with { Geometry4 = 0x81, Geometry5 = 0 } };
        BattleModelRoster roster = new([Target]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Target)]);
        Assert.True(aliases.TryEncodeExact(Target, out byte operand));
        NativeBattleModulePoolTask task = TargetTask(Target, [operand]).WriteWord(0x36, 0xA55A).WriteWord(0x3E, 0xB66B).WriteWord(0x46, 0xC77C);
        List<NativeBattleBoneAnchorProducer> observed = [];
        BattleActorModels models = new([new(Target, model)]);
        NativeBattleModulePoolTask updated = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, _ => source, observed.Add);
        Assert.Equal((short)200, observed[0].Distance);
        Assert.Equal((short)0, observed[1].Distance);
        Assert.Equal(new short[] { 100, -333, 300 }, Words(updated, 0x30));
        Assert.Equal(new short[] { 100, 200, 600 }, Words(updated, 0x40));
        Assert.Equal((ushort)0xA55A, updated.ReadWord(0x36));
        Assert.Equal((ushort)0xB66B, updated.ReadWord(0x3E));
        Assert.Equal((ushort)0xC77C, updated.ReadWord(0x46));
        Assert.Equal(operand, updated.ReadByte(0x2D));
        Assert.Equal(Target, updated.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner);
    }

    [Fact]
    public void Target_and_caster_select_different_original_models_and_keep_angle_separate_from_anchor_Y()
    {
        NativeBattlePoseSource source = Source(200, new(1, -3, 2));
        BattleActorModels models = PublishedModels(source);
        NativeBattleModulePoolTask task = TargetTask(Target);
        NativeBattleModulePoolTask target = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, _ => source);
        Assert.Equal(new short[] { 116, -333, 632 }, Words(target, 0x30));
        Assert.Equal(new short[] { 116, 152, 482 }, Words(target, 0x38));
        Assert.Equal(new short[] { 116, 152, 332 }, Words(target, 0x40));
        NativeBattleModulePoolTask caster = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, Parameters(Caster), models.Get, _ => source);
        Assert.Equal(new short[] { -84, 777, 32 }, Words(caster, 0x30));
        Assert.Equal(new short[] { -84, -248, -118 }, Words(caster, 0x38));
        Assert.Equal(new short[] { -84, -248, -268 }, Words(caster, 0x40));
    }

    [Fact]
    public void Midpoints_use_signed_truncation_and_unloaded_models_do_not_read_pose_or_modify_retained_fields()
    {
        NativeBattleModulePoolTask task = TargetTask(Target).WriteWord(0x32, 123);
        NativeBattleGraphicsModel model = Model(0, new(0, 0, 0), 0);
        NativeBattlePoseSource source = Source(-101, new(0, 0, -1));
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source) };
        BattleActorModels models = new([new(Target, model)]);
        NativeBattleModulePoolTask loaded = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, _ => source);
        Assert.Equal((short)-17, unchecked((short)loaded.ReadWord(0x34)));
        Assert.Equal((short)-16, unchecked((short)loaded.ReadWord(0x44)));
        Assert.Equal((short)-16, unchecked((short)loaded.ReadWord(0x3C)));
        models = models.Set(Target, model with { Flags0 = 0, Transform = null, SequenceState = null });
        NativeBattleModulePoolTask unchanged = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, models.Get, _ => throw new InvalidOperationException("Unloaded model must not read a resource."), _ => throw new InvalidOperationException("Unloaded model must not observe a bone producer."));
        Assert.Same(task, unchanged);
    }

    [Fact]
    public void Target_bounds_resolve_an_unaliased_enemy_from_task_metadata()
    {
        BattleEnemyActorModel extra = new(new("extra-enemy"));
        NativeBattlePoseSource source = Source(200, new(1, -3, 2));
        NativeBattleGraphicsModel model = PublishModel(Model(0, new(100, 200, 300), -333), source);
        BattleActorModels models = new([new(extra, model)]);
        List<BattleModelOwner> requested = [];
        NativeBattlePoseSource ReadSource(BattleModelOwner owner)
        {
            requested.Add(owner);
            return source;
        }

        NativeBattleModulePoolTask updated = NativeBattleEffectModelHelpers.UpdateTargetWithBounds8DC610(TargetTask(extra), models.Get, ReadSource);

        Assert.Equal(new BattleModelOwner[] { extra, extra }, requested);
        Assert.Equal(new short[] { 116, -333, 632 }, Words(updated, 0x30));
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.LocalBounds8DC870(source, source.Sample(0, 1, model.SequenceState!.PoseFlags109));
        Assert.Equal(new short[] { bounds.Minimum.X, bounds.Minimum.Y, bounds.Minimum.Z }, Words(updated, 0x48));
        Assert.Equal(new short[] { bounds.Maximum.X, bounds.Maximum.Y, bounds.Maximum.Z }, Words(updated, 0x50));
    }

    [Fact]
    public void Target_bounds_reject_a_retired_owner_before_reading_foreign_geometry()
    {
        BattleForeignEnemyModel retired = new(new("former-target"));
        BattleForeignEnemyModel replacement = new(new("replacement-target"));
        NativeBattleGraphicsModel model = Model(0, default, 0);
        BattleActorModels models = new([new(retired, model)]);
        NativeBattleModulePoolTask task = TargetTask(retired).WriteWord(0x32, 123);
        models = models.ReplaceOwner(retired, replacement, model);
        bool readForeign = false;
        NativeBattleEffectActorGeometry ReadForeign(BattleModelOwner owner)
        {
            readForeign = true;
            return new(default, default, 0, new(default, default));
        }

        Assert.Throws<KeyNotFoundException>(() => NativeBattleEffectModelHelpers.UpdateTargetWithBounds8DC610(task, models.Get, _ => Source(), ReadForeign));
        Assert.False(readForeign);
        Assert.Equal((ushort)123, task.ReadWord(0x32));
    }

    [Fact]
    public void Raw_target_byte_alone_cannot_select_a_model()
    {
        BattleModelRoster roster = new([Target]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Target)]);
        Assert.True(aliases.TryEncodeExact(Target, out byte operand));
        NativeBattleModulePoolTask task = Task().WriteByte(0x2D, operand).WriteWord(0x32, 123);

        Assert.Throws<InvalidDataException>(() => NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, ReadModel, _ => Source()));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectModelHelpers.UpdateTargetWithBounds8DC610(task, ReadModel, _ => Source()));
        Assert.Equal((ushort)123, task.ReadWord(0x32));
        Assert.Equal(operand, task.ReadByte(0x2D));

        NativeBattleGraphicsModel ReadModel(BattleModelOwner owner)
        {
            throw new InvalidOperationException("A model lookup requires a live target reference.");
        }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bounds_read_loaded_or_composed_current_matrices_without_selecting_a_pose(bool composed)
    {
        NativeBattlePoseSource source = LoadedBoundsSource(200);
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(Model(0, new(9000, 8000, 7000), 0) with { SequenceState = new() }, source);
        if (composed)
        {
            model = NativeBattleBodyBoneMatrices.Compose5095B0(model, new([-4096, 0, 0, 0, 4096, 0, 0, 0, -4096], 1000, -2000, 3000, 4096));
        }
        string originalModel = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(model));
        NativeBattleModulePoolTask task = Task().WriteWord(0x4E, 0xA55A).WriteWord(0x56, 0xB66B);
        string originalTask = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task));

        NativeBattleModulePoolTask updated = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, model, source);

        Assert.Equal(composed ? new short[] { 900, -1980, 2900 } : new short[] { 10, 20, 30 }, Words(updated, 0x48));
        Assert.Equal(composed ? new short[] { 990, -1800, 2970 } : new short[] { 100, 200, 100 }, Words(updated, 0x50));
        Assert.Equal((ushort)0xA55A, updated.ReadWord(0x4E));
        Assert.Equal((ushort)0xB66B, updated.ReadWord(0x56));
        Assert.Null(model.SequenceState!.Pose);
        Assert.Null(model.SequenceState.DecodedPose);
        Assert.Equal(originalModel, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(model)));
        Assert.Same(task, updated);

        NativeBattleGraphicsModel restoredModel = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGraphicsModel>(originalModel)!);
        NativeBattlePoseSource restoredSource = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattlePoseSource>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source)))!);
        NativeBattleModulePoolTask restoredTask = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(originalTask)!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(updated)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffectModelHelpers.UpdateBounds8DC870(restoredTask, restoredModel, restoredSource))));
    }

    [Fact]
    public void Current_bounds_wrap_the_negated_length_and_reject_missing_or_wrong_rig_publications()
    {
        NativeBattlePoseSource source = LoadedBoundsSource(short.MinValue);
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(Model(0, default, 0) with { SequenceState = new() }, source);
        NativeBattleModulePoolTask updated = NativeBattleEffectModelHelpers.UpdateBounds8DC870(Task(), model, source);
        Assert.Equal(new short[] { 10, 20, -32468 }, Words(updated, 0x48));
        Assert.Equal(new short[] { 100, 200, 30 }, Words(updated, 0x50));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectModelHelpers.UpdateBounds8DC870(Task(), model with { CurrentBodyBoneMatrices = null }, source));
        NativeBattlePoseSource differentRig = source with { Skeleton = source.Skeleton.SetItem(18, 101) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectModelHelpers.UpdateBounds8DC870(Task(), model, differentRig));
    }

    private static NativeBattlePoseSource LoadedBoundsSource(short childLength)
    {
        NativeBattlePoseSource source = Source(childLength);
        byte[] skeleton = source.Skeleton.ToArray();
        for (int bone = 0; bone < 2; bone++)
        {
            int matrix = 32 + 48 * bone;
            for (int lane = 0; lane < 3; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(matrix + 8 * lane), 4096);
                BinaryPrimitives.WriteInt32LittleEndian(skeleton.AsSpan(matrix + 20 + 4 * lane), (bone == 0 ? 10 : 100) * (lane + 1));
            }
        }
        return source with { Skeleton = skeleton.ToImmutableArray() };
    }

    private static NativeBattleGraphicsModel Model(byte modelId, NativeBattleVector position, short angle)
    {
        return new(3) { ScaleVectorAddress48 = 0, ModelId4 = modelId, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(position, position, 0, angle, 16384), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
    }

    private static BattleActorModels PublishedModels(NativeBattlePoseSource source)
    {
        NativeBattleGraphicsModel target = PublishModel(Model(0, new(100, 200, 300), -333), source);
        NativeBattleGraphicsModel caster = PublishModel(Model(1, new(-100, -200, -300), 777), source);
        return new([new(Target, target), new(Caster, caster)]);
    }

    private static NativeBattleGraphicsModel PublishModel(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        return model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source) };
    }

    private static NativeBattleEffectParameters Parameters(BattleModelOwner source)
    {
        return new(source, 0, BattleModelTargets.Empty, new(0), 0, 0);
    }

    private static NativeBattleModulePoolTask TargetTask(BattleModelOwner target, ImmutableArray<byte>? nativeBytes = null)
    {
        NativeBattleModulePoolTask task = Task();
        task.WriteExecutionReference(0x2D, 1, new BattleExecutionReference.Model(target), nativeBytes);
        return task;
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
