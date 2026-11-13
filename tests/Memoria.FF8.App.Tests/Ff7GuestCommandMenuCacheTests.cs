using System.Reflection;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestCommandMenuCacheTests
{
    [Fact]
    public void UnchangedCombatInputsReuseOptionsAcrossMenuFrames()
    {
        using Fixture fixture = new();
        IReadOnlyList<Ff7BattleCommandOption> first = fixture.Battle.Commands(0);
        int supports = fixture.Guest.SupportCalls;
        for (int frame = 0; frame < 10; frame++)
        {
            fixture.State = fixture.State with
            {
                Tick = fixture.State.Tick + 1,
                Actors = fixture.State.Actors.Select(actor => actor with { Gauge = (ushort)(actor.Gauge ^ 1) }).ToArray()
            };
            Assert.Same(first, fixture.Battle.Commands(0));
        }
        Assert.Equal(1, fixture.Guest.AbilityReads);
        Assert.Equal(supports, fixture.Guest.SupportCalls);
        Assert.NotEqual(0, supports);
    }

    [Theory]
    [InlineData("hp")]
    [InlineData("status")]
    [InlineData("conditions")]
    [InlineData("mp")]
    [InlineData("source-status")]
    [InlineData("inventory")]
    [InlineData("all-uses")]
    [InlineData("token")]
    public void MeaningfulChangesRefreshOptionsAndTargetMasks(string change)
    {
        using Fixture fixture = new();
        IReadOnlyList<Ff7BattleCommandOption> first = fixture.Battle.Commands(0);
        Assert.Equal((ushort)0x10, Ability(first).ValidTargets);
        switch (change)
        {
            case "hp":
                fixture.ChangeActor(4, actor => actor with { Actor = actor.Actor with { HitPoints = 0 } });
                break;
            case "status":
                fixture.ChangeActor(4, actor => actor with { Actor = actor.Actor with { Statuses = 1 } });
                break;
            case "conditions":
                fixture.ChangeActor(4, actor => actor with { Actor = actor.Actor with { Conditions = actor.Actor.Conditions & ~8u } });
                break;
            case "mp":
                fixture.ChangeActor(4, actor => actor with { Actor = actor.Actor with { MagicPoints = 0 } });
                break;
            case "source-status":
                fixture.ChangeActor(0, actor => actor with { Actor = actor.Actor with { Statuses = 0x80 } });
                break;
            case "inventory":
                fixture.State.Inventory[0] = new(0, 3);
                break;
            case "all-uses":
                fixture.State.Actors.Single(actor => actor.Actor.Slot == 0).AllUses![42] = 2;
                break;
            case "token":
                fixture.Guest.StateToken = new();
                fixture.Guest.AllowTarget = false;
                break;
        }
        IReadOnlyList<Ff7BattleCommandOption> refreshed = fixture.Battle.Commands(0);
        Assert.NotSame(first, refreshed);
        Assert.Equal(2, fixture.Guest.AbilityReads);
        if (change is "hp" or "status" or "conditions" or "mp" or "token")
        {
            Assert.Equal((ushort)0, Ability(refreshed).ValidTargets);
        }
        if (change == "source-status")
        {
            Assert.NotNull(Ability(refreshed).UnsupportedReason);
        }
        if (change == "inventory")
        {
            Assert.Equal(2, Assert.Single(first, option => option.Command == Ff7BattleCommand.Item && option.Argument == 0).Cost);
            Assert.Equal(3, Assert.Single(refreshed, option => option.Command == Ff7BattleCommand.Item && option.Argument == 0).Cost);
        }
        Assert.Same(refreshed, fixture.Battle.Commands(0));
    }

    [Fact]
    public void LosingReadinessOrChangingTheRequestedOwnerClearsTheMenu()
    {
        using Fixture fixture = new();
        IReadOnlyList<Ff7BattleCommandOption> first = fixture.Battle.Commands(0);
        Assert.Empty(fixture.Battle.Commands(1));
        IReadOnlyList<Ff7BattleCommandOption> next = fixture.Battle.Commands(0);
        Assert.NotSame(first, next);
        fixture.ChangeActor(0, actor => actor with { AwaitingTurn = false });
        Assert.Empty(fixture.Battle.Commands(0));
        fixture.ChangeActor(0, actor => actor with { AwaitingTurn = true });
        Assert.NotSame(next, fixture.Battle.Commands(0));
        Assert.Equal(3, fixture.Guest.AbilityReads);
    }

    [Fact]
    public void ProvidersWithoutATokenRemainLive()
    {
        using Fixture fixture = new();
        fixture.Guest.StateToken = null;
        IReadOnlyList<Ff7BattleCommandOption> first = fixture.Battle.Commands(0);
        fixture.Guest.AllowTarget = false;
        IReadOnlyList<Ff7BattleCommandOption> next = fixture.Battle.Commands(0);
        Assert.NotSame(first, next);
        Assert.Equal(2, fixture.Guest.AbilityReads);
        Assert.Equal((ushort)0x10, Ability(first).ValidTargets);
        Assert.Equal((ushort)0, Ability(next).ValidTargets);
    }

    [Fact]
    public void SupportMemoizationDuringConstructionRetainsTheCompletedToken()
    {
        using Fixture fixture = new();
        fixture.Guest.RefreshTokenOnSupport = true;
        IReadOnlyList<Ff7BattleCommandOption> first = fixture.Battle.Commands(0);
        Assert.Same(first, fixture.Battle.Commands(0));
        Assert.Equal(1, fixture.Guest.AbilityReads);
    }

    private static Ff7BattleCommandOption Ability(IReadOnlyList<Ff7BattleCommandOption> options) => Assert.Single(options, option => option.Command == Ff7BattleCommand.Magic);

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo StateField = typeof(Ff7BattleSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly Ff7Session original;
        private readonly LgpArchive archive;
        private readonly LgpArchive magic;

        public Ff7BattleSession Battle { get; }
        public Probe Guest { get; }
        public Ff7BattleSnapshot State
        {
            get => (Ff7BattleSnapshot)StateField.GetValue(Battle)!;
            set => StateField.SetValue(Battle, value);
        }

        public Fixture()
        {
            string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the installed original FFVII resources.");
            Ff7Installation installation = Ff7Installation.Discover(directory!);
            original = Ff7Session.NewGame(installation);
            IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
            Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
            SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
            archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
            Ff7BattleSession initial = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
            Guid encounter = Guid.NewGuid();
            initial.BindGuestVitals(new(encounter, new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17));
            Ff7BattleSnapshot snapshot = initial.Capture();
            snapshot = snapshot with
            {
                Intro = null,
                Inventory = [new(0, 2)],
                Actors = snapshot.Actors.Select(actor => actor with { Gauge = actor.Actor.Slot == 0 ? ushort.MaxValue : (ushort)0, AwaitingTurn = actor.Actor.Slot == 0 }).ToArray()
            };
            Battle = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);
            // Exercise defensive copying of a mutable host input after validating the guest save contract.
            ChangeActor(0, actor => actor with { AllUses = new() { [42] = 1 } });
            ChangeActor(4, actor => actor with { Actor = actor.Actor with { HitPoints = 100, MaxHitPoints = 100, MagicPoints = 10, MaxMagicPoints = 10, Statuses = 0, Conditions = actor.Actor.Conditions | 8u } });
            Guest = new(encounter);
            Battle.BindGuestAttack(Guest);
            Assert.Equal(0, Battle.ReadyPlayer);
        }

        public void ChangeActor(int slot, Func<Ff7BattleActorState, Ff7BattleActorState> change)
        {
            State = State with { Actors = State.Actors.Select(actor => actor.Actor.Slot == slot ? change(actor) : actor).ToArray() };
        }

        public void Dispose()
        {
            magic.Dispose();
            archive.Dispose();
            original.Dispose();
        }
    }

    private sealed class Probe(Guid encounter) : IFf7BattleGuestAttack, IFf7BattleGuestAbilities
    {
        public object? StateToken { get; set; } = new();
        public bool AllowTarget { get; set; } = true;
        public bool RefreshTokenOnSupport { get; set; }
        public int AbilityReads { get; private set; }
        public int SupportCalls { get; private set; }
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation => null;
        public long HostTick => 0;
        public bool ContactPublished => false;
        public object? CommandMenuState => StateToken;
        public IReadOnlyList<Ff7BattleGuestAbility> Abilities
        {
            get
            {
                AbilityReads++;
                return [new(Ff7BattleCommand.Magic, 1, "Ability", 0x12, 4)];
            }
        }

        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;

        public bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target)
        {
            SupportCalls++;
            if (RefreshTokenOnSupport)
            {
                StateToken = new();
            }
            return AllowTarget && target.Slot == 4 && target.Actor.MagicPoints > 0;
        }

        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target) => throw new InvalidOperationException("The menu must not start an action.");
        public Ff7BattleGuestAttackUpdate Advance(long hostTick) => new(null, true);
        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
        }
    }
}
