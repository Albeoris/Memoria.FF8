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
        Assert.Equal(learned, child.Presentation!.HostedGuardianBoost!.Enabled);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(Caster, out _));
        SetWidget(child, child.Presentation!.HostedGuardianBoost! with { Phase = 4, Remaining = 1, Window = 1, WindowRemaining = 10, Percent = percent });
        NativeBattleActionPacketsState original = child.NativeActionPackets;

        Poll(child, postCore: true);
        BattlePresentationProcess.HostedGuardianBoostRequest request = Assert.IsType<BattlePresentationProcess.HostedGuardianBoostRequest>(child.Presentation!.PendingHostedGuardianBoost);
        Assert.Equal(token, request.Token);
        Assert.Equal(Header, request.Header);
        Assert.Equal(Caster, request.Caster);
        Assert.Equal(expected, request.Percent);
        Assert.Equal((byte)6, child.Presentation!.HostedGuardianBoost!.Phase);
        Assert.Same(original, child.NativeActionPackets);
        Poll(child, publish: false);
        Assert.Null(child.Presentation!.HostedGuardianBoost);
        Assert.Same(request, child.Presentation!.PendingHostedGuardianBoost);
        Assert.Same(original, child.NativeActionPackets);
    }

    [Fact]
    public void HostedPollAppliesSelectBeforePublishingNewMappedButtonsAndHonorsPause()
    {
        (BattleSession child, _) = Create();
        SetWidget(child, child.Presentation!.HostedGuardianBoost! with { Phase = 4, Remaining = 10, Window = 1, WindowRemaining = 10, InputGrace = 4 });
        child.Presentation!.SetHostedGuardianBoostMappedButtons(0x180);
        Assert.True(child.Presentation!.PressHostedGuardianBoost());
        Assert.False(child.Presentation!.PressHostedGuardianBoost());
        Poll(child);
        Assert.Equal((ushort)0x180, child.Presentation!.Graphics.UiServices!.HeldButtons16);
        Assert.Equal(0, child.Presentation!.Graphics.UiServices.Flags942 & 1);
        Assert.Equal((ushort)0, child.Presentation!.HostedGuardianBoost!.Percent);

        Assert.True(child.Presentation!.PressHostedGuardianBoost());
        Poll(child);
        Assert.Equal(1, child.Presentation!.Graphics.UiServices!.Flags942 & 1);
        Assert.Equal((ushort)76, child.Presentation!.HostedGuardianBoost!.Percent);

        child.Presentation!.SetHostedGuardianBoostMappedButtons(0);
        Assert.True(child.Presentation!.PressHostedGuardianBoost());
        NativeBattleGuardianBoost pressed = child.Presentation!.HostedGuardianBoost!;
        Poll(child, paused: true);
        Assert.Same(pressed, child.Presentation!.HostedGuardianBoost);
        Assert.Equal((ushort)0, child.Presentation!.Graphics.UiServices!.HeldButtons16);
        Assert.Equal(1, child.Presentation!.Graphics.UiServices.Flags942 & 1);
        Poll(child, postCore: true);
        Assert.Equal(0, child.Presentation!.Graphics.UiServices!.Flags942 & 1);
        Assert.Equal((ushort)76, child.Presentation!.HostedGuardianBoost!.Percent);
        Assert.False(child.Presentation!.HostedGuardianBoost.PendingPress);
        Assert.Null(child.Presentation!.PendingHostedGuardianBoost);
    }

    [Fact]
    public void OnlyMatchingRequestAndPreparedContactReplacementCanAcknowledge()
    {
        (BattleSession child, long token) = Create();
        (BattleSession other, _) = Create();
        SetWidget(child, child.Presentation!.HostedGuardianBoost! with { Phase = 5 });
        Poll(child, publish: false);
        BattlePresentationProcess.HostedGuardianBoostRequest request = child.Presentation!.PendingHostedGuardianBoost!;
        NativeBattleActionPacketsState original = child.NativeActionPackets;

        Assert.Throws<InvalidOperationException>(() => child.Presentation!.PrepareHostedContactReplacement(token, [Contact(4)], acknowledgeGuardian: request with { }));
        Assert.Throws<InvalidOperationException>(() => other.Presentation!.PrepareHostedContactReplacement(token, [Contact(4)], acknowledgeGuardian: request));
        Assert.Throws<InvalidDataException>(() => child.Presentation!.PrepareHostedContactReplacement(token, [], acknowledgeGuardian: request));
        BattlePresentationProcess.PreparedHostedContactReplacement stale = child.Presentation!.PrepareHostedContactReplacement(token, [Contact(4)], acknowledgeGuardian: request);
        Poll(child, publish: false);
        Assert.Throws<InvalidOperationException>(() => child.Presentation!.PublishHostedContactReplacement(stale));
        Assert.Same(original, child.NativeActionPackets);
        Assert.Same(request, child.Presentation!.PendingHostedGuardianBoost);

        BattlePresentationProcess.PreparedHostedContactReplacement ready = child.Presentation!.PrepareHostedContactReplacement(token, [Contact(4)], acknowledgeGuardian: request);
        child.Presentation!.ValidatePreparedHostedContactReplacement(ready);
        child.Presentation!.PublishHostedContactReplacement(ready);
        Assert.Null(child.Presentation!.PendingHostedGuardianBoost);
        Assert.Equal((ushort)4, child.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary.Amount6);
        Assert.Same(original.ReadHeader(Header), child.NativeActionPackets.ReadHeader(Header));
        Assert.Equal(token, child.Presentation!.HostedActiveActionToken);
        Assert.Throws<InvalidOperationException>(() => child.Presentation!.PublishHostedContactReplacement(ready));
    }

    private static (BattleSession Child, long Token) Create(bool learned = true)
    {
        BattleNativeAction native = new(254, 64, 2, 0) { MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 72, AttackFlags = 33, DamagePresentation = 5, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = 0, LevelModifier = 60, RawRecord = new byte[132].ToImmutableArray(), SupplementalRecord = [] };
        BattleActionDefinition action = new(BattleAbility.GuardianForce, 0, "Boost fixture", BattleActionEffect.Native, 44, false) { NativeAction = native, NativeGuardianBoostAvailable = learned };
        BattleSession child = new(new BattleDefinition
        {
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
        long token = child.Presentation!.BeginHostedAction(new(Caster, 254, 1, -1, 64, 116, null), [Contact(0)]);
        Field("nativeActionPackets").SetValue(child, child.NativeActionPackets.ActivateHeader(Header));
        NativeBattleGraphicsState registered = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("RegisterNativeGuardianBoost", PrivateInstance)!.Invoke(child, [child.Presentation!.Graphics, (ushort)64])!;
        SetGraphics(child, registered);
        return (child, token);
    }

    private static NativeBattleContactPacket Contact(ushort amount) => new(new(Enemy, 0, 1, 0, 0, amount, 0), null);

    private static void Poll(BattleSession child, bool publish = true, bool paused = false, bool postCore = false)
    {
        NativeBattleGraphicsState graphics = child.Presentation!.Graphics;
        NativeBattleUiServices ui = graphics.UiServices! with { PauseDE9 = paused };
        SetGraphics(child, graphics with { UiServices = publish ? ui.QueuePublication(1) : ui });
        typeof(BattleSession).GetMethod("AdvanceHostedUiPublication", PrivateInstance)!.Invoke(child, [null, postCore]);
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
    private static void SetWidget(BattleSession child, NativeBattleGuardianBoost widget) => Field("nativeGuardianBoost").SetValue(child, widget);
    private static void SetGraphics(BattleSession child, NativeBattleGraphicsState graphics) => typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(child.Presentation!, graphics);
}
