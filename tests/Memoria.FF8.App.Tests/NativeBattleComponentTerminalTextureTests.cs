using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentTerminalTextureTests
{
    [Fact]
    public void Installed_effect_101_keeps_its_terminal_resident_texture_through_private_forks()
    {
        const ushort effect = 101;
        string directory = TestResources.GameDirectory;
        GameSession producerSession = GameWorld.CreateColdProcess(directory);
        producerSession.StartNewGame();
        using GameWorld producer = new(producerSession, directory);
        BattleSession source = PrepareTraumaComponent(producer, effect);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)290, admission.Action);
        string sha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(admission)));
        using GameWorld world = new(GameWorld.CreateColdProcess(directory), directory);
        BattleSession active = world.StartEffectPreview(new(1, 0, effect), new(source, sha256));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(active.Presentation);
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        active.DiagnosticEffectDispatched += dispatches.Add;
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        for (int callback = 0; callback < 900; callback++)
        {
            NativeBattleGraphicsState graphicsBefore = process.Graphics;
            NativeBattleEffectMemory memoryBefore = process.Memory;
            BattleComponentTextureQuiescenceObservation? observation = active.ObserveComponentTerminalTextures(effect);
            Assert.Same(graphicsBefore, process.Graphics);
            Assert.Same(memoryBefore, process.Memory);
            if (observation is not null)
            {
                Assert.Equal((ushort)290, Assert.Single(dispatches, dispatch => dispatch.RequestedEffect == effect).ActionId);
                Assert.True(observation.IsEligible);
                Assert.True(observation.Textures.IsTerminalQuiescent);
                Assert.All(observation.Textures.Tasks, row => Assert.False(string.IsNullOrWhiteSpace(row.ModelActorId)));
                Assert.Equal(new[] { "effect-or-auxiliary-task" }, active.ObserveDiagnosticDrainBlockers());
                BattleSession first = active.ForkForContinuation();
                BattleSession second = active.ForkForContinuation();
                byte[] vram = process.Memory.Textures!.ReadVram();
                for (int tail = 0; tail < 3; tail++)
                {
                    first.Tick();
                    second.Tick();
                    AssertTail(first);
                    AssertTail(second);
                    Assert.Equal(first.Presentation!.Graphics.Callback, second.Presentation!.Graphics.Callback);
                }
                Assert.Equal(observation.ResidentFingerprint, active.ObserveComponentTerminalTextures(effect)!.ResidentFingerprint);
                return;

                void AssertTail(BattleSession retained)
                {
                    BattleComponentTextureQuiescenceObservation next = Assert.IsType<BattleComponentTextureQuiescenceObservation>(retained.ObserveComponentTerminalTextures(effect));
                    Assert.Equal(observation.ResidentFingerprint, next.ResidentFingerprint);
                    Assert.True(next.GraphicsCallback > observation.GraphicsCallback);
                    Assert.Equal(vram, retained.Presentation!.Memory.Textures!.ReadVram());
                    Assert.Equal(new[] { "effect-or-auxiliary-task" }, retained.ObserveDiagnosticDrainBlockers());
                }
            }
            active.Tick();
            Assert.Empty(process.LastStep.Contacts);
        }
        Assert.Fail($"Installed effect101 did not reach the resident texture boundary after {process.Graphics.Callback} graphics callbacks. Current blockers: {string.Join(", ", active.ObserveDiagnosticDrainBlockers())}.");
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
