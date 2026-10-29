using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlRig86Tests
{
    [Fact]
    public void OriginalFile512InitializesItsActual47BonePoseAndPreservesUnwrittenBoneScales()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 512);
        Assert.Equal(41920, NativeBattleStorageSnapshots.Capture(file).Data.Length);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(NativeBattleQuezacotlRig86.Resource, NativeBattleStorageSnapshots.Capture(file).Data.AsSpan()) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child], Quezacotl = new(1, 0, 3690) };
        NativeBattleQuezacotlRigWrite initialized = NativeBattleQuezacotlRig86.InitializeBeforeSecondWindow(memory, effect);
        int skeleton = NativeBattleQuezacotlRig86.Resource + 24;
        Assert.Equal((byte)47, initialized.Memory.Arena!.Read(skeleton, 1)[0]);
        Assert.Equal(new byte[] { 0, 0, 0x9D, 0xFC, 37, 0 }, initialized.Memory.Arena.Read(skeleton + 8, 6));
        Assert.Equal(new byte[] { 0, 0, 0, 0xF8, 0, 0 }, initialized.Memory.Arena.Read(skeleton + 20, 6));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan().Slice(24 + 26, 6).ToArray(), initialized.Memory.Arena.Read(skeleton + 26, 6));
        NativeBattleActionMatrix root = NativeBattleQuezacotlRig86.ReadBoneMatrix(initialized.Memory, 0);
        Assert.Equal((0, -1623, 69), (root.X, root.Y, root.Z));
        Assert.Equal((byte)1, initialized.Memory.Arena.Read(0x9A, 1)[0]);
        Assert.Equal((byte)8, initialized.Memory.Arena.Read(0x9B, 1)[0]);
        Assert.Throws<NotSupportedException>(() => initialized.Memory.Arena.Read(skeleton + 32 + 48 + 18, 2));
        NativeBattleEffectMemory restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(initialized.Memory))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(initialized.Memory, 1)), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(restored, 1)));
        Assert.Equal(new short[] { 93, 33, 696, 562, 578, 450, 9, 240, 388, 408, 265 }, resources.AnchorVertexIndices11!.Value.ToArray());
        int[] selectedBones = [22, 4, 1, 21, 33, 39, 4, 14, 34, 36, 40];
        NativeBattleVector[] selectedLocals = [new(-25, 0, -1543), new(-2, -720, -2153), new(1589, -1034, -3168), new(-172, -411, -323), new(-81, -501, -1974), new(48, -673, -1615), new(204, 604, -2112), new(265, -365, -1220), new(-309, -442, -462), new(92, -439, -313), new(-47, -673, -1615)];
        NativeBattleQuezacotlRigWrite worldInput = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(initialized));
        NativeBattleQuezacotlRigWrite anchorWorld = NativeBattleQuezacotlRig86.Compose5095B0(worldInput.Memory, worldInput.Effect);
        for (int index = 0; index < selectedBones.Length; index++)
        {
            NativeBattleActionMatrix expectedBone = NativeBattleQuezacotlRig86.ReadBoneMatrix(anchorWorld.Memory, selectedBones[index]);
            NativeBattleVector expectedPosition = NativeBattleGfMatrices.Transform460840(expectedBone, selectedLocals[index]);
            NativeBattleQuezacotlAnchor anchor = NativeBattleQuezacotlRig86.ReadMeshAnchor6C6440(initialized.Memory, initialized.Effect, 0, resources.AnchorVertexIndices11!.Value[index]);
            Assert.Equal(expectedPosition, anchor.Position);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(initialized.Memory, selectedBones[index])), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(anchor.Memory, selectedBones[index])));
        }
        NativeBattleQuezacotlRigWrite advanced = NativeBattleQuezacotlRig86.Advance6FBDB0(initialized.Memory, initialized.Effect);
        Assert.Equal((byte)2, advanced.Memory.Arena!.Read(0x9A, 1)[0]);
        Assert.Equal((byte)0, advanced.Memory.Arena.Read(0x95, 1)[0]);
        NativeBattlePoseSource original = new(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan().Slice(24, 0x8F8 - 24).ToArray().ToImmutableArray(), NativeBattleStorageSnapshots.Capture(file).Data.AsSpan().Slice(0x6DF0, 0xA3C0 - 0x6DF0).ToArray().ToImmutableArray());
        NativeBattleDecodedPose expected = original.Sample(0, 2, 0);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(original, expected)[1]), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(advanced.Memory, 1)));
        NativeBattleActionMatrix before = NativeBattleQuezacotlRig86.ReadBoneMatrix(advanced.Memory, 0);
        NativeBattleQuezacotlRigWrite pathInput = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(advanced));
        NativeBattleQuezacotlRigWrite composed = NativeBattleQuezacotlRig86.Compose5095B0(advanced.Memory, advanced.Effect);
        Assert.Equal(before.Z + 3690, NativeBattleQuezacotlRig86.ReadBoneMatrix(composed.Memory, 0).Z);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleQuezacotlRigWrite pathBones = NativeBattleQuezacotlRig86.ComposePathModel5095B0(pathInput.Memory, pathInput.Effect, identity);
        NativeBattleQuezacotlPathModelBatch pathModel = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan().Slice(0x8F8, 0x6DF0 - 0x8F8).ToArray().ToImmutableArray(), selector => NativeBattleQuezacotlRig86.ReadBoneMatrix(pathBones.Memory, selector), new(identity, 216, 160, 108), 0x808080, 1);
        Assert.NotEmpty(pathModel.VertexScratchWrites);
        Assert.All(pathModel.VertexScratchWrites, write => Assert.Equal(8, NativeBattleStorageSnapshots.Capture(write).Data.Length));
        Console.WriteLine($"Actual MAG115_H.07 ordinary6A79B0: scratch={pathModel.VertexScratchWrites.Length}, primitives={pathModel.Primitives.Length}");
        NativeBattleQuezacotlSkinBatch skin = NativeBattleQuezacotlRig86.Emit6C6770(composed.Memory, composed.Effect, new(identity, 216, 160, 108), new(0, 0, 1f / 4096, 4503601774854144d), -70);
        Assert.Contains(skin.Writes, write => write.Bank == NativeBattleQuezacotlSkinBank.VertexScratch);
        Console.WriteLine($"Actual MAG115_H.07 frame2 skin: writes={skin.Writes.Length}, common={skin.CommonAllocationBytes}, effect={skin.EffectAllocationBytes}");
        NativeBattleQuezacotlRigWrite recomputed = NativeBattleQuezacotlRig86.Restore508C90(composed.Memory, composed.Effect);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(before), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(recomputed.Memory, 0)));
        NativeBattleQuezacotlRigWrite selected = NativeBattleQuezacotlRig86.Select6574D0(recomputed.Memory, recomputed.Effect, 1);
        Assert.Equal((byte)1, selected.Memory.Arena!.Read(0x94, 1)[0]);
        Assert.Equal((byte)1, selected.Memory.Arena.Read(0x9A, 1)[0]);
        NativeBattleDecodedPose selectedExpected = original.Sample(1, 1, 0);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(original, selectedExpected)[1]), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(selected.Memory, 1)));
        NativeBattleQuezacotlRigWrite selectedAdvance = NativeBattleQuezacotlRig86.Advance6FBDB0(selected.Memory, selected.Effect);
        Assert.Equal((byte)2, selectedAdvance.Memory.Arena!.Read(0x9A, 1)[0]);
        NativeBattleQuezacotlRigWrite selectedAgain = NativeBattleQuezacotlRig86.Select6574D0(selectedAdvance.Memory, selectedAdvance.Effect, 1);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(selected.Memory, 1)), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(selectedAgain.Memory, 1)));
        foreach (int paused in new[] { 0, 1 })
        {
            NativeBattleQuezacotlRigWrite third = NativeBattleQuezacotlRig86.PreparePose6C3A1E(selectedAdvance.Memory with { QuezacotlPause = paused }, selectedAdvance.Effect, 238);
            Assert.Equal((byte)2, third.Memory.Arena!.Read(0x94, 1)[0]);
            byte frame = paused == 0 ? (byte)2 : (byte)1;
            Assert.Equal(frame, third.Memory.Arena.Read(0x9A, 1)[0]);
            NativeBattleDecodedPose thirdExpected = original.Sample(2, frame, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(original, thirdExpected)[1]), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(third.Memory, 1)));
        }
        NativeBattleQuezacotlRibbonResources ribbonResources = Assert.IsType<NativeBattleQuezacotlRibbonResources>(resources.Ribbon);
        ribbonResources.Validate();
        Assert.Equal(4032, ribbonResources.ControlTriangles.Length);
        Assert.NotNull(ribbonResources.ParticleScaleWords4096);
        Assert.Equal(4096, ribbonResources.ParticleScaleWords4096.Value.Length);
        Assert.Equal((short)12, ribbonResources.ParticleScaleWords4096.Value[2]);
        Assert.NotEqual(NativeBattleCameraMath.Sine(2), ribbonResources.ParticleScaleWords4096.Value[2]);
        Assert.Equal(new byte[] { 18, 0, 11, 0, 52, 0, 18, 0 }, ribbonResources.Pairs80.AsSpan().Slice(8, 8).ToArray());
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(0, 0, 0), new(0, 0, 0), 216, 0) };
        NativeBattleQuezacotlRigWrite pools = NativeBattleQuezacotlRig86.InitializeAfterSecondWindow(recomputed.Memory, recomputed.Effect, camera);
        NativeBattleQuezacotlRibbonSpawn spawned = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(pools.Memory, pools.Effect, 1);
        NativeBattleQuezacotlRigWrite world = NativeBattleQuezacotlRig86.Compose5095B0(spawned.Memory, spawned.Effect);
        byte[] envelope = world.Memory.Arena!.Read(0x3C25C, 32);
        NativeBattleQuezacotlRibbonStep ribbon = NativeBattleQuezacotlRibbon.Advance6C8850(world.Memory.Arena.Read(0x3BA5C, 128).ToImmutableArray(), ribbonResources, 1, selector => NativeBattleQuezacotlRig86.ReadBoneMatrix(world.Memory, selector), Enumerable.Range(0, 16).Select(index => BinaryPrimitives.ReadInt16LittleEndian(envelope.AsSpan(index * 2))).ToImmutableArray(), 1, false, identity, new(identity, 216, 160, 108));
        Assert.Equal((short)256, BinaryPrimitives.ReadInt16LittleEndian(ribbon.Task128.AsSpan().Slice(0xE)));
        Assert.Equal(0, ribbon.Returned);
        Assert.NotEmpty(ribbon.CommonPackets.Attempts);
        Console.WriteLine($"Actual MAG115_H.07 pair1 ribbon: packets={ribbon.CommonPackets.Attempts.Length}, allocated={ribbon.CommonPackets.AllocationBytes}, random={ribbon.Random17A0}");
    }
}
