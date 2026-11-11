using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverOpeningCombatTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_opening_formations_reach_victory_with_a_cold_restored_guest_turn()
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
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        IReadOnlyList<Ff7BattleAttack> limits = Ff7PcExecutableData.LoadBattleLimits(installation);
        IReadOnlyList<byte> reel = Ff7PcExecutableData.LoadBeatRushReel(installation);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals();
        GameplaySaveEconomy economy = GameplaySaveEconomy.Capture(game.ReadNewGameEconomy());
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        int[] selection = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, .. Enumerable.Range(392, 4)];
        foreach (int formationId in selection)
        {
            SceneFormation formation = formations.Single(item => item.FormationId == formationId);
            int[] party = formationId switch
            {
                < 305 or 328 or 329 or 330 => [0, 255, 255],
                < 325 or >= 392 => [0, 1, 255],
                364 => [1, 0, 2],
                _ => [0, 1, 2]
            };
            Ff7Progress progress = original.Progress with { Party = party };
            if (formationId is >= 332 and < 392)
            {
                Ff7InventorySlot[] inventory = Ff7NativeInventory.Acquire(progress.Inventory, 10 << 9);
                progress = progress with { Inventory = Ff7NativeInventory.Acquire(inventory, 3 << 9 | 7) };
            }
            int guestSlot = Array.IndexOf(party, 0);
            Ff7BattleSession host = Create();
            CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(guestSlot, 0, 0)], []);
            Ff7BattleGuestVitalsBinding binding = new(roster.EncounterId, new('A', 64), guestSlot, 0, 0, kit.RecordRow, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17);
            host.BindGuestVitals(binding);
            CrossoverGunbladeHostAttack guest = CreateGuest();
            guest.PrepareStanding(host.VisualPlacement(guestSlot));
            CrossoverGuestHostActions actions = CreateActions();
            host.BindGuestAttack(actions);
            bool restored = false;
            bool contact = false;
            for (int tick = 0; tick < 40000 && !host.ResultReady; tick++)
            {
                if (host.ReadyPlayer is int slot && Select(host, slot, guestSlot, contact) is { } picked)
                {
                    (Ff7BattleCommandOption option, ushort mask) = picked;
                    Assert.True(host.Submit(slot, option.Command, option.Argument, mask), $"Formation {formationId} rejected {option.Command}/{option.Argument} from slot {slot}.");
                }
                if (guest.TriggerWindowOpen)
                {
                    if (!restored)
                    {
                        Ff7BattleSnapshot native = RoundTrip(host.Capture());
                        CrossoverGunbladeHostSnapshot presentation = RoundTrip(guest.Capture());
                        CrossoverGuestHostActionsSnapshot continuation = RoundTrip(actions.Capture());
                        host = Create(native);
                        guest = CreateGuest(presentation);
                        actions = CreateActions(continuation);
                        host.BindGuestAttack(actions);
                        restored = true;
                    }
                    guest.PressTrigger();
                }
                host.Advance(confirmLimit: host.BeatRushSelecting);
                contact |= guest.ContactPublished;
                if (actions.PeekSoundRequests() is { Length: > 0 } sounds)
                {
                    actions.AcknowledgeSoundRequests(sounds[^1].Ordinal);
                }
            }
            Assert.True(contact, $"Formation {formationId} never published a Gunblade contact.");
            Assert.True(restored, $"Formation {formationId} never reached its original trigger window.");
            Assert.Null(actions.Invocation);
            Assert.True(host.ResultReady && host.Outcome == Ff7BattleOutcome.Victory, $"Formation {formationId} ended with {host.Outcome} at {host.Tick}: {host.LastHit}. Active {host.Action?.Command}; ready {host.ReadyPlayer}; party and enemies {string.Join(", ", host.Actors.Select(actor => $"{actor.Actor.Slot}:{actor.Actor.HitPoints}/{actor.Actor.Statuses:X}/{actor.Actor.Conditions:X}/{actor.Gauge}"))}.");

            Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: snapshot, animations: animations, limits: limits, beatRushSymbols: reel);
            CrossoverGunbladeHostAttack CreateGuest(CrossoverGunbladeHostSnapshot? snapshot = null) => new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, snapshot);
            CrossoverGuestHostActions CreateActions(CrossoverGuestHostActionsSnapshot? snapshot = null) => new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), guest, host, game, assets, snapshot, economy);
        }
        output.WriteLine($"Original mixed combat and trigger-window cold restore reached victory in all {selection.Length} opening formations.");
    }

    [Fact]
    public void Original_Renzokuken_reaches_every_opening_enemy_prototype_and_arena()
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
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals();
        origin = origin.WithSavedHitPoints(origin.FindPhysicalRow(0), 80);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        ushort options = game.ReadNewGameLimitProgress().Options978!.Value;
        int[] selection = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, .. Enumerable.Range(392, 4)];
        HashSet<int> prototypes = [];
        HashSet<short> arenas = [];
        int turns = 0;
        int roughTurns = 0;
        foreach (int formationId in selection)
        {
            SceneFormation formation = formations.Single(item => item.FormationId == formationId);
            if (arenas.Contains(formation.LocationId) && formation.Enemies.All(enemy => enemy.EnemyId < 0 || prototypes.Contains(enemy.EnemyId)))
            {
                continue;
            }
            int[] party = formationId switch
            {
                < 305 or 328 or 329 or 330 => [0, 255, 255],
                < 325 or >= 392 => [0, 1, 255],
                364 => [1, 0, 2],
                _ => [0, 1, 2]
            };
            Ff7Progress progress = original.Progress with { Party = party };
            int guestSlot = Array.IndexOf(party, 0);
            Ff7BattleSession host = Create(1);
            Ff7BattleAiActor source = host.Actors.Single(actor => actor.Actor.Slot == guestSlot).Actor with { HitPoints = kit.HitPoints, MaxHitPoints = (uint)kit.MaximumHitPoints };
            Ff7BattleActorState[] targets = host.Actors.Where(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints > 0 && (actor.Actor.Conditions & 8) != 0).DistinctBy(actor => actor.Identity).ToArray();
            foreach (Ff7BattleActorState target in targets)
            {
                if (arenas.Contains(formation.LocationId) && prototypes.Contains(target.Identity))
                {
                    continue;
                }
                bool finisher = (turns & 1) != 0;
                long request = 1;
                CrossoverNativeRenzokukenPlan plan;
                for (;; request++)
                {
                    CrossoverNativeRenzokukenSelection ready = CrossoverNativeRenzokukenSelection.Begin(request, formationId).ObserveReady(0, source, 0, false, kit);
                    if (ready.Crisis > 0)
                    {
                        _ = ready.Select(kit.FinisherFlags, out plan);
                        if (finisher == (plan.Finisher == 0))
                        {
                            break;
                        }
                    }
                    Assert.True(request < 256, $"Formation {formationId} did not provide the diagnostic native Limit selection.");
                }
                host = Create(request);
                CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(guestSlot, 0, 0)], []);
                host.BindGuestVitals(new(roster.EncounterId, new('A', 64), guestSlot, 0, 0, kit.RecordRow, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17));
                CrossoverGunbladeHostAttack guest = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.CameraOutput, _ => 128);
                guest.PrepareStanding(host.VisualPlacement(guestSlot));
                CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), guest, host, game, assets, originLimitOptions: options);
                host.BindGuestAttack(actions);
                int targetSlot = target.Actor.Slot;
                for (int tick = 0; tick < 2000 && (host.ReadyPlayer != guestSlot || !actions.LimitReady); tick++)
                {
                    if (host.ReadyPlayer is int ally && ally != guestSlot)
                    {
                        Assert.True(host.Submit(ally, Ff7BattleCommand.Defend, 0, (ushort)(1 << ally)));
                    }
                    host.Advance();
                }
                Assert.True(host.SubmitGuestLimit(guestSlot, targetSlot), $"Formation {formationId} prototype {target.Identity} did not admit its selected native Limit turn at {host.Tick}: {host.Outcome}, ready {host.ReadyPlayer}, Limit {actions.LimitReady}, selected {plan}, actors {string.Join(',', host.Actors.Select(actor => (actor.Actor.Slot, actor.Actor.HitPoints, actor.Actor.Statuses, actor.Actor.Conditions)))}.");
                bool dispatched = false;
                int contacts = 0;
                int impacts = 0;
                int packets = 0;
                bool usedLease = false;
                for (int tick = 0; tick < 2000 && (!dispatched || host.Action?.GuestLimit is not null); tick++)
                {
                    if (actions.RenzokukenTrigger?.TriggerWindowOpen == true)
                    {
                        actions.PressTrigger();
                    }
                    host.Advance();
                    dispatched |= host.Action?.GuestLimit is not null;
                    if (host.LastHit is { } hit && hit.Source == guestSlot && hit.Target == targetSlot && hit.Tick == host.Tick)
                    {
                        contacts++;
                    }
                    if (host.DisplayedHit is { } visible && visible.Source == guestSlot && visible.Target == targetSlot && visible.Tick == host.Tick)
                    {
                        impacts++;
                    }
                    packets = Math.Max(packets, actions.EffectVisual?.Ordering?.Packets.Count ?? 0);
                    usedLease |= actions.LeasedCamera is not null;
                }
                int expected = (plan.Variant == 141 ? 5 : 4) + (finisher ? 1 : 0);
                Assert.True((contacts, impacts) == (expected, expected) && usedLease && packets > 0 && host.Action?.GuestLimit is null && actions.Invocation is null, $"Formation {formationId} prototype {target.Identity}, arena {formation.LocationId}, original {plan.Variant}/{plan.Finisher}: {contacts}/{impacts}/{expected}, lease {usedLease}, packets {packets}, active {host.Action?.Command}.");
                prototypes.Add(target.Identity);
                arenas.Add(formation.LocationId);
                turns++;
                if (finisher)
                {
                    roughTurns++;
                }
            }

            Ff7BattleSession Create(long request) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, request, requestFlags: 4, animations: animations);
        }
        Assert.Equal(16, prototypes.Count);
        Assert.Equal(11, arenas.Count);
        output.WriteLine($"Original Renzokuken completed {turns} selected turns ({roughTurns} with Rough Divide) over {prototypes.Count} opening enemy prototypes and {arenas.Count} original arenas.");
    }

    private static (Ff7BattleCommandOption Option, ushort Mask)? Select(Ff7BattleSession host, int slot, int guestSlot, bool contact)
    {
        IReadOnlyList<Ff7BattleCommandOption> options = host.Commands(slot);
        if (!contact && slot != guestSlot)
        {
            return (options.Single(option => option.Command == Ff7BattleCommand.Defend), (ushort)(1 << slot));
        }
        if (slot == guestSlot)
        {
            Ff7BattleActorState actor = host.Actors.Single(actor => actor.Actor.Slot == slot);
            if (contact && actor.Actor.HitPoints * 2 < actor.Actor.MaxHitPoints && options.FirstOrDefault(option => option.Command == Ff7BattleCommand.Item && option.Argument == 1 && (option.ValidTargets & 1 << slot) != 0) is { } potion)
            {
                return (potion, (ushort)(1 << slot));
            }
        }
        // Guard Scorpion's native fifth turn raises the tail and selects idle action 7 until it lowers it again.
        bool waitForTail = host.Formation.FormationId == 324 && host.Actors.Any(actor => actor.Actor.Slot == 4 && actor.Actor.IdleAction == 7);
        Ff7BattleCommandOption? chosen = options.FirstOrDefault(option => slot != guestSlot && option is { Command: Ff7BattleCommand.Item, Argument: 7, ValidTargets: not 0 })
            ?? options.FirstOrDefault(option => slot != guestSlot && option is { Command: Ff7BattleCommand.Item, Argument: 0, ValidTargets: not 0 } && host.Actors.Any(ally => (option.ValidTargets & 1 << ally.Actor.Slot) != 0 && ally.Actor.HitPoints > 0 && ally.Actor.HitPoints * 2 < ally.Actor.MaxHitPoints))
            ?? options.FirstOrDefault(option => !waitForTail && slot != guestSlot && option is { Command: Ff7BattleCommand.Magic, Argument: 0x21, ValidTargets: not 0 })
            ?? options.FirstOrDefault(option => !waitForTail && slot != guestSlot && option is { Command: Ff7BattleCommand.Limit, ValidTargets: not 0 })
            ?? options.FirstOrDefault(option => !waitForTail && option is { Command: Ff7BattleCommand.Attack, ValidTargets: not 0 })
            ?? options.FirstOrDefault(option => option.Command == Ff7BattleCommand.Defend);
        if (chosen is null)
        {
            return null;
        }
        Ff7BattleActorState target = host.Actors.Where(actor => (chosen.ValidTargets & 1 << actor.Actor.Slot) != 0).OrderByDescending(actor => (actor.Actor.Slot >= 4) == chosen.DefaultsToEnemies).ThenBy(actor => actor.Actor.HitPoints).First();
        return (chosen, (ushort)(1 << target.Actor.Slot));
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
