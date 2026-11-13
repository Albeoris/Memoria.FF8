using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestStatusHostTests
{
    [Theory]
    [InlineData("Double", 0x20000, 2)]
    [InlineData("Triple", 0x40000, 3)]
    public void Original_status_on_a_second_guest_enables_selected_magic_targets_in_one_turn(string name, uint status, int count)
    {
        using Fixture fixture = new();
        Assert.NotNull(fixture.Host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, fixture.Spell(name), 4));
        fixture.Cast(0, name, 2);
        Assert.Equal(status, fixture.Host.GetGuestOriginBattleStatuses(1) & 0x60000);
        Ff7BattleSnapshot roundtrip = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(fixture.Host.Capture()))!;
        Assert.Equal(status, Assert.Single(roundtrip.GuestOriginStatuses!, entry => entry.HostSlot == 1).BattleStatuses & 0x60000);
        ushort cure = fixture.Spell("Cure");
        Assert.Contains(count, fixture.Providers[1].MagicCastCounts(cure));
        int before = fixture.Stock(cure);
        ushort[] targets = Enumerable.Range(0, count).Select(index => (ushort)(1 << (index % 2))).ToArray();
        fixture.Ready(1);
        Assert.True(fixture.Host.SubmitGuestMagicSequence(1, cure, targets));
        int contacts = fixture.Complete(1, count);
        Assert.Equal(count, contacts);
        Assert.Equal(before - count, fixture.Stock(cure));
        Assert.Empty(fixture.Host.Capture().GuestMagicSequences);
        Assert.Equal(status, fixture.Host.GetGuestOriginBattleStatuses(1) & 0x60000);
        Assert.Equal(new[] { 1 }, fixture.Providers[0].MagicCastCounts(cure));
    }

    [Fact]
    public void Drain_uses_the_host_damage_receipt_to_heal_the_caster_once()
    {
        using Fixture fixture = new();
        ushort spell = fixture.Spell("Drain");
        uint sourceBefore = fixture.Hp(0);
        uint targetBefore = fixture.Hp(1);
        int stockBefore = fixture.Stock(spell);
        Assert.NotNull(fixture.Host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, spell, 4));
        fixture.Cast(0, "Drain", 2);
        Assert.True(fixture.Hp(0) > sourceBefore);
        Assert.True(fixture.Hp(1) < targetBefore);
        Assert.Equal(targetBefore - fixture.Hp(1), fixture.Hp(0) - sourceBefore);
        Assert.Equal(stockBefore - 1, fixture.Stock(spell));
    }

    [Fact]
    public void Rejected_repeat_selection_preserves_the_queue_stock_and_random_state()
    {
        using Fixture fixture = new();
        ushort spell = fixture.Spell("Cure");
        fixture.Ready(0);
        string before = JsonSerializer.Serialize(fixture.Host.Capture());
        Assert.False(fixture.Host.SubmitGuestMagicSequence(0, spell, [1, 2]));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Host.Capture()));
        Assert.Equal(8, fixture.Stock(spell));
    }

    [Fact]
    public void Economical_triple_keeps_its_paid_selection_after_spending_the_last_copy()
    {
        using Fixture fixture = new(0x40, 1);
        fixture.Cast(0, "Triple", 2);
        fixture.Ready(1);
        ushort cure = fixture.Spell("Cure");
        Assert.Contains(3, fixture.Providers[1].MagicCastCounts(cure));
        Assert.True(fixture.Host.SubmitGuestMagicSequence(1, cure, [1, 2, 1]));
        Assert.Equal(3, fixture.Complete(1, 3, restoreQueuedBoundary: true));
        Assert.Equal(0, fixture.Stock(cure));
        Assert.Equal(1, fixture.Providers[1].Capture().CommittedSpells!.Value.Count(cost => cost.Spell == cure));
    }

    [Fact]
    public void Abbreviated_triple_does_not_apply_the_economical_double_passive()
    {
        using Fixture fixture = new(0x20, 2);
        fixture.Cast(0, "Triple", 2);
        fixture.Ready(1);
        ushort cure = fixture.Spell("Cure");
        Assert.Equal(new[] { 1, 2 }, fixture.Providers[1].MagicCastCounts(cure));
        Assert.Equal(2, fixture.Providers[1].MagicCastCost(cure, 2));
        Assert.True(fixture.Host.SubmitGuestMagicSequence(1, cure, [1, 2]));
        Assert.Equal(2, fixture.Complete(1, 2));
        Assert.Equal(0, fixture.Stock(cure));
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly GameAssets game;
        private readonly BattleAssets assets;
        private readonly Ff7Session original;
        private readonly LgpArchive archive;
        private readonly LgpArchive magic;
        private CrossoverGuestEncounter encounter;
        private readonly NativeCharacterVitals source;
        private readonly CrossoverBattleRoster roster;
        private readonly Func<Ff7BattleSnapshot, Ff7BattleSession> restoreHost;
        private readonly Ff7BattleRenderer renderer;
        private readonly GameplaySaveEconomy? economy;
        public Ff7BattleSession Host { get; private set; }
        public CrossoverGuestHostActions[] Providers { get; private set; }

        public Fixture(uint economical = 0, int cureStock = 8, int guestCount = 2, string[]? additionalSpells = null, string[]? additionalItems = null)
        {
            string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
            string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8) && !string.IsNullOrWhiteSpace(ff7), "Select both original game installations.");
            game = new(ff8!);
            assets = new(ff8!);
            NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
            origin = origin with { MagicSystem = origin.MagicSystem with { SharedMagicStock = true } };
            int command = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
            int itemCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4) + 20;
            for (byte id = 0; id < 2; id++)
            {
                int row = origin.FindPhysicalRow(id);
                origin.Records[row] = origin.Records[row] with { HitPointBonus = 9999, HitPoints = new((ushort)(id == 0 ? 1000 : 5000), (ushort)(id == 0 ? 1000 : 5000)), Status = 0, Junction = origin.Records[row].Junction with { Commands = (uint)(command | (additionalItems is null ? 0 : itemCommand << 8)) } };
            }
            if (economical != 0)
            {
                int passive = origin.Definitions.CharacterStatusPassiveFlags.IndexOf(economical);
                Assert.True(passive >= 0);
                int row = origin.FindPhysicalRow(1);
                origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Abilities = checked((uint)(58 + passive)) } };
            }
            foreach (string name in new[] { "Drain", "Double", "Triple", "Cure" }.Concat(additionalSpells ?? []).Distinct())
            {
                origin = origin.ChangeMagicStock(0, Spell(name), name == "Cure" ? cureStock : 8, MagicOperationReason.ScriptReward, out _);
            }
            source = origin;
            if (additionalItems is not null)
            {
                NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
                foreach (string name in additionalItems)
                {
                    ushort item = Item(name);
                    items[item] = new(checked((byte)item), 8);
                }
                economy = new(items, null, 0, 0, 0, 0);
            }
            CrossoverFf8BattleKit[] kits = Enumerable.Range(0, guestCount).Select(id => CrossoverFf8BattleKit.Read(origin, assets, id)).ToArray();
            Ff7Installation installation = Ff7Installation.Discover(ff7!);
            original = Ff7Session.NewGame(installation);
            archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
            SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
            Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
            IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
            Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
            Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
            roster = CrossoverBattleRoster.Create(Guid.NewGuid(), initial.Capture(), Enumerable.Range(0, guestCount).Select(slot => new CrossoverBattleBinding(slot, slot, slot)).ToArray(), []);
            for (int slot = 0; slot < guestCount; slot++)
            {
                CrossoverFf8BattleKit kit = kits[slot];
                Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
                initial.BindGuestVitals(new(roster.EncounterId, new('A', 64), slot, slot, slot, kit.RecordRow, kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
            }
            Ff7BattleSnapshot state = initial.Capture();
            state = state with { Actors = state.Actors.Select(actor => actor.Actor.Slot >= 4 ? actor with { Actor = actor.Actor with { Statuses = 0x400 }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 0x400) } : actor).ToArray() };
            Host = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: state, animations: animations);
            restoreHost = saved => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);
            encounter = new(roster, origin, Host, game.ReadNewGameGlobalRandom(), NativeCrtRandomState.FromColdProcess(), economy, null);
            Providers = new CrossoverGuestHostActions[guestCount];
            renderer = new(archive, formation.LocationId);
            MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
            for (int slot = 0; slot < guestCount; slot++)
            {
                CrossoverGunbladeHostAttack attack = new(roster, kits[slot], assets, game, encounter.GlobalRandom, () => Host.RenderCameraOutput, _ => 128, readHostImpactAnchor: Host.GuestEffectTargetAnchor, readHostTargetBone: Host.GuestEffectTargetBone);
                attack.PrepareStanding(Host.VisualPlacement(slot));
                CrossoverGuestHostActions provider = new(roster, kits[slot], attack, Host, game, assets, encounter, null, null, null);
                provider.BindForeignGeometry(target => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [Host, Host.Actors.Single(actor => actor.Actor.Slot == target)])!));
                Host.BindGuestAttack(provider);
                Providers[slot] = provider;
            }
        }

        public ushort Spell(string name) => assets.NativeActions.Single(action => action.Command == 2 && action.Name == name).ActionId;
        public ushort Item(string name) => assets.NativeActions.Single(action => action.Command == 4 && action.Name == name).ActionId;
        public uint Hp(int slot) => Host.Actors.Single(actor => actor.Actor.Slot == slot).Actor.HitPoints;
        public int Stock(ushort spell) => encounter.Working.GetMagicCount(0, spell);

        public void IsolateReadyPlayer(int slot, bool preserveProviders = false)
        {
            Assert.Null(Host.Action);
            Ff7BattleSnapshot snapshot = Host.Capture();
            Assert.Equal(snapshot.Queue.ReadyRead, snapshot.Queue.ReadyWrite);
            Assert.All(snapshot.Queue.Slots, entry => Assert.Null(entry));
            Ff7BattleSnapshot ready = snapshot with { Actors = snapshot.Actors.Select(actor => actor with { Gauge = actor.Actor.Slot == slot ? ushort.MaxValue : (ushort)0, AwaitingTurn = actor.Actor.Slot == slot }).ToArray() };
            if (preserveProviders)
            {
                typeof(Ff7BattleSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Host, ready);
                Assert.Equal(JsonSerializer.Serialize(snapshot.Random), JsonSerializer.Serialize(Host.Capture().Random));
                Assert.Equal(snapshot.Inventory, Host.Capture().Inventory);
                Assert.Equal(snapshot.Actors.Select(actor => actor.Actor), Host.Capture().Actors.Select(actor => actor.Actor));
            }
            else
            {
                RestoreQueuedBoundary(ready);
            }
        }

        public void Ready(int slot)
        {
            for (int tick = 0; tick < 12000 && Host.ReadyPlayer != slot; tick++)
            {
                if (Host.ReadyPlayer is int other && other != slot)
                {
                    if (Host.Commands(other).Any(command => command.Command == Ff7BattleCommand.Defend))
                    {
                        Assert.True(Host.Submit(other, Ff7BattleCommand.Defend, 0, (ushort)(1 << other)));
                    }
                    else
                    {
                        IsolateReadyPlayer(slot, preserveProviders: true);
                    }
                }
                Host.Advance(waitForCommand: false);
            }
            Assert.Equal(slot, Host.ReadyPlayer);
        }

        public void Cast(int caster, string name, ushort targets)
        {
            Ready(caster);
            ushort spell = Spell(name);
            Assert.Null(Host.GuestAbilityTargetUnsupportedReason(caster, Ff7BattleCommand.Magic, spell, System.Numerics.BitOperations.TrailingZeroCount(targets)));
            Assert.True(Host.Submit(caster, Ff7BattleCommand.Magic, spell, targets));
            Assert.Equal(1, Complete(caster, 1));
        }

        public void UseItem(int caster, string name, int target, bool restoreQueued = false)
        {
            Ready(caster);
            ushort item = Item(name);
            Assert.Null(Host.GuestAbilityTargetUnsupportedReason(caster, Ff7BattleCommand.Item, item, target));
            Assert.True(Host.SubmitGuestItem(caster, target, item));
            if (restoreQueued)
            {
                RestoreQueuedBoundary();
            }
            Assert.Equal(1, Complete(caster, 1));
        }

        public int Complete(int caster, int count, bool restoreQueuedBoundary = false)
        {
            HashSet<long> invocations = [];
            int contacts = 0;
            bool restored = false;
            for (int tick = 0; tick < 20000; tick++)
            {
                Host.Advance(waitForCommand: true);
                Assert.Null(Host.GuestAbilityFailure);
                if (Providers[caster].Invocation is { } invocation)
                {
                    invocations.Add(invocation.StartedAt);
                }
                if (Host.LastHit is { } hit && hit.Tick == Host.Tick && hit.Source == caster)
                {
                    contacts++;
                }
                if (restoreQueuedBoundary && !restored && Host.Action is null && Providers[caster].Invocation is null && !Host.Capture().GuestMagicSequences.IsEmpty)
                {
                    RestoreQueuedBoundary();
                    restored = true;
                }
                if (invocations.Count > 0 && Host.Action is null && Providers[caster].Invocation is null && Host.Capture().GuestMagicSequences.IsEmpty)
                {
                    Assert.Equal(count, invocations.Count);
                    Assert.Equal(restoreQueuedBoundary, restored);
                    return contacts;
                }
            }
            Assert.Fail("The selected native magic did not finish its host queue sequence.");
            return contacts;
        }

        public void RestoreQueuedBoundary(Ff7BattleSnapshot? snapshot = null)
        {
            Ff7BattleSnapshot saved = Roundtrip(snapshot ?? Host.Capture());
            CrossoverGuestEncounterSnapshot shared = Roundtrip(encounter.Capture());
            CrossoverGuestActorSnapshot[] actors = Providers.Select(provider => Roundtrip(provider.CaptureActor())).ToArray();
            CrossoverGunbladeHostSnapshot[] attacks = Providers.Select(provider => Roundtrip(provider.Attack.Capture())).ToArray();
            Host = restoreHost(saved);
            encounter = new(roster, source, Host, game.ReadNewGameGlobalRandom(), NativeCrtRandomState.FromColdProcess(), economy, shared);
            Providers = new CrossoverGuestHostActions[actors.Length];
            MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
            for (int slot = 0; slot < actors.Length; slot++)
            {
                CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(encounter.Working, assets, slot);
                CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, encounter.GlobalRandom, () => Host.RenderCameraOutput, _ => 128, attacks[slot], Host.GuestEffectTargetAnchor, Host.GuestEffectTargetBone);
                CrossoverGuestHostActions provider = new(roster, kit, attack, Host, game, assets, encounter, actors[slot], null, null);
                provider.BindForeignGeometry(target => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [Host, Host.Actors.Single(actor => actor.Actor.Slot == target)])!));
                Host.BindGuestAttack(provider);
                Providers[slot] = provider;
            }

            static T Roundtrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        }

        public void Dispose()
        {
            magic.Dispose();
            archive.Dispose();
            original.Dispose();
            assets.Dispose();
            game.Dispose();
        }
    }
}
