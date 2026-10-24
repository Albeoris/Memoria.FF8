using System.Collections.Immutable;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeZombieRevivalHudTests
{
    [Theory]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void RevivalTargetsIncludeZombieAndKoButExcludeOrdinaryLivingAlly(byte type, bool draw)
    {
        BattleNativeAction native = new(2, 24, 1, 24) { MagicId = 0, TargetFlags = 1, AttackType = type, Power = 0, HitCount = 1, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 255, Statuses0 = 1, Statuses1 = 0, Hit = 255, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
        BattleSession battle = new(new BattleDefinition
        {
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0)),
            NativeActions = [native],
            ActionDefinitions = [new(BattleAbility.Magic, 24, "Revive", BattleActionEffect.Native, 0, true) { NativeAction = native }],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [draw ? BattleAbility.Draw : BattleAbility.Magic], MagicStocks = [new(24, 3)] },
                new() { Id = "zombie", Name = "Zombie", IsPlayer = true, NativeStatus0 = 0x40 },
                new() { Id = "fallen", Name = "Fallen", IsPlayer = true, CurrentHp = 0, NativeStatus0 = 1 },
                new() { Id = "source", Name = "Source", NativeDrawSlots = [new(24, 0), new(0, 0), new(0, 0), new(0, 0)] }
            ]
        }, BattleAtbMode.TurnBased);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        if (draw)
        {
            Assert.True(hud.SelectingDrawSource);
            hud.SelectTarget("source", true);
            Assert.True(hud.SelectingAction);
            hud.Choose(0);
            Assert.True(hud.SelectingDrawMode);
            hud.Navigate(MenuInput.Right);
            hud.Navigate(MenuInput.Confirm);
            Assert.True(hud.SelectingDrawCastRecipient);
        }
        else
        {
            hud.Choose(0);
        }
        Assert.Equal(new[] { "zombie", "fallen" }, hud.Targets.Select(target => target.Id));
        hud.SelectTarget("zombie", true);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(draw ? "source" : "zombie", input.TargetId);
        Assert.Equal(draw ? "zombie" : null, input.DrawCastTargetId);
    }
}
