using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_Doom_expiration_dispatches_effect_nine_once_per_recipient()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        (string status, string detail) = RunExpandedDoomExpiration(directory, PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        Assert.Equal("native", status);
        Assert.Contains("246/43", detail);
    }

    private static (string Status, string Detail) RunExpandedDoomExpiration(string directory, GameState field)
    {
        GameSession session = new(field with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, current));
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int frame = 0; frame < 900 && (!process.EntryReady || battle.QueuedActions.Count != 0 || battle.ActiveAction is not null); frame++)
        {
            foreach (BattleCombatant actor in battle.Combatants)
            {
                actor.Atb = 0;
            }
            battle.Tick();
        }
        Assert.True(process.EntryReady);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        string[] recipients = ["party-0", "party-3"];
        var original = battle.Combatants.Where(actor => recipients.Contains(actor.Id))
            .ToDictionary(actor => actor.Id, actor => (actor.CurrentHp, actor.PersistentStatus));
        List<BattleDiagnosticResolution> resolutions = [];
        battle.DiagnosticResolution += resolutions.Add;
        foreach (BattleCombatant actor in battle.Combatants.Where(actor => recipients.Contains(actor.Id)))
        {
            actor.NativeStatus1 |= 0x400;
            actor.NativeStatusTimers[10] = 0;
        }
        int before = battle.Events.Count;
        List<string> casts = [];
        HashSet<long> observed = [];
        for (int frame = 0; frame < 1800 && casts.Count <= recipients.Length; frame++)
        {
            if (battle.Presentation is { ActiveActionToken: long token, Graphics.OriginalEffect.ReturnedDescriptor: > 0 } live && observed.Add(token))
            {
                BattleActionPresentation command = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
                Assert.Equal((byte)246, command.NativeQueuedCommand);
                Assert.Equal(43, command.ActionId);
                Assert.Equal((ushort)9, live.Graphics.OriginalEffect!.RequestedEffect);
                Assert.Equal(command.ActorId, command.TargetId);
                Assert.False(battle.Combatants.Single(actor => actor.Id == command.ActorId).IsAlive);
                casts.Add(command.ActorId);
            }
            if (casts.Count == recipients.Length && battle.Presentation?.ActiveActionToken is null)
            {
                break;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Equal(recipients, casts);
        Assert.Null(battle.Presentation?.ActiveActionToken);
        BattleEvent[] events = battle.Events.Skip(before).ToArray();
        foreach (string recipient in recipients)
        {
            BattleEvent contact = Assert.Single(events, entry => entry.Kind == "EffectContact" && entry.TargetId == recipient);
            Assert.Equal(recipient, contact.ActorId);
            Assert.Equal(0, contact.Amount);
            Assert.Single(events, entry => entry.Kind == "Defeated" && entry.TargetId == recipient);
            Assert.Single(events, entry => entry.Kind == "ActionComplete" && entry.ActorId == recipient);
        }
        BattleSnapshot completed = battle.Capture();
        Assert.All(completed.Combatants.Where(actor => recipients.Contains(actor.Id)), actor => Assert.Equal(-1111, actor.NativeStatus!.Timers[10]));
        BattleDiagnosticResolution[] periodic = resolutions.Where(receipt => receipt.PeriodicStatus is not null).ToArray();
        Assert.Equal(recipients, periodic.Select(receipt => receipt.ActualTargetId));
        foreach (BattleDiagnosticResolution resolution in periodic)
        {
            BattleDiagnosticPeriodicStatus proof = Assert.IsType<BattleDiagnosticPeriodicStatus>(resolution.PeriodicStatus);
            (int hp, ushort statuses) = original[proof.RecipientId];
            Assert.Null(resolution.ActorId);
            Assert.Equal(proof.RecipientId, resolution.OriginalTargetId);
            Assert.Equal(proof.RecipientId, resolution.ActualTargetId);
            Assert.Equal(proof.RecipientId == recipients[0] ? (byte?)0 : null, proof.OriginalRecipientOperand);
            Assert.Equal("damage", resolution.Kind);
            Assert.Equal(hp, proof.ResolvedAmount);
            Assert.Equal(statuses, proof.SavedStatuses);
            Assert.Equal(0x483470u, proof.ProducerAddress);
            Assert.Equal(10, proof.StatusBit);
            Assert.Equal(0, proof.TimerBefore);
            Assert.Equal(-1111, proof.TimerAfter);
            BattleDiagnosticResolution restored = JsonSerializer.Deserialize<BattleDiagnosticResolution>(JsonSerializer.Serialize(resolution))!;
            Assert.Equal(proof, restored.PeriodicStatus);
            JsonObject missing = JsonSerializer.SerializeToNode(proof)!.AsObject();
            missing.Remove(nameof(BattleDiagnosticPeriodicStatus.TimerBefore));
            Assert.Throws<JsonException>(() => missing.Deserialize<BattleDiagnosticPeriodicStatus>());
        }
        return ("native", "Original Doom expiry dispatched command 246/43 and completed with one zero-amount death contact per recipient.");
    }
}
