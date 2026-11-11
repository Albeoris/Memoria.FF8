using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverRenzokukenHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Original_Renzokuken_returns_one_FFVII_turn_after_cold_timing_and_B7(bool automatic, bool finisher)
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals();
        origin = origin.WithSavedHitPoints(origin.FindPhysicalRow(0), 1);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(origin, assets);
        ushort options = game.ReadNewGameLimitProgress().Options978!.Value;
        if (automatic)
        {
            options |= 1;
        }
        NativeBattleUiRandomState uiRandom = NativePlaytimeState.FromColdProcess().CreateBattleUiRandom();
        NativeGlobalRandomState global = game.ReadNewGameGlobalRandom();
        NativeCrtRandomState crt = NativeCrtRandomState.FromColdProcess();
        Ff7BattleSession host = Host(1, null);
        Ff7BattleAiActor source = host.Actors.Single(actor => actor.Actor.Slot == 0).Actor with { HitPoints = kit.HitPoints, MaxHitPoints = (uint)kit.MaximumHitPoints };
        long request = finisher ? 1564 : 1;
        CrossoverNativeRenzokukenSelection selection = CrossoverNativeRenzokukenSelection.Begin(request, formation.FormationId).ObserveReady(0, source, 0, false, kit);
        Assert.True(selection.Crisis > 0);
        _ = selection.Select(kit.FinisherFlags, out CrossoverNativeRenzokukenPlan plan);
        Assert.Equal(finisher, plan.Finisher == 0);
        host = Host(request, null);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, kit.RecordRow, kit.HitPoints, checked((ushort)kit.MaximumHitPoints), 6, 14, 24, 17));
        CrossoverGunbladeHostAttack attack = Attack(null);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions actions = Actions(null);
        host.BindGuestAttack(actions);
        for (int tick = 0; tick < 1000 && (host.ReadyPlayer != 0 || !actions.LimitReady); tick++)
        {
            host.Advance();
        }
        Assert.True(actions.LimitReady);
        Assert.Equal(0, host.ReadyPlayer);
        Assert.True(host.SubmitGuestLimit(0, 4));
        List<long> numeric = [];
        List<long> visible = [];
        bool pendingCold = false;
        bool shownCold = false;
        int renderedPackets = 0;
        int roughPackets = 0;
        bool roughCold = false;
        bool sawAutomatic = false;
        bool dispatched = false;
        for (int tick = 0; tick < 1800 && (!dispatched || host.Action?.GuestLimit is not null); tick++)
        {
            if (automatic && actions.RenzokukenTrigger?.Widget is { } widget)
            {
                Assert.False(widget.Visible);
                Assert.False(actions.RenzokukenTrigger.TriggerWindowOpen);
                sawAutomatic = true;
            }
            if (!automatic && actions.RenzokukenTrigger?.TriggerWindowOpen == true)
            {
                actions.PressTrigger();
            }
            host.Advance();
            dispatched |= host.Action?.GuestLimit is not null;
            if (host.LastHit is { Source: 0, Target: 4 } hit && hit.Tick == host.Tick)
            {
                numeric.Add(hit.Tick);
                if (!pendingCold)
                {
                    Assert.NotNull(actions.Capture().Limit!.Program.PreparedContact);
                    Assert.True(host.DisplayedHit is null || host.DisplayedHit.Tick < hit.Tick);
                    Cold();
                    pendingCold = true;
                }
            }
            if (host.DisplayedHit is { Source: 0, Target: 4 } display && display.Tick == host.Tick)
            {
                visible.Add(display.Tick);
                if (!shownCold)
                {
                    Assert.Null(actions.Capture().Limit!.Program.PreparedContact);
                    Cold();
                    shownCold = true;
                }
            }
            renderedPackets = Math.Max(renderedPackets, actions.EffectVisual?.Ordering?.Packets.Count ?? 0);
            if (finisher && actions.Capture().Limit?.Rough is { } rough)
            {
                roughPackets = Math.Max(roughPackets, actions.EffectVisual?.Ordering?.Packets.Count ?? 0);
                if (!roughCold && rough.ContactPublished)
                {
                    Cold();
                    roughCold = true;
                }
            }
        }
        int expected = (plan.Variant == 141 ? 5 : 4) + (finisher ? 1 : 0);
        Assert.Equal(expected, numeric.Count);
        Assert.Equal(expected, visible.Count);
        Assert.All(numeric.Zip(visible), pair => Assert.True(pair.First < pair.Second));
        Assert.True(pendingCold && shownCold);
        Assert.True(renderedPackets > 69);
        Assert.Equal(automatic, sawAutomatic);
        Assert.Equal(finisher, roughCold);
        if (finisher)
        {
            Assert.True(roughPackets > 100);
            Assert.True(visible[^1] - numeric[^1] > 100);
        }
        Assert.Null(actions.Invocation);
        Assert.Null(host.Action?.GuestLimit);
        Assert.Equal(Ff7BattleOutcome.Fighting, host.Outcome);
        Assert.True(host.Actors.Single(actor => actor.Actor.Slot == 5).Actor.HitPoints > 0);
        Assert.Equal(0u, host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
        Assert.Equal(1u, host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.Statuses & 1);
        Assert.Equal((byte)1, actions.Visual!.Model.Sequence);
        Assert.True(actions.PeekSoundRequests().Length >= 2);
        output.WriteLine($"Original formation300 request{request} Renzokuken{plan.Variant}: numeric {string.Join(',', numeric)}, B7 {string.Join(',', visible)}, peak {renderedPackets} source packets (Rough {roughPackets}), auto {automatic}, cold around two sides of presentation, native first MP KO, turn release and standing.");

        Ff7BattleSession Host(long id, Ff7BattleSnapshot? saved) => new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, id, requestFlags: 4, snapshot: saved, animations: animations);
        CrossoverGunbladeHostAttack Attack(CrossoverGunbladeHostSnapshot? saved) => new(roster, kit, assets, game, global, () => host.CameraOutput, _ => 128, saved);
        CrossoverGuestHostActions Actions(CrossoverGuestHostActionsSnapshot? saved) => new(roster, kit, origin, crt, attack, host, game, assets, saved, originLimitOptions: options, originLimitUiRandom: uiRandom);

        void Cold()
        {
            host = Host(request, RoundTrip(host.Capture()));
            attack = Attack(RoundTrip(attack.Capture()));
            actions = Actions(RoundTrip(actions.Capture()));
            host.BindGuestAttack(actions);
        }
    }

    private static T RoundTrip<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;
}
