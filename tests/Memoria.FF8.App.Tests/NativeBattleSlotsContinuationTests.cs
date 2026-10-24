using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSlotsContinuationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Installed_magic_group_queues_one_graphical_invocation_for_its_factual_headers(int casts)
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_SLOTS_SNAPSHOT");
        Assert.SkipUnless(File.Exists(input), "Select a standalone battle save with a ready caster and Fire stock.");
        BattleSnapshot initial = Assert.IsType<BattleSnapshot>(GameSession.Load(input!).Battle);
        string actorId = initial.Definition.Combatants.Single(actor => actor.IsPlayer && actor.ModelId == 5).Id;
        //Controlled component: grant Double/Triple and use raw personal stock rather than shared encounter reservations.
        BattleCombatantSnapshot[] actors = initial.Combatants.Select(actor => actor.Id == actorId ? actor with { NativeStatus = actor.NativeStatus! with { Battle = actor.NativeStatus.Battle | 0x60000 } } : actor).ToArray();
        BattleSession battle = BattleSession.Restore(initial with { Combatants = actors, MagicVitals = null, Definition = initial.Definition with { MagicVitals = null } });
        string target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive).Id;
        battle.QueueAction(actorId, target, BattleAbility.Magic, 1, magicTargets: Enumerable.Repeat(target, casts).ToArray());
        battle.Tick();
        BattleSnapshot produced = battle.Capture();
        Assert.Equal(casts, produced.NativeActionPackets!.ActionCount);
        Assert.Equal(casts - 1, produced.NativeActionPackets.Headers[0].ReadByte(17));
        NativeBattleDescriptorState descriptors = produced.NativeGraphics!.Descriptors;
        Assert.Single(descriptors.Queue.Where(index => descriptors.Packets[index].Command == 104));
        Assert.Equal(JsonSerializer.Serialize(produced), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(produced))!).Capture()));
    }

    [Fact]
    public void Selected_standalone_executes_only_its_accepted_slots_casts_and_completes()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_SLOTS_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_SLOTS_EVIDENCE");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && Directory.Exists(directory), "Select a standalone Slots save, installed resources and evidence directory.");
        GameState source = GameSession.Load(input!);
        BattleSnapshot initial = Assert.IsType<BattleSnapshot>(source.Battle);
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleScenePresentation scene = new(assets, initial.Definition, true);
        BattleSession battle = Restore(initial);
        Directory.CreateDirectory(evidence!);
        Console.WriteLine($"Slots source field {source.FieldId}, battle frame {initial.Frame}");
        BattleCombatant actor = battle.Combatants.Single(combatant => combatant.IsPlayer && combatant.ModelId == 5);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal(actor.Id);
        Console.WriteLine(JsonSerializer.Serialize(deal));
        int selection = Enumerable.Range(0, deal.Candidates.Length).First(index => deal.Candidates[index] == new BattleNativeSlotsRoll(1, 3));
        Assert.True(battle.SelectNativeSlotsCard(deal.Id, selection));
        Assert.True(battle.SubmitNativeSlotsCard(deal.Id));
        File.WriteAllBytes(Path.Combine(evidence!, "accepted.battle.json"), JsonSerializer.SerializeToUtf8Bytes(battle.Capture()));
        using StreamWriter trace = new(Path.Combine(evidence!, "callbacks.jsonl")) { AutoFlush = true };
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        BattleSnapshot before = battle.Capture();
        bool restoredMidpoint = false;
        bool produced = false;
        try
        {
            for (int count = 0; count < 2048; count++)
            {
                before = battle.Capture();
                battle.AdvanceNativeGraphicsPresentation(callback);
                BattleSnapshot current = battle.Capture();
                if (!produced && current.NativeActionPackets!.ActionCount != 0)
                {
                    Assert.Equal(3, current.NativeActionPackets.ActionCount);
                    Assert.Equal(2, current.NativeActionPackets.Headers[0].ReadByte(17));
                    NativeBattleDescriptorState descriptors = current.NativeGraphics!.Descriptors;
                    Assert.Single(descriptors.Queue.Where(index => descriptors.Packets[index].Command == 104));
                    produced = true;
                }
                if (current.NativeGraphics?.ActionGraphics is { } context && current.NativeActionPackets!.Headers[context.HeaderIndex].ReadByte(1) == 16)
                {
                    Assert.Equal(0, context.HeaderIndex);
                }
                if (count % 8 == 0)
                {
                    trace.WriteLine(JsonSerializer.Serialize(new { count, current.Frame, current.Outcome, ActiveHeader = current.NativeGraphics?.ActionGraphics?.HeaderIndex, Headers = current.NativeActionPackets?.ActionCount, DescriptorQueue = current.NativeGraphics?.Descriptors.Queue.Select(index => current.NativeGraphics.Descriptors.Packets[index].Command).ToArray() }));
                }
                if (!restoredMidpoint && current.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: not 0, Parameters.HeaderIndex7C: 0 })
                {
                    string midpoint = Path.Combine(evidence!, "midpoint.battle.json");
                    File.WriteAllBytes(midpoint, JsonSerializer.SerializeToUtf8Bytes(current));
                    BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(midpoint))!);
                    BattleSession direct = Restore(current);
                    cold.AdvanceNativeGraphicsPresentation(callback);
                    direct.AdvanceNativeGraphicsPresentation(callback);
                    Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(scene.Render(direct, 320, 224).ToArray(), scene.Render(cold, 320, 224).ToArray());
                    battle = cold;
                    restoredMidpoint = true;
                }
                if (battle.Outcome != BattleOutcome.Active)
                {
                    current = battle.Capture();
                    File.WriteAllBytes(Path.Combine(evidence!, "completed.battle.json"), JsonSerializer.SerializeToUtf8Bytes(current));
                    Assert.Equal(BattleOutcome.Victory, battle.Outcome);
                    Assert.True(produced);
                    Assert.True(restoredMidpoint);
                    using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(scene.Render(battle, 640, 448).ToArray(), 640, 448);
                    bitmap.Save(Path.Combine(evidence!, "completed.png"));
                    Console.WriteLine($"Completed battle frame {current.Frame}, callback {current.NativeGraphics?.Callback}");
                    return;
                }
            }
            Assert.Fail("Slots did not complete within its bounded continuation.");
        }
        catch
        {
            File.WriteAllBytes(Path.Combine(evidence!, "before-boundary.battle.json"), JsonSerializer.SerializeToUtf8Bytes(before));
            File.WriteAllBytes(Path.Combine(evidence!, "failed-live.battle.json"), JsonSerializer.SerializeToUtf8Bytes(battle.Capture()));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }
    }
}
