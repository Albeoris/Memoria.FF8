using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshZantetsukenAnchorsTests
{
    [Fact]
    public void Resolver_uses_current_bone_without_model_world_composition_or_model8F_special_case()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source) with { ModelId4 = 0x8F, GeometryWorldMatrix = Identity() with { X = 30000 } };
        NativeBattleBoneAnchorProducer producer = NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model, source, 0xF0, 0);
        Assert.Same(model.CurrentBodyBoneMatrices!.Matrices[1], producer.Matrix);
        Assert.Equal((short)1000, producer.Distance);
        Assert.Equal(40000, producer.Matrix!.X);
        NativeBattleGraphicsModel restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGraphicsModel>(NativeBattleTestSnapshots.Json(model))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(producer), NativeBattleTestSnapshots.Json(NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(restored, source, 0xF0, 0)));
    }

    [Fact]
    public void Mutable_aliases_override_original_header_and_negative_parent_disables_distance()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source) with { SequenceState = new() { Geometry4 = 0, Geometry5 = 1 } };
        NativeBattleBoneAnchorProducer root = NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model, source, 0xF0, 4096);
        Assert.Same(model.CurrentBodyBoneMatrices!.Matrices[0], root.Matrix);
        Assert.Equal((short)0, root.Distance);
        Assert.Equal((short)500, NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model, source, 0xF1, 2048).Distance);
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model, source, 2, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model with { CurrentBodyBoneMatrices = null }, source, 0xF0, 0));
    }

    [Fact]
    public void Full_MAC_low_words_and_sparse_physical_outputs_preserve_scratch_and_unwritten_bytes()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { AnchorScratch8 = Enumerable.Repeat<byte?>(0x6B, 8).ToImmutableArray() }
        };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0xA5, 52).ToArray());
        NativeBattleBoneAnchorProducer producer = new(Identity() with { X = 40000, Y = -40000, Z = 11 }, 10, default);
        NativeBattleGilgameshZantetsukenAnchorStep step = NativeBattleGilgameshZantetsukenAnchors.Project58E2E0(memory, staging, producer, null);
        Assert.Equal(new NativeBattleVector(-25536, 25536, 21), step.Position);
        Assert.Equal(new int?[] { 40000, -40000, 21 }, step.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 32767, -32768, 21 }, step.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(memory.GeometryRegisters!.AnchorScratch8, step.Memory.GeometryRegisters!.AnchorScratch8);
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 32), step.Staging.Read(NativeBattleTemporaryMemory.Address, 32));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 4), step.Staging.Read(NativeBattleTemporaryMemory.Address + 44, 4));
        Assert.Equal(40000, BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Address + 32, 4)));
        Assert.Equal(-40000, BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Address + 36, 4)));
        Assert.Equal(21, BinaryPrimitives.ReadInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Address + 40, 4)));
        Assert.Equal(step.Memory.ScriptedColorRegisters.Flags, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Address + 48, 4)));
        Assert.Equal(staging.Read(NativeBattleTemporaryMemory.Cursor, 4), step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleGilgameshZantetsukenAnchors.Project58E2E0(NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!, NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging))!, producer, null)));
    }

    [Fact]
    public void Disabled_anchor_does_not_allocate_or_touch_GTE_and_average_truncates_signed_words()
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags0 = 0, Transform = new(default, new(-5, 4, -3), 0, 77, 4096) };
        NativeBattleBoneAnchorProducer producer = NativeBattleGilgameshZantetsukenAnchors.Resolve58E2E0(model, null, 0xF0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleGilgameshZantetsukenAnchorStep step = NativeBattleGilgameshZantetsukenAnchors.Project58E2E0(memory, staging, producer, null);
        Assert.Same(memory, step.Memory);
        Assert.Same(staging, step.Staging);
        Assert.Equal(model.Transform.Position28, step.Position);
        Assert.Equal(new NativeBattleModuleAnchor(-1, 1, 0, 77), NativeBattleGilgameshZantetsukenAnchors.Average58E260(new(-2, 2, -1), new(-1, 1, 0), 77));
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[4] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16, 2), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66, 2), 1000);
        return new(skeleton.ToImmutableArray(), [1]);
    }

    private static NativeBattleGraphicsModel Model(NativeBattlePoseSource source)
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags0 = 2, Transform = new(default, default, 0, 77, 4096) };
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(model, source);
        return model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = [Identity(), Identity() with { X = 40000 }] } };
    }
}
