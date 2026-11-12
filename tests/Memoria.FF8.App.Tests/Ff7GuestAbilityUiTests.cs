using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestAbilityUiTests
{
    [Fact]
    public void UnsupportedAbilitiesAndTheirStockAppearInRed()
    {
        BattleUiTests.TestCanvas canvas = new();
        UiTheme theme = new();
        BattleHudLayout layout = new(new(0, 0, 960, 672));
        Ff7Scene.DrawBattleChoices(canvas, theme, layout, 1, "BLUE MAGIC", [new("Available"), new("Unsupported", "6", true)], 1, true);

        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Available").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "Unsupported").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "6").Color);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableTargetsRemainVisibleInRedForSingleAndGroupSelection(bool group)
    {
        BattleUiTests.TestCanvas canvas = new();
        UiTheme theme = new();
        Ff7Scene.BattleTargetRow[] targets = [new(4, "Available"), new(5, "Unavailable", "The host cannot change this target's status."), new(0, "Guest")];

        Ff7Scene.DrawBattleTargetChoices(canvas, theme, new(new(0, 0, 960, 672)), 1, targets, 1, group);

        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Available").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "Unavailable").Color);
        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Guest").Color);
        Assert.Equal(group ? 2 : 1, canvas.Pointers.Count);
    }

    [Fact]
    public void AllTargetSelectionPointsToBothAlliesAndEnemies()
    {
        BattleUiTests.TestCanvas canvas = new();
        Ff7Scene.DrawBattleTargetChoices(canvas, new UiTheme(), new(new(0, 0, 960, 672)), 1, [new(0, "Ally"), new(4, "Enemy")], 0, group: true, allTargets: true);
        Assert.Equal(2, canvas.Pointers.Count);
        Assert.Contains("TARGET  < ALL >", canvas.Texts);
    }

    [Fact]
    public void SelectingAnUnsupportedAbilityShowsItsReasonWithoutSelectingATarget()
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Ff7BattleCommandOption option = new(Ff7BattleCommand.Magic, 0xEC01, 0, true, true)
        {
            DisplayName = "Blue Magic",
            Category = "Blue Magic",
            UnsupportedReason = "This action requires a target status contract that is unavailable in the current battle bridge."
        };
        typeof(Ff7Scene).GetMethod("SelectBattleAction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [option]);

        Assert.Same(option, typeof(Ff7Scene).GetField("unavailableBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene));
        Assert.Null(typeof(Ff7Scene).GetField("selectedBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene));
        BattleUiTests.TestCanvas canvas = new();
        Ff7Scene.DrawBattleUnsupportedAbility(canvas, new UiTheme(), new(new(0, 0, 960, 672)), 1, option);
        Assert.Contains(canvas.TextDraws, draw => draw.Text == option.UnsupportedReason && draw.Color == new UiColor(255, 80, 80));
        Assert.Contains(canvas.Texts, text => text == "Confirm or Cancel to return");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableTargetConfirmKeepsTurnStockAndSelectionAndReasonCanBeDismissed(bool group)
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
        Ff7BattleSession initial = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
        Guid encounter = Guid.NewGuid();
        initial.BindGuestVitals(new(encounter, new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17));
        Ff7BattleSnapshot snapshot = initial.Capture();
        snapshot = snapshot with
        {
            Intro = null,
            Actors = snapshot.Actors.Select(actor => actor with { Gauge = actor.Actor.Slot == 0 ? ushort.MaxValue : (ushort)0, AwaitingTurn = actor.Actor.Slot == 0 }).ToArray()
        };
        Ff7BattleSession battle = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);
        TargetProbe guest = new(encounter, group);
        battle.BindGuestAttack(guest);
        Assert.Equal(0, battle.ReadyPlayer);
        Ff7BattleCommandOption option = Assert.Single(battle.Commands(0), candidate => candidate.Command == Ff7BattleCommand.Magic);
        Assert.Null(option.UnsupportedReason);
        Assert.Equal((ushort)0x10, option.ValidTargets);
        Assert.Equal((ushort)0x30, option.CandidateTargets);
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Set("selectedBattleCommand", option);
        Set("battleTargetIndex", group ? 0 : 1);
        Set("semanticInput", new InputFrame(0, 0, true, false, false, false));
        string before = JsonSerializer.Serialize(battle.Capture());

        Invoke("ReadBattleCommand", battle, 0);

        Ff7BattleCommandOption blocked = Assert.IsType<Ff7BattleCommandOption>(Get("unavailableBattleCommand"));
        Assert.Equal(TargetProbe.Reason, blocked.UnsupportedReason);
        Assert.Same(option, Get("selectedBattleCommand"));
        Assert.Equal(group ? 0 : 1, Get("battleTargetIndex"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(4, guest.MagicStock(TargetProbe.Ability));
        Set("semanticInput", new InputFrame(0, 0, false, true, false, false));
        Assert.Equal(true, Invoke("ReadUnavailableBattleCommand"));
        Assert.Null(Get("unavailableBattleCommand"));
        Assert.Same(option, Get("selectedBattleCommand"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(false, Invoke("ReadUnavailableBattleCommand"));
        if (!group)
        {
            Set("battleTargetIndex", 0);
            Set("semanticInput", new InputFrame(0, 0, true, false, false, false));
            Invoke("ReadBattleCommand", battle, 0);
            Assert.Null(Get("selectedBattleCommand"));
            Ff7BattleQueuedCommand accepted = Assert.Single(battle.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
            Assert.Equal((ushort)0x10, accepted.TargetMask);
            Assert.Equal(TargetProbe.Ability, accepted.GuestAbility);
            Assert.Equal(4, guest.MagicStock(TargetProbe.Ability));
        }

        void Set(string field, object value) => typeof(Ff7Scene).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, value);
        object? Get(string field) => typeof(Ff7Scene).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene);
        object? Invoke(string method, params object[] arguments) => typeof(Ff7Scene).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, arguments);
    }

    [Theory]
    [InlineData((ushort)57, (ushort)0x10)]
    [InlineData((ushort)0xEC01, (ushort)1)]
    public void GenericAbilityKeysRetainOrdinarySingleTargetQueueSemantics(ushort ability, ushort target)
    {
        Ff7BattleCommandQueueState state = Ff7BattleCommandQueue.StageReady(Ff7BattleCommandQueueState.Create(), 0, (byte)Ff7BattleCommand.Magic, ability, target, guestAttack: true, guestAbility: ability);
        Ff7BattleQueuedCommand command = Assert.Single(state.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.Equal(ability, command.GuestAbility);
        Assert.Equal(ability, command.Argument);
        Assert.Equal(target, command.TargetMask);
        Assert.True(command.GuestAttack);
    }
    private sealed class TargetProbe(Guid encounter, bool group) : IFf7BattleGuestAttack, IFf7BattleGuestMagic, IFf7BattleGuestAbilities, IFf7BattleGuestGroupMagic
    {
        public const ushort Ability = 57;
        public const string Reason = "The host cannot change this target's status.";
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation => null;
        public long HostTick => 0;
        public bool ContactPublished => false;
        public IReadOnlyList<Ff7BattleGuestAbility> Abilities => [new(Ff7BattleCommand.Magic, Ability, "Native ability", group ? (byte)0x16 : (byte)0x12, 4)];
        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;
        public bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => TargetUnsupportedReason(command, ability, target) is null;
        public string? TargetUnsupportedReason(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => target.Slot == 4 ? null : Reason;
        public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => target.Slot == 4;
        public int MagicStock(ushort ability) => 4;
        public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability) => throw new InvalidOperationException("An unavailable selection must not prepare magic.");
        public void PrepareMagic(Ff7BattleGuestPhysicalHit prepared) => throw new InvalidOperationException("An unavailable selection must not consume stock.");
        public string? PrepareGroupUnsupportedReason(IReadOnlyList<Ff7BattleGuestPhysicalHit> prepared) => throw new InvalidOperationException("An unavailable selection must not prepare a group.");
        public void PrepareGroup(IReadOnlyList<Ff7BattleGuestPhysicalHit> prepared) => throw new InvalidOperationException("An unavailable selection must not consume group stock.");
        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target) => throw new InvalidOperationException("An unavailable selection must not start an action.");
        public Ff7BattleGuestAttackUpdate Advance(long hostTick) => new(null, true);
        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
        }
    }

}
