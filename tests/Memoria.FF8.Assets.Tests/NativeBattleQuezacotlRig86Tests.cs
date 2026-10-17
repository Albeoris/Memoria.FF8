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
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 512);
        Assert.Equal(41920, file.Data.Length);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(NativeBattleQuezacotlRig86.Resource, file.Data.AsSpan()) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child], Quezacotl = new(1, 0, 3690) };
        NativeBattleQuezacotlRigWrite initialized = NativeBattleQuezacotlRig86.InitializeBeforeSecondWindow(memory, effect);
        int skeleton = NativeBattleQuezacotlRig86.Resource + 24;
        Assert.Equal((byte)47, initialized.Memory.Arena!.Read(skeleton, 1)[0]);
        Assert.Equal(new byte[] { 0, 0, 0x9D, 0xFC, 37, 0 }, initialized.Memory.Arena.Read(skeleton + 8, 6));
        Assert.Equal(new byte[] { 0, 0, 0, 0xF8, 0, 0 }, initialized.Memory.Arena.Read(skeleton + 20, 6));
        Assert.Equal(file.Data.AsSpan().Slice(24 + 26, 6).ToArray(), initialized.Memory.Arena.Read(skeleton + 26, 6));
        NativeBattleActionMatrix root = NativeBattleQuezacotlRig86.ReadBoneMatrix(initialized.Memory, 0);
        Assert.Equal((0, -1623, 69), (root.X, root.Y, root.Z));
        Assert.Equal((byte)1, initialized.Memory.Arena.Read(0x9A, 1)[0]);
        Assert.Equal((byte)8, initialized.Memory.Arena.Read(0x9B, 1)[0]);
        Assert.Throws<NotSupportedException>(() => initialized.Memory.Arena.Read(skeleton + 32 + 48 + 18, 2));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(initialized.Memory))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(initialized.Memory, 1)), JsonSerializer.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(restored, 1)));
        NativeBattleQuezacotlRigWrite advanced = NativeBattleQuezacotlRig86.Advance6FBDB0(initialized.Memory, initialized.Effect);
        Assert.Equal((byte)2, advanced.Memory.Arena!.Read(0x9A, 1)[0]);
        Assert.Equal((byte)0, advanced.Memory.Arena.Read(0x95, 1)[0]);
        NativeBattlePoseSource original = new(file.Data.AsSpan().Slice(24, 0x8F8 - 24).ToArray().ToImmutableArray(), file.Data.AsSpan().Slice(0x6DF0, 0xA3C0 - 0x6DF0).ToArray().ToImmutableArray());
        NativeBattleDecodedPose expected = original.Sample(0, 2, 0);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(original, expected)[1]), JsonSerializer.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(advanced.Memory, 1)));
        NativeBattleActionMatrix before = NativeBattleQuezacotlRig86.ReadBoneMatrix(advanced.Memory, 0);
        NativeBattleQuezacotlRigWrite composed = NativeBattleQuezacotlRig86.Compose5095B0(advanced.Memory, advanced.Effect);
        Assert.Equal(before.Z + 3690, NativeBattleQuezacotlRig86.ReadBoneMatrix(composed.Memory, 0).Z);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleQuezacotlSkinBatch skin = NativeBattleQuezacotlRig86.Emit6C6770(composed.Memory, composed.Effect, new(identity, 216, 160, 108), new(0, 0, 1f / 4096, 4503601774854144d), -70);
        Assert.Contains(skin.Writes, write => write.Bank == NativeBattleQuezacotlSkinBank.VertexScratch);
        Console.WriteLine($"Actual MAG115_H.07 frame2 skin: writes={skin.Writes.Length}, common={skin.CommonAllocationBytes}, effect={skin.EffectAllocationBytes}");
        NativeBattleQuezacotlRigWrite recomputed = NativeBattleQuezacotlRig86.Restore508C90(composed.Memory, composed.Effect);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(NativeBattleQuezacotlRig86.ReadBoneMatrix(recomputed.Memory, 0)));
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
