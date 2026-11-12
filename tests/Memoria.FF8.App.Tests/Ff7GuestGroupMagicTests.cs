using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestGroupMagicTests
{
    [Fact]
    public void GroupQueuesOnceConsumesOneStockAndPublishesEachTargetOnce()
    {
        using Fixture fixture = new();
        Ff7BattleSession battle = fixture.Battle;
        GroupProbe guest = fixture.Guest;
        Dictionary<int, uint> before = battle.Actors.Where(actor => actor.Actor.Slot >= 4).ToDictionary(actor => actor.Actor.Slot, actor => actor.Actor.HitPoints);
        Assert.True(battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x30));
        Ff7BattleQueuedCommand queued = Assert.Single(battle.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.Equal((ushort)0x30, queued.TargetMask);
        Assert.True(queued.GroupTarget);
        Assert.Equal(4, guest.Stock);
        List<int> publications = [];
        for (int tick = 0; tick < 1000 && !guest.Completed; tick++)
        {
            battle.Advance(waitForCommand: true);
            if (battle.LastHit is { Source: 0 } hit && hit.Tick == battle.Tick)
            {
                publications.Add(hit.Target);
            }
        }
        Assert.True(guest.Completed);
        Assert.Equal(new[] { 4, 5 }, publications);
        Assert.Equal(1, guest.PrepareCalls);
        Assert.Equal(1, guest.BeginCalls);
        Assert.Equal(3, guest.Stock);
        Assert.Null(battle.Action);
        Assert.Null(guest.Invocation);
        Assert.All(battle.Actors.Where(actor => actor.Actor.Slot >= 4), actor => Assert.True(actor.Actor.HitPoints < before[actor.Actor.Slot]));
        Assert.Equal(0x30, guest.Prepared.Aggregate(0, (mask, hit) => mask | 1 << hit.PreparedContact!.Invocation.TargetSlot));
    }

    [Fact]
    public void AllTargetGroupUsesTheWholeBattlefieldAndPublishesAlliesAndEnemies()
    {
        using Fixture fixture = new(allTargets: true);
        Ff7BattleCommandOption option = Assert.Single(fixture.Battle.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic);
        Assert.True(option.AllTargets);
        Assert.Equal((ushort)0x33, option.CandidateTargets);
        Assert.False(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x30));
        Assert.True(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x33));
        Ff7BattleQueuedCommand queued = Assert.Single(fixture.Battle.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.True(queued.GroupTarget);
        Assert.Equal((ushort)0x33, queued.TargetMask);
        List<int> publications = [];
        for (int tick = 0; tick < 1000 && !fixture.Guest.Completed; tick++)
        {
            fixture.Battle.Advance(waitForCommand: true);
            if (fixture.Battle.LastHit is { Source: 0 } hit && hit.Tick == fixture.Battle.Tick)
            {
                publications.Add(hit.Target);
            }
        }
        Assert.True(fixture.Guest.Completed);
        Assert.Equal(new[] { 0, 1, 4, 5 }, publications);
        Assert.Equal(1, fixture.Guest.PrepareCalls);
        Assert.Equal(3, fixture.Guest.Stock);
        Assert.Null(fixture.Battle.Action);
    }

    [Fact]
    public void UnavailableGroupMemberRejectsSubmissionWithoutSpendingOrLosingTurn()
    {
        using Fixture fixture = new();
        fixture.Guest.BlockedTarget = 5;
        Ff7BattleCommandOption option = Assert.Single(fixture.Battle.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic);
        Assert.Equal((ushort)0x30, option.CandidateTargets);
        Assert.Equal((ushort)0x10, option.ValidTargets);
        string before = JsonSerializer.Serialize(fixture.Battle.Capture());

        Assert.False(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x30));
        Assert.False(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x10));

        Assert.Equal(before, JsonSerializer.Serialize(fixture.Battle.Capture()));
        Assert.Equal(4, fixture.Guest.Stock);
        Assert.Equal(0, fixture.Guest.PrepareCalls);
        Assert.Equal(0, fixture.Guest.BeginCalls);
        Assert.Equal(0, fixture.Battle.ReadyPlayer);
    }

    [Fact]
    public void ExactGroupPreflightRejectsEveryContactAtomicallyAndPreservesRandomTurnAndStock()
    {
        using Fixture fixture = new();
        using Fixture control = new();
        Ff7BattleSession battle = fixture.Battle;
        fixture.Guest.RejectPrepared = true;
        Ff7BattleSnapshot before = battle.Capture();
        Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(control.Battle.Capture().Random));
        Assert.True(battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x30));
        for (int tick = 0; tick < 1000 && battle.GuestAbilityFailure is null; tick++)
        {
            battle.Advance(waitForCommand: true);
            control.Battle.Advance(waitForCommand: true);
        }

        Assert.Equal(GroupProbe.Reason, Assert.IsType<Ff7BattleGuestAbilityFailure>(battle.GuestAbilityFailure).Reason);
        Assert.Equal(2, fixture.Guest.Prepared.Count);
        Assert.All(fixture.Guest.Prepared, hit => Assert.NotNull(hit.PreparedContact));
        Assert.Equal(0, fixture.Guest.PrepareCalls);
        Assert.Equal(0, fixture.Guest.BeginCalls);
        Assert.Equal(4, fixture.Guest.Stock);
        Assert.Equal(0, battle.ReadyPlayer);
        Ff7BattleSnapshot after = battle.Capture();
        // The ordinary presentation tick rolls escape RNG even while its battle clock waits.
        // Only the rejected group's speculative contact rolls must remain uncommitted.
        Assert.Equal(control.Battle.Tick, after.Tick);
        Assert.NotEqual(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(after.Random));
        Assert.Equal(JsonSerializer.Serialize(control.Battle.Capture().Random), JsonSerializer.Serialize(after.Random));
        Assert.Equal(JsonSerializer.Serialize(before.Inventory), JsonSerializer.Serialize(after.Inventory));
        Assert.Equal(before.Actors.Select(actor => actor.Actor), after.Actors.Select(actor => actor.Actor));
        Assert.Equal(before.Actors.Single(actor => actor.Actor.Slot == 0).Gauge, after.Actors.Single(actor => actor.Actor.Slot == 0).Gauge);
        Assert.Null(battle.Action);
        Assert.Null(battle.LastHit);
        Assert.All(after.Queue.Ready, command => Assert.Null(command));
        Assert.All(after.Queue.Slots, command => Assert.Null(command));
    }

    [Fact]
    public void RepeatingAGroupTargetIsRejectedBeforeItCanPublishTwice()
    {
        using Fixture fixture = new();
        fixture.Guest.RepeatFirstTarget = true;
        Assert.True(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0x30));
        for (int tick = 0; tick < 1000 && fixture.Battle.LastHit is null; tick++)
        {
            fixture.Battle.Advance(waitForCommand: true);
        }
        Assert.NotNull(fixture.Battle.LastHit);
        uint hp = fixture.Battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints;

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => fixture.Battle.Advance(waitForCommand: true));

        Assert.Contains("repeated a contact", error.Message);
        Assert.Equal(hp, fixture.Battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
        Assert.Equal(3, fixture.Guest.Stock);
    }

    [Fact]
    public void ReviveGroupCandidatesIncludeOnlyKnockedOutAlliesAndKeepTheNativeGroupMask()
    {
        using Fixture fixture = new(revive: true);
        Ff7BattleCommandOption option = Assert.Single(fixture.Battle.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic);
        Assert.False(option.SelectSingle);
        Assert.Equal((ushort)2, option.CandidateTargets);
        Assert.Equal((ushort)2, option.ValidTargets);
        Assert.Null(fixture.Battle.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 1));
        Assert.NotNull(fixture.Battle.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 0));
        Assert.False(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 1));
        Assert.True(fixture.Battle.Submit(0, Ff7BattleCommand.Magic, GroupProbe.Ability, 2));
        Ff7BattleQueuedCommand queued = Assert.Single(fixture.Battle.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.True(queued.GroupTarget);
        Assert.Equal((ushort)2, queued.TargetMask);
        Assert.Equal(4, fixture.Guest.Stock);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Ff7Session original;
        private readonly LgpArchive archive;
        private readonly LgpArchive magic;
        public Ff7BattleSession Battle { get; }
        public GroupProbe Guest { get; }

        public Fixture(bool revive = false, bool allTargets = false)
        {
            string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the installed original FFVII resources.");
            Ff7Installation installation = Ff7Installation.Discover(directory!);
            original = Ff7Session.NewGame(installation);
            Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
            IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
            Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
            SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
            archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
            Ff7BattleSession initial = Create();
            Guid encounter = Guid.NewGuid();
            initial.BindGuestVitals(new(encounter, new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17));
            Ff7BattleSnapshot snapshot = initial.Capture();
            snapshot = snapshot with
            {
                Intro = null,
                Actors = snapshot.Actors.Select(actor => actor.Actor.Slot == 0
                    ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true }
                    : actor with
                    {
                        Gauge = 0,
                        AwaitingTurn = false,
                        Actor = actor.Actor with { Statuses = revive && actor.Actor.Slot == 1 ? 1u : 0x400u, HitPoints = revive && actor.Actor.Slot == 1 ? 0 : actor.Actor.HitPoints },
                        Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, revive && actor.Actor.Slot == 1 ? 1u : 0x400u)
                    }).ToArray()
            };
            Battle = Create(snapshot);
            Guest = new(encounter, revive, allTargets);
            Battle.BindGuestAttack(Guest);
            Assert.Equal(0, Battle.ReadyPlayer);

            Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);
        }

        public void Dispose()
        {
            magic.Dispose();
            archive.Dispose();
            original.Dispose();
        }
    }

    private sealed class GroupProbe(Guid encounter, bool revive, bool allTargets) : IFf7BattleGuestAttack, IFf7BattleGuestMagic, IFf7BattleGuestAbilities, IFf7BattleGuestGroupMagic
    {
        public const ushort Ability = 57;
        public const string Reason = "The host cannot present one selected target contact.";
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation { get; private set; }
        public long HostTick { get; private set; }
        public bool ContactPublished => false;
        public int Stock { get; private set; } = 4;
        public int PrepareCalls { get; private set; }
        public int BeginCalls { get; private set; }
        public int? BlockedTarget { get; set; }
        public bool RejectPrepared { get; set; }
        public bool RepeatFirstTarget { get; set; }
        public bool Completed { get; private set; }
        public IReadOnlyList<Ff7BattleGuestPhysicalHit> Prepared { get; private set; } = [];
        private int published;
        public IReadOnlyList<Ff7BattleGuestAbility> Abilities => [new(Ff7BattleCommand.Magic, Ability, "Native group", revive ? (byte)0x14 : allTargets ? (byte)0x04 : (byte)0x16, Stock) { CanTargetDead = revive, AllTargets = allTargets }];
        public bool Supports(Ff7BattleGuestAttackTarget target) => true;
        public bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => TargetUnsupportedReason(command, ability, target) is null;
        public string? TargetUnsupportedReason(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => target.Slot == BlockedTarget ? Reason : null;
        public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => SupportsAbility(Ff7BattleCommand.Magic, ability, target);
        public int MagicStock(ushort ability) => Stock;
        public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability) => new(new(6, 24, 0, 0, 14, 0, 0, 24, 17), new(0x22, 1, 100, LongRange: true) { SuppressCritical = true, AlwaysHits = true }, 0);
        public void PrepareMagic(Ff7BattleGuestPhysicalHit prepared) => throw new InvalidOperationException("Group stock belongs to the shared group transaction.");

        public string? PrepareGroupUnsupportedReason(IReadOnlyList<Ff7BattleGuestPhysicalHit> prepared)
        {
            Prepared = prepared.ToArray();
            return RejectPrepared ? Reason : null;
        }

        public void PrepareGroup(IReadOnlyList<Ff7BattleGuestPhysicalHit> prepared)
        {
            Prepared = prepared.ToArray();
            PrepareCalls++;
            Stock--;
        }

        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            BeginCalls++;
            Invocation = invocation;
            HostTick = invocation.StartedAt;
        }

        public Ff7BattleGuestAttackUpdate Advance(long hostTick)
        {
            HostTick = hostTick;
            if (published < Prepared.Count)
            {
                Ff7BattleGuestPhysicalHit hit = Prepared[RepeatFirstTarget ? 0 : published];
                published++;
                return new(null, false) { GroupHits = [hit] };
            }
            Invocation = null;
            Completed = true;
            return new(null, true);
        }

        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
            HostTick = hostTick;
        }
    }
}
