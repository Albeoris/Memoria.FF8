using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentTerminalTextureTests
{
    [Fact]
    public void Installed_effect_101_component_fork_activates_its_authored_Droma()
    {
        const ushort effect = 101;
        string directory = TestResources.GameDirectory;
        GameSession producerSession = GameWorld.CreateColdProcess(directory);
        producerSession.StartNewGame();
        using GameWorld producer = new(producerSession, directory);
        BattleSession source = PrepareTraumaComponent(producer, effect);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        BattleCombatant dormant = Assert.Single(source.Combatants, actor => !actor.IsPlayer && actor.ModelId == 110 && actor.IsActive);
        Assert.True(dormant.Hidden && dormant.Suspended && dormant.Untargetable);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)290, admission.Action);
        string sha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(admission)));
        using GameWorld world = new(GameWorld.CreateColdProcess(directory), directory);
        BattleSession active = world.StartEffectPreview(new(1, 0, effect), new(source, sha256));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(active.Presentation);
        BattleCombatant droma = Assert.Single(active.Combatants, actor => actor.Id == dormant.Id);
        Assert.NotSame(source.Presentation, process);
        Assert.NotSame(dormant, droma);
        Assert.Equal(admission, GameWorld.ValidateEffectComponentSource(active, effect));

        List<BattleDiagnosticEffectDispatch> dispatches = [];
        active.DiagnosticEffectDispatched += dispatches.Add;
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        int initialEvents = active.Events.Count;
        for (int callback = 0; callback < 900 && droma.Suspended; callback++)
        {
            active.Tick();
        }

        BattleDiagnosticEffectDispatch dispatched = Assert.Single(dispatches, observation => observation.RequestedEffect == effect);
        Assert.Equal(effect, dispatched.ExecutedEffect);
        Assert.Equal(290, dispatched.ActionId);
        Assert.Equal((byte)2, dispatched.NativeAiCallbackStage);
        Assert.Contains(droma.Id, dispatched.ContactActorIds);
        Assert.True(droma.IsAlive && !droma.Hidden && !droma.Suspended && !droma.Untargetable);
        Assert.Equal(dormant.CurrentHp, droma.CurrentHp);
        Assert.True(dormant.Hidden && dormant.Suspended && dormant.Untargetable);
        Assert.Contains(active.Events.Skip(initialEvents), entry => entry.Kind == "Activated" && entry.ActorId == droma.Id);
        Assert.DoesNotContain(active.Events.Skip(initialEvents), entry => entry.Kind == "EffectContact" && entry.ActorId == dispatched.ActorId && entry.TargetId == droma.Id);
    }

    private static BattleSession PrepareTraumaComponent(GameWorld world, ushort effect)
    {
        const ushort formation = 431;
        DiagnosticBattleRoster roster = new([], 1) { DonorCount = 0, AuthoredFormationHash = world.BattleResources.FormationSourceHash(formation) };
        BattleSession battle = world.StartDiagnosticBattle(new(formation, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Roster = roster });
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant trauma = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
        Assert.Equal(109, trauma.ModelId);
        foreach (BattleCombatant ally in battle.Combatants.Where(actor => actor.IsPlayer))
        {
            ally.Atb = 0;
        }
        int firstEvent = battle.Events.Count;
        for (int callback = 0; callback < 1200; callback++)
        {
            if (battle.ObserveDiagnosticDrainBlockers().Count == 0 && !battle.Events.Skip(firstEvent).Any(entry => entry.Kind == "NativeSpawn" && entry.ActorId == trauma.Id))
            {
                trauma.Atb = BattleSession.AtbMaximum;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (process.ActiveActionToken is not null && battle.ActiveAction is { ExpandedAiCallback.Stage: 2 } &&
                process.Graphics.Descriptors.Queue.Any(slot => process.Graphics.Descriptors.Packets[slot] is { Command: 104, Header: { } header } && process.ActionPackets.ReadHeader(header).Effect == effect))
            {
                return battle;
            }
        }
        throw new InvalidDataException("Installed Trauma did not admit its authored action290 against dormant Droma.");
    }
}
