using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_Doom_expiration_dispatches_effect_nine_once_per_recipient()
    {
        string directory = TestResources.GameDirectory;
        (string status, string detail) = RunExpandedDoomExpiration(directory, PrepareField(directory));
        Assert.Equal("native", status);
        Assert.Contains("246/43", detail);
    }

    private static (string Status, string Detail) RunExpandedDoomExpiration(string directory, GameState field)
    {
        GameSession original = null!;
        using GameWorld entry = StartBattle(directory, field, 0, captureSession: session => original = session);
        GameState state = original.PrepareSnapshot();
        BattleSnapshot snapshot = Assert.IsType<BattleSnapshot>(state.Battle);
        string[] recipients = ["party-0", "party-10"];
        var actors = snapshot.Combatants.Select(actor =>
        {
            if (!recipients.Contains(actor.Id))
            {
                return actor;
            }
            NativeBattleStatusSnapshot status = actor.NativeStatus!;
            status.Timers[10] = 2;
            return actor with { NativeStatus = status with { Battle = status.Battle | 0x400 } };
        }).ToArray();
        string checkpoint = TestResources.ArtifactFile($"{Guid.NewGuid():N}.battle.json");
        File.WriteAllText(checkpoint, JsonSerializer.Serialize(state with { Battle = snapshot with { Combatants = actors } }));
        GameSession resumed = new(GameSession.Load(checkpoint));
        using GameWorld world = new(resumed, directory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.All(battle.Capture().Combatants.Where(actor => recipients.Contains(actor.Id)), actor => Assert.Equal(2, actor.NativeStatus!.Timers[10]));
        int before = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
        List<string> casts = [];
        NativeBattleEffectPresentation? effect = null;
        for (int frame = 0; frame < 600 && casts.Count <= recipients.Length; frame++)
        {
            if (battle.ExpandedEffect is { } live && live != effect)
            {
                effect = live;
                BattleActionPresentation command = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
                Assert.Equal((byte)246, command.NativeQueuedCommand);
                Assert.Equal(43, command.ActionId);
                Assert.Equal((ushort)9, live.Graphics.OriginalEffect!.RequestedEffect);
                Assert.Equal(command.ActorId, command.TargetId);
                Assert.False(battle.Combatants.Single(actor => actor.Id == command.ActorId).IsAlive);
                casts.Add(command.ActorId);
            }
            if (casts.Count == recipients.Length && battle.ExpandedEffect is null)
            {
                break;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Equal(recipients, casts);
        Assert.Null(battle.ExpandedEffect);
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
        return ("native", "Original Doom expiry dispatched command 246/43 and completed with one zero-amount death contact per recipient.");
    }
}
