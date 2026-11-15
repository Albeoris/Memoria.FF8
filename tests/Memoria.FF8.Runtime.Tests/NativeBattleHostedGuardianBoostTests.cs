using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedGuardianBoostTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattlePartyModelOwner Caster = new BattlePartyActorModel(new("fourth"));
    private static readonly BattleEnemyModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly BattleActionHeaderReference Header = new(0);

    [Theory]
    [InlineData(true, (ushort)124, (byte)124)]
    [InlineData(false, (ushort)0, (byte)100)]
    public void NativeWidgetPublishesOneRequestThatSurvivesUnregistration(bool learned, ushort percent, byte expected)
    {
        (BattleSession child, long token) = Create(learned);
        Assert.Equal(learned, child.HostedGuardianBoost!.Enabled);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(Caster, out _));
        SetWidget(child, child.HostedGuardianBoost! with { Phase = 4, Remaining = 1, Window = 1, WindowRemaining = 10, Percent = percent });
        NativeBattleActionPacketsState original = child.NativeActionPackets;

        Poll(child, postCore: true);
        BattleSession.HostedGuardianBoostRequest request = Assert.IsType<BattleSession.HostedGuardianBoostRequest>(child.PendingHostedGuardianBoost);
        Assert.Equal(token, request.Token);
        Assert.Equal(Header, request.Header);
        Assert.Equal(Caster, request.Caster);
        Assert.Equal(expected, request.Percent);
        Assert.Equal((byte)6, child.HostedGuardianBoost!.Phase);
        Assert.Same(original, child.NativeActionPackets);
        Poll(child, publish: false);
        Assert.Null(child.HostedGuardianBoost);
        Assert.Same(request, child.PendingHostedGuardianBoost);
        Assert.Same(original, child.NativeActionPackets);
    }

    [Fact]
    public void HostedPollAppliesSelectBeforePublishingNewMappedButtonsAndHonorsPause()
    {
        (BattleSession child, _) = Create();
        SetWidget(child, child.HostedGuardianBoost! with { Phase = 4, Remaining = 10, Window = 1, WindowRemaining = 10, InputGrace = 4 });
        child.SetHostedGuardianBoostMappedButtons(0x180);
        Assert.True(child.PressHostedGuardianBoost());
        Assert.False(child.PressHostedGuardianBoost());
        Poll(child);
        Assert.Equal((ushort)0x180, child.NativeGraphics!.UiServices!.HeldButtons16);
        Assert.Equal(0, child.NativeGraphics.UiServices.Flags942 & 1);
        Assert.Equal((ushort)0, child.HostedGuardianBoost!.Percent);

        Assert.True(child.PressHostedGuardianBoost());
        Poll(child);
        Assert.Equal(1, child.NativeGraphics!.UiServices!.Flags942 & 1);
        Assert.Equal((ushort)76, child.HostedGuardianBoost!.Percent);

        child.SetHostedGuardianBoostMappedButtons(0);
        Assert.True(child.PressHostedGuardianBoost());
        NativeBattleGuardianBoost pressed = child.HostedGuardianBoost!;
        Poll(child, paused: true);
        Assert.Same(pressed, child.HostedGuardianBoost);
        Assert.Equal((ushort)0, child.NativeGraphics!.UiServices!.HeldButtons16);
        Assert.Equal(1, child.NativeGraphics.UiServices.Flags942 & 1);
        Poll(child, postCore: true);
        Assert.Equal(0, child.NativeGraphics!.UiServices!.Flags942 & 1);
        Assert.Equal((ushort)76, child.HostedGuardianBoost!.Percent);
        Assert.False(child.HostedGuardianBoost.PendingPress);
        Assert.Null(child.PendingHostedGuardianBoost);
    }

    [Fact]
    public void OnlyMatchingRequestAndPreparedContactReplacementCanAcknowledge()
    {
        (BattleSession child, long token) = Create();
        (BattleSession other, _) = Create();
        SetWidget(child, child.HostedGuardianBoost! with { Phase = 5 });
        Poll(child, publish: false);
        BattleSession.HostedGuardianBoostRequest request = child.PendingHostedGuardianBoost!;
        NativeBattleActionPacketsState original = child.NativeActionPackets;

        Assert.Throws<InvalidOperationException>(() => child.PrepareHostedGuardianContacts(request with { }, [Contact(4)]));
        Assert.Throws<InvalidOperationException>(() => other.PrepareHostedGuardianContacts(request, [Contact(4)]));
        Assert.Throws<InvalidDataException>(() => child.PrepareHostedGuardianContacts(request, []));
        BattleSession.PreparedHostedGuardianContacts stale = child.PrepareHostedGuardianContacts(request, [Contact(4)]);
        Poll(child, publish: false);
        Assert.Throws<InvalidOperationException>(() => child.CommitHostedGuardianContacts(stale));
        Assert.Same(original, child.NativeActionPackets);
        Assert.Same(request, child.PendingHostedGuardianBoost);

        BattleSession.PreparedHostedGuardianContacts ready = child.PrepareHostedGuardianContacts(request, [Contact(4)]);
        child.ValidatePreparedHostedGuardianContacts(ready);
        child.CommitHostedGuardianContacts(ready);
        Assert.Null(child.PendingHostedGuardianBoost);
        Assert.Equal((ushort)4, child.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary.Amount6);
        Assert.Same(original.ReadHeader(Header), child.NativeActionPackets.ReadHeader(Header));
        Assert.Equal(token, child.HostedActiveActionToken);
        Assert.Throws<InvalidOperationException>(() => child.CommitHostedGuardianContacts(ready));
    }

    private static (BattleSession Child, long Token) Create(bool learned = true)
    {
        BattleNativeAction native = new(254, 64, 2, 0) { MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 72, AttackFlags = 33, DamagePresentation = 5, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = 0, LevelModifier = 60, RawRecord = new byte[132].ToImmutableArray(), SupplementalRecord = [] };
        BattleActionDefinition action = new(BattleAbility.GuardianForce, 0, "Boost fixture", BattleActionEffect.Native, 44, false) { NativeAction = native, NativeGuardianBoostAvailable = learned };
        BattleSession child = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            NativeBattleUiRandom = new(1),
            ActionDefinitions = [action],
            Combatants = [new() { Id = "first", Name = "First", IsPlayer = true }, new() { Id = "second", Name = "Second", IsPlayer = true }, new() { Id = "third", Name = "Third", IsPlayer = true }, new() { Id = "fourth", Name = "Fourth", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        BattleModelRoster roster = child.ReadBattlePresentationRoster();
        NativeBattleModelAliases aliases = new(roster, []);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner.Side == BattleSide.Allies ? (byte)1 : (byte)16 });
        SetGraphics(child, graphics with { EntryReady = true, UiServices = NativeBattleUiServices.Initialize() });
        Field("nativeActionPackets").SetValue(child, NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases));
        Field("hostedBattlePresentation").SetValue(child, true);
        NativeBattleHostedTestFixture.InstallModelBindings(child, new NativeBattleGraphicsBinding("fourth", 1, new([0, 0])));
        long token = child.BeginHostedAction(new(Caster, 254, 1, -1, 64, 116, null), [Contact(0)]);
        Field("nativeActionPackets").SetValue(child, child.NativeActionPackets.ActivateHeader(Header));
        NativeBattleGraphicsState registered = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("RegisterNativeGuardianBoost", PrivateInstance)!.Invoke(child, [child.NativeGraphics!, (ushort)64])!;
        SetGraphics(child, registered);
        return (child, token);
    }

    private static NativeBattleContactPacket Contact(ushort amount) => new(new(Enemy, 0, 1, 0, 0, amount, 0), null);

    private static void Poll(BattleSession child, bool publish = true, bool paused = false, bool postCore = false)
    {
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        NativeBattleUiServices ui = graphics.UiServices! with { PauseDE9 = paused };
        SetGraphics(child, graphics with { UiServices = publish ? ui.QueuePublication(1) : ui });
        typeof(BattleSession).GetMethod("AdvanceHostedUiPublication", PrivateInstance)!.Invoke(child, [null, postCore]);
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
    private static void SetWidget(BattleSession child, NativeBattleGuardianBoost widget) => Field("nativeGuardianBoost").SetValue(child, widget);
    private static void SetGraphics(BattleSession child, NativeBattleGraphicsState graphics) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(child, graphics);
}
