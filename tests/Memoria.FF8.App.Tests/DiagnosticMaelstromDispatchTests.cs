using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticMaelstromDispatchTests
{
    [Fact]
    public void Original_Ultimecia_Maelstrom_dispatches_after_its_actual_initialization()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original Maelstrom admission.");
        BattleDiagnosticEnemyAbility selection = new(121, 0, 0, 1, 8, 365, 11, 204);
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: "enemy-0", TargetId: "party-0", Ability: BattleAbility.EnemyAbility, ActionId: 365), 207)
        {
            EnemyAbility = selection,
            ExpectedNativeAction = 365,
            ExpectedNativeCommand = 8
        };
        DiagnosticBattleRequest request = new(511, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [new(0, 0, [], [0]) { MagicCommand = new(0, 20), HitPoints = 486, Level = 7 }], true),
            Target = new("enemy-0", 142, 121, 0, 3, 195, 1, null, null),
            Driver = driver,
            PrepareEntryBeforePresentation = true
        };
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        world.PrepareDiagnosticEntryBeforePresentation(request);
        for (int frame = 0; frame < 6; frame++)
        {
            battle.Tick();
            audio.Update(battle);
        }
        Assert.True(battle.CanSubmitDiagnosticEnemyAbility("enemy-0", selection));
        bool dispatched = false;
        battle.DiagnosticEffectDispatched += value => dispatched |= value.RequestedEffect == 207 && value.ActorId == "enemy-0";
        battle.SubmitDiagnosticEnemyAbility("enemy-0", selection, 207);
        for (int callback = 0; callback < 300 && !dispatched; callback++)
        {
            battle.Tick();
            audio.Update(battle);
        }
        if (!dispatched)
        {
            NativeBattleGraphicsState graphics = battle.NativeGraphics!;
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleGraphicsResource resource = snapshot.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == "party-0").Resource;
            NativeBattleGraphicsModel party = graphics.Models[0];
            byte sequence = party.Sequence!.Value;
            string bytes = Convert.ToHexString(resource.SequenceSection.AsSpan(resource.SequenceStart(sequence), resource.SequenceEnd(sequence) - resource.SequenceStart(sequence)));
            Console.WriteLine(JsonSerializer.Serialize(new { battle.Frame, battle.NativeHostCallbacks, party.Sequence, party.ResourceFlags44, State = party.SequenceState is { } state ? new { state.Offset46, state.Wait1, state.Pending3, state.Callback42, state.Pose, state.PoseFrame114 } : null, bytes, owners = battle.ObserveDiagnosticInvocationOwners() }));
        }
        Assert.True(dispatched, "Original command8/action365/sequence11 must dispatch207 after readiness without a synthetic sequence/status repair.");
        world.EndDiagnosticBattle();
    }
}
