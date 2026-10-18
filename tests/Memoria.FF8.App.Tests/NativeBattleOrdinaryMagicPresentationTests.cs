using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOrdinaryMagicPresentationTests
{
    [Fact]
    public void External_ordinary_Aero_cast_completes_with_formula_contact_stock_and_independent_next_callbacks()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_ORDINARY_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "A complete ordinary-produced Aero header standalone is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ORDINARY_OUTPUT") ?? throw new InvalidDataException("A distinct ordinary-cast evidence output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(Path.GetDirectoryName(sourcePath!)!, "before-ordinary-input.battle.json"));
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        int slot = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
        uint offset = graphics.Descriptors.Packets[slot].Argument - 0x1D280C4;
        Assert.Equal(0u, offset % 20);
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(offset / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal((ushort)11, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)118, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        int targetHp = source.Combatants.Single(actor => actor.Id == "enemy").CurrentHp;
        Assert.Equal(contact.Primary.Amount6, beforeInput.Combatants.Single(actor => actor.Id == "enemy").CurrentHp - targetHp);
        Assert.True(contact.Primary.Amount6 > 0);
        Assert.True(source.NativeGlobalRandomCalls > beforeInput.NativeGlobalRandomCalls);
        BattleSession battle = BattleSession.Restore(source);
        Assert.Equal(1, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks).Count);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        BattleSession independent = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!));
        battle.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
        bool initialized = false;
        bool packets = false;
        bool restoredMid = false;
        bool completed = false;
        int callbacks = 1;
        BattleSnapshot prior = battle.Capture();
        try
        {
            for (; callbacks < 512; callbacks++)
            {
                prior = battle.Capture();
                bool restore = !restoredMid && prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 118, ReturnedDescriptor: > 0 } current && current.Pools.Any(pool => pool.DescriptorAddress == 0x2521630 && pool.Slots.Any(task => (task.Flags & 1) != 0 && task.ReadWord(12) == 20));
                if (restore)
                {
                    NativeBattleRestorationPresentationTests.Publish(output, "mid-before", prior, scene.Render(battle, 320, 224));
                    independent = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "mid-before.battle.json")));
                }
                battle.Tick();
                if (restore)
                {
                    independent.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
                    restoredMid = true;
                }
                Assert.Equal(targetHp, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
                Assert.Equal(1, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks).Count);
                if (battle.NativeGraphics!.OriginalEffect is { Binding.Effect: 118 } effect)
                {
                    initialized = true;
                    packets |= battle.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                    if (effect.ReturnedDescriptor == 0 && battle.NativeEffectMemory!.Active == 0 && !battle.NativeGraphics.Descriptors.Queue.Any(index => battle.NativeGraphics.Descriptors.Packets[index].Command == 104) && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
                    {
                        completed = true;
                        break;
                    }
                }
            }
            Assert.True(initialized && packets && restoredMid && completed);
            Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == 300110);
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "party");
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
            independent = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json")));
            battle.Tick();
            independent.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(independent, 320, 224).ToArray());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "checked", Effect = 118, Callbacks = callbacks + 2, ContactAmount = contact.Primary.Amount6, TargetHpAfterFormula = targetHp, FinalStock = 1, OrdinaryActionComplete = true, MidAndCompletedNextCallbacks = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Callbacks = callbacks, FirstBlocker = error.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            throw;
        }
    }
}
