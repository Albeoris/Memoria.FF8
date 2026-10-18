using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleScanEntryResourcesTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void Original_Scan_entry_uploads_only_its_TIM_when_enabled_and_never_registers_an_animation(byte flags)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(game.GameDirectory);
        BattleNativeAction action = battle.NativeActions.Single(action => action.Command == 2 && action.ActionId == 50);
        Assert.Equal((ushort)40, action.MagicId);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(action.MagicId, game.ReadNativeEffectCatalogForEffects([action.MagicId]));
        var initialized = NativeBattleOriginalEffects.Initialize(game.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, action.Command, 11, 0, action.ActionId, action.MagicId, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = initialized.Effect with { Parameters = new(0, flags, 8, 0, 0, 0) };
        NativeBattleScanFrame frame = NativeBattleScanController.Initialize84D030(initialized.Memory, effect, packets);
        Assert.Equal(0x269ABA8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x2D));
        Assert.Equal(initialized.Memory.Cursor, frame.Memory.Cursor);
        Assert.Same(initialized.Memory.Textures!.Animation, frame.Memory.Textures!.Animation);
        Assert.Equal(flags == 0 ? 1 : 0, frame.Memory.Textures.Count);
        if (flags == 0)
        {
            int source = Assert.Single(frame.Effect.ResourcePointers).ArenaOffset;
            Assert.Equal(source, frame.Memory.Textures.Slots[0]!.ArenaOffset);
            NativeBattleEffectTextureDrain drained = frame.Memory.Textures.Drain505D20(frame.Memory.Arena);
            Assert.Equal(2, drained.Uploads.Length);
            Assert.Equal(0, drained.State.Count);
        }
        else
        {
            NativeBattleScanFrame skipped = NativeBattleScanController.Initialize84D030(initialized.Memory with { Textures = null, Arena = null }, effect with { ResourcePointers = [] }, packets);
            Assert.Null(skipped.Memory.Textures);
            Assert.Null(skipped.Memory.Arena);
        }
    }
}
