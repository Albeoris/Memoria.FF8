using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSharedDrawingTemporaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Neutral_pose_temporary_matches_actor_publication_without_requiring_a_fake_stage_actor(bool scaled)
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 150);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleDecodedPose pose = new(new(10, 20, 30), [new(new(0, 128, 0), new(1024, 1024, 1024)), new(new(128, 0, 0), new(1100, 1200, 1300))], scaled);
        NativeBattleGraphicsModel actor = new(3, 3) { SequenceState = new() { DecodedPose = pose } };
        NativeBattleEffectArena original = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0x4C, [0x55, 0x66, 0x77, 0x88]);
        NativeBattleBodyBonePreparation actualActor = NativeBattleBodyBoneTemporary.Prepare508C90(actor, source, original);
        NativeBattleEffectArena actualStage = NativeBattleBodyBoneTemporary.Prepare508C90(source, pose, NativeBattlePoseMatrices.StageBoneMatrices(source, pose), original);
        Assert.Equal(JsonSerializer.Serialize(actualActor.Temporary58), JsonSerializer.Serialize(actualStage));
        Assert.Equal(new byte[] { 0x55, 0x66, 0x77, 0x88 }, actualStage.Read(0x4C, 4));
        Assert.Equal(new byte[] { 0, 0 }, actualStage.Read(18, 2));
        Assert.Throws<NotSupportedException>(() => actualStage.Read(0x20, 4));
        Assert.Null(actor.CurrentBodyBoneMatrices);
        Assert.NotNull(actualActor.Model.CurrentBodyBoneMatrices);
    }

    [Fact]
    public void Shared_shadow_retains_physical_reservation_and_unknown_vector_padding_until_caller_release()
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGraphicsModel actor = new(3, 3)
        {
            GeometryWorldMatrix = identity with { Z = 2000 },
            Transform = new(new(0, 0, 0), new(0, 0, 2000), 0, 0, 4096),
            GeometryBounds52 = new(new(-100, 0, -100), new(100, 0, 100)),
            Color44 = 0x32808080
        };
        NativeBattleShadowSource source = new(identity, new(4096, 0, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address + 0x26, [0x44, 0x55]);
        NativeBattleShadowDrawStep step = NativeBattleShadowDrawing.Prepare5088A0(staging, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), actor, new(identity, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess(), source, true, 4121, 16);
        Assert.Equal(NativeBattleTemporaryMemory.Address + 0x78, BinaryPrimitives.ReadUInt32LittleEndian(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new byte[] { 0x44, 0x55 }, step.Temporary78.Read(0x26, 2));
        Assert.Throws<NotSupportedException>(() => step.Temporary78.Read(18, 2));
        Assert.False(step.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(step.Registers.Colors.NormalInputKnown.Value[7]);
        Assert.NotNull(step.Registers.Geometry.Sxy[2]);
        Assert.Equal(step.Registers.Colors.Flags!.Value, BinaryPrimitives.ReadUInt32LittleEndian(step.Temporary78.Read(0x70, 4)));
        NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(step.Staging, step.TemporaryAddress, step.Temporary78, 0x78);
        published = NativeBattleTemporaryMemory.Release5082D0(published, 0x78);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(published.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }
}
