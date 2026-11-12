using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestAbilityAdmissionTests
{
    [Fact]
    public void RejectedExactContactPreservesTurnStockAndRandomThenAcceptsAnotherCommand()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the installed original FFVII resources.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession initial = Create();
        Guid encounter = Guid.NewGuid();
        initial.BindGuestVitals(new(encounter, new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17));
        Ff7BattleSnapshot fixture = initial.Capture();
        // Stopped enemies and a frozen ATB clock isolate the command's own random transaction.
        fixture = fixture with
        {
            Actors = fixture.Actors.Select(actor => actor.Actor.Slot == 0
                ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true }
                : actor with { Gauge = 0, AwaitingTurn = false, Actor = actor.Actor with { Statuses = 0x400 }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 0x400) }).ToArray()
        };
        Ff7BattleSession battle = Create(fixture);
        AdmissionProbe guest = new(encounter);
        battle.BindGuestAttack(guest);
        for (int tick = 0; tick < 1000 && battle.ReadyPlayer != 0; tick++)
        {
            battle.Advance(waitForCommand: true);
        }
        Assert.Equal(0, battle.ReadyPlayer);
        Ff7BattleSnapshot ready = battle.Capture();
        Ff7BattleActorState sourceBefore = ready.Actors.Single(actor => actor.Actor.Slot == 0);
        Ff7BattleAiActor targetBefore = ready.Actors.Single(actor => actor.Actor.Slot == 4).Actor;
        string randomBefore = JsonSerializer.Serialize(ready.Random);
        string inventoryBefore = JsonSerializer.Serialize(ready.Inventory);
        Ff7BattleCommandOption option = Assert.Single(battle.Commands(0), candidate => candidate.Command == Ff7BattleCommand.Magic);
        Assert.Null(option.UnsupportedReason);
        Assert.Equal((ushort)0x30, option.ValidTargets);
        Assert.True(battle.Submit(0, Ff7BattleCommand.Magic, AdmissionProbe.Ability, 1 << 4));
        Assert.Equal(4, guest.MagicStock(AdmissionProbe.Ability));
        for (int tick = 0; tick < 1000 && battle.GuestAbilityFailure is null; tick++)
        {
            battle.Advance(waitForCommand: true);
        }

        Ff7BattleGuestAbilityFailure failure = Assert.IsType<Ff7BattleGuestAbilityFailure>(battle.GuestAbilityFailure);
        Assert.Equal((battle.Tick, 0, AdmissionProbe.Ability, AdmissionProbe.Reason), (failure.Tick, failure.ActorSlot, failure.Ability, failure.Reason));
        Assert.Equal(1, guest.PreviewCalls);
        Ff7BattleGuestPreparedContact preview = Assert.IsType<Ff7BattleGuestPreparedContact>(guest.Preview!.PreparedContact);
        Assert.Equal((0, 4, AdmissionProbe.Ability), (preview.Invocation.ActorSlot, preview.Invocation.TargetSlot, preview.Invocation.GuestAbility));
        Assert.False(preview.Receipt.Damage.Missed);
        Assert.True(preview.Receipt.Damage.Damage > 0);
        Assert.Equal(0, guest.BeginCalls);
        Assert.Equal(0, guest.PrepareCalls);
        Assert.Equal(4, guest.MagicStock(AdmissionProbe.Ability));
        Assert.Null(guest.Invocation);
        Assert.Null(battle.Action);
        Assert.Null(battle.LastHit);
        Assert.Equal(0, battle.ReadyPlayer);
        Ff7BattleSnapshot rejected = battle.Capture();
        Ff7BattleActorState sourceAfter = rejected.Actors.Single(actor => actor.Actor.Slot == 0);
        Assert.True(sourceAfter.AwaitingTurn);
        Assert.Equal(sourceBefore.Gauge, sourceAfter.Gauge);
        Assert.Equal(sourceBefore.Actor, sourceAfter.Actor);
        Assert.Equal(targetBefore, rejected.Actors.Single(actor => actor.Actor.Slot == 4).Actor);
        Assert.Equal(randomBefore, JsonSerializer.Serialize(rejected.Random));
        Assert.Equal(inventoryBefore, JsonSerializer.Serialize(rejected.Inventory));
        Assert.Equal(rejected.Queue.ReadyRead, rejected.Queue.ReadyWrite);
        Assert.All(rejected.Queue.Ready, command => Assert.Null(command));
        Assert.All(rejected.Queue.Slots, command => Assert.Null(command));
        Assert.All(rejected.Actors.Where(actor => actor.Actor.Slot >= 4), actor => Assert.Equal(0x400u, actor.Actor.Statuses));

        Assert.True(battle.SubmitGuestAttack(0, 4));
        int publications = 0;
        for (int tick = 0; tick < 1000 && (guest.BeginCalls == 0 || guest.Invocation is not null || battle.Action is not null); tick++)
        {
            battle.Advance(waitForCommand: true);
            if (battle.LastHit is { Source: 0, Target: 4, AttackId: 0 } hit && hit.Tick == battle.Tick)
            {
                publications++;
            }
        }
        Assert.Equal(1, guest.BeginCalls);
        Assert.Equal(1, publications);
        Assert.Null(guest.Invocation);
        Assert.Null(battle.Action);
        Assert.Equal(4, guest.MagicStock(AdmissionProbe.Ability));
        Assert.Equal(0, guest.PrepareCalls);
        Assert.True(battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints < targetBefore.HitPoints);

        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: snapshot, animations: animations);
    }

    private sealed class AdmissionProbe(Guid encounter) : IFf7BattleGuestAttack, IFf7BattleGuestMagic, IFf7BattleGuestAbilities
    {
        public const ushort Ability = 57;
        public const string Reason = "The host cannot display this contact's target lifecycle.";
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation { get; private set; }
        public long HostTick { get; private set; }
        public bool ContactPublished { get; private set; }
        public int BeginCalls { get; private set; }
        public int PrepareCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public Ff7BattleGuestPhysicalHit? Preview { get; private set; }
        private int stock = 4;

        public IReadOnlyList<Ff7BattleGuestAbility> Abilities => [new(Ff7BattleCommand.Magic, Ability, "Native ability", 0x12, stock)];
        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;
        public bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => command == Ff7BattleCommand.Magic && ability == Ability && Supports(target);
        public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => ability == Ability && Supports(target);
        public int MagicStock(ushort ability) => ability == Ability ? stock : 0;
        public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability) => new(new(6, 24, 23, 0, 14, 0, 0, 24, 17), new(0x22, 8, 100, LongRange: true) { SuppressCritical = true }, 0);

        public string? PrepareMagicUnsupportedReason(Ff7BattleGuestPhysicalHit prepared)
        {
            PreviewCalls++;
            Preview = prepared;
            return Reason;
        }

        public void PrepareMagic(Ff7BattleGuestPhysicalHit prepared)
        {
            PrepareCalls++;
            stock--;
        }

        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            BeginCalls++;
            Invocation = invocation;
            HostTick = invocation.StartedAt;
            ContactPublished = false;
        }

        public Ff7BattleGuestAttackUpdate Advance(long hostTick)
        {
            HostTick = hostTick;
            if (ContactPublished)
            {
                Invocation = null;
                return new(null, true);
            }
            ContactPublished = true;
            return new(new(new(6, 24, 0, 0, 14, 0, 0, 24, 17), new(0x11, 1, 255, UsePhysicalStats: true) { SuppressCritical = true, AlwaysHits = true }, 0), false);
        }

        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
            HostTick = hostTick;
        }
    }
}
