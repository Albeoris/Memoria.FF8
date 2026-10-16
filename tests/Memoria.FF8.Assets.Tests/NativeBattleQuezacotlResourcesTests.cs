using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlResourcesTests
{
    [Fact]
    public void Original116CustomInitializerAndEntryRetainSeparateWorkBankOneRandomDrawAndPhysicalMainPool()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        ImmutableArray<NativeBattleEffectBinding> original = game.ReadNativeEffectCatalog();
        Assert.Null(original[115].NativeQuezacotlResources);
        ImmutableArray<NativeBattleEffectBinding> catalog = game.ReadNativeEffectCatalogForEffects([116]);
        NativeBattleQuezacotlResources resources = Assert.IsType<NativeBattleQuezacotlResources>(catalog[115].NativeQuezacotlResources);
        Assert.Null(catalog[115].InitializerResource);
        Assert.All(catalog.Where(binding => binding.Effect != 116), binding => Assert.Null(binding.NativeQuezacotlResources));
        Assert.Equal(new[] { 0, 0x100 }, resources.Vibration.Offsets.ToArray());
        resources.Vibration.Validate();
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        memory = memory with { GuardianWorkBuffer = memory.GuardianWorkBuffer!.Write(NativeBattleGuardianWorkBuffer.Length - 1, [0xA5]) };
        NativeBattleOriginalEffectState effect = NativeBattleOriginalEffects.Select(116, catalog);
        (memory, effect) = NativeBattleOriginalEffects.Initialize(memory, effect);
        Assert.Equal(0x6C3580u, memory.EffectIo!.Callback);
        Assert.Equal(resources.Initializer.Data.Length, memory.Cursor);
        NativeBattleActionPacketsState packets = game.ReadColdProcessNativeBattleMemory().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 0xFE, 14, -1, 64, 116, null));
        foreach (byte target in new byte[] { 3, 4 })
        {
            (packets, _) = packets.AppendContact(new(new(target, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, int header) = packets.Publish(2);
        effect = effect with { Parameters = new(0, 0, 0x18, header, 0, 0) };
        NativeCrtRandomState random = new(1);
        NativeCrtRandomState expected = random.Draw(out ushort value);
        NativeBattleQuezacotlEntry entered = NativeBattleQuezacotl.Enter6C3640(memory, effect, random, packets, index => index == 3 ? (short)-101 : (short)100);
        Assert.Equal(expected, entered.Random);
        Assert.Equal(value, entered.Effect.Quezacotl!.Random17A0);
        Assert.Equal(0, entered.Effect.Quezacotl.TargetZ1770);
        Assert.Equal(3690, entered.Effect.Quezacotl.TargetZ1778);
        Assert.Equal(resources.WorkSeed0.Data.ToArray(), entered.Memory.GuardianWorkBuffer!.Read(0, 0x109FC));
        Assert.Equal(resources.WorkSeed1.Data.ToArray(), entered.Memory.GuardianWorkBuffer.Read(0x109FC, 0x4D9C));
        Assert.Equal(0xA5, entered.Memory.GuardianWorkBuffer.Read(NativeBattleGuardianWorkBuffer.Length - 1, 1)[0]);
        NativeBattleEffectTaskPool pool = Assert.Single(entered.Effect.Pools);
        Assert.Equal(0x2521748u, entered.Effect.ReturnedDescriptor);
        Assert.Equal(0x2521758u, pool.BankAddress);
        Assert.Equal((ushort)20, pool.Stride);
        Assert.Equal(0x6C3760u, Assert.Single(pool.Slots).Callback);
        Assert.All(pool.Slots[0].Payload, item => Assert.Equal((byte)0, item));
        Assert.Equal(1, entered.Memory.Textures!.Count);
        Assert.Equal(2, entered.Memory.Textures.Drain505D20().Uploads.Length);
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(entered.Effect))!;
        restored.Validate();
        NativeBattleQuezacotlPreparation firstFrame = NativeBattleQuezacotl.Prepare6C3760(entered.Memory, restored, null, null);
        Assert.False(firstFrame.ChildBoundary);
        Assert.Equal(0x15E50, firstFrame.Effect.DrawArenaOffset);
        Assert.Equal((ushort)1, firstFrame.Effect.Pools[0].Slots[0].ReadWord(12));
        NativeBattleQuezacotlPreparation secondFrame = NativeBattleQuezacotl.Prepare6C3760(firstFrame.Memory, firstFrame.Effect, null, null);
        Assert.False(secondFrame.ChildBoundary);
        Assert.Equal(0x2E50, secondFrame.Effect.DrawArenaOffset);
        Assert.Equal((ushort)2, secondFrame.Effect.Pools[0].Slots[0].ReadWord(12));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1, 2, 3, 4096);
        NativeBattleQuezacotlPreparation childPrefix = NativeBattleQuezacotl.Prepare6C3760(secondFrame.Memory, secondFrame.Effect, NativeVibrationProcessState.FromColdProcess(), view);
        Assert.True(childPrefix.ChildBoundary);
        Assert.Equal((ushort)2, childPrefix.Effect.Pools[0].Slots[0].ReadWord(12));
        NativeBattleEffectTaskPool child = childPrefix.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        Assert.Equal(0x20DFAB8u, child.BankAddress);
        Assert.Equal((ushort)0x8D0, child.Stride);
        Assert.Equal(0x6C3940u, child.Slots[0].Callback);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, childPrefix.Memory.Arena!.Read(0x28E54, 5));
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, childPrefix.Memory.Arena.Read(0x28E50, 4));
        Assert.Equal(0x28E54, childPrefix.Memory.EffectIo!.BaseOffset);
        Assert.Equal((byte)128, childPrefix.Vibration!.Slots.Single(slot => slot.Priority != 0).Priority);
        Assert.NotEqual(0u, childPrefix.Effect.ReturnedDescriptor);
        Assert.Equal(240066u, resources.InitialSound);
        NativeBattleQuezacotlChildStep childZero = NativeBattleQuezacotlChild.AdvanceFirst6C3940(childPrefix.Memory, childPrefix.Effect);
        Assert.True(childZero.Entered);
        Assert.Equal(new NativeBattleEffectFileRequest(505, 0x2108954, 0x6C3580), childZero.FileRequest);
        Assert.Equal(2400, childZero.Effect.Quezacotl!.Intensity17A4);
        Assert.Equal((ushort)3, childZero.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521748).Slots[0].ReadWord(12));
        Assert.Equal((ushort)1, childZero.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 505);
        NativeBattleEffectMemory loaded = childZero.Memory with { Arena = childZero.Memory.Arena!.Write(0x28E9C, file.Data.AsSpan()) };
        NativeBattleEffectIoCompletion complete = NativeBattleEffectIo.Complete6C3580(loaded, _ => throw new InvalidOperationException("Arena TIM must retain its mutable native pointer."));
        Assert.Equal(0x28E9C, complete.Memory.Textures!.Slots[1]!.ArenaOffset);
        Assert.Equal(new byte[] { 0, 1, 1, 3, 1 }, complete.Memory.Arena!.Read(0x28E54, 5));
        Assert.Equal(4, complete.Memory.Textures.Drain505D20(complete.Memory.Arena).Uploads.Length);
        Assert.Contains("7016B0", Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlChild.AdvanceFirst6C3940(complete.Memory, childZero.Effect)).Message);
        NativeBattleEffectMemory later = entered.Memory.BeginLoad(1);
        Assert.Equal(0x534150u, later.EffectIo!.Callback);
        Assert.Equal(0xA5, later.GuardianWorkBuffer!.Read(NativeBattleGuardianWorkBuffer.Length - 1, 1)[0]);
        Assert.Equal(0, later.Arena!.Read(0, 1)[0]);
    }
}
