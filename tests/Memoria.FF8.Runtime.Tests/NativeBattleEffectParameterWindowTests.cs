namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectParameterWindowTests
{
    [Fact]
    public void RebindingDoesNotChangeTheFirstWritersOriginalBytes()
    {
        BattlePartyActorModel source = new(new("source"));
        BattleEnemyActorModel target = new(new("target"));
        BattleModelRoster roster = new([source, target]);
        NativeBattleModelAliases first = new(roster, [(NativeBattleModelSlot.Decode(2), source), (NativeBattleModelSlot.Decode(5), target)]);
        NativeBattleModelAliases later = new(roster, [(NativeBattleModelSlot.Decode(1), source), (NativeBattleModelSlot.Decode(6), target)]);
        NativeBattleEffectParameters parameters = new(source, 0x11, BattleModelTargets.From([target]), new(1), 0, 0);
        NativeBattleEffectParameters captured = parameters.CaptureOriginalWindow(first);
        NativeBattleEffectParameters second = captured.CaptureOriginalWindow(later);

        Assert.Equal(new byte[] { 2, 0x11, 32, 0, 0xD8, 0x80, 0xD2, 1 }, NativeBattleCinematicParameterBytes.Read(captured, 0, 8));
        Assert.Equal(new byte[] { 1, 0x11, 64, 0 }, NativeBattleCinematicParameterBytes.Read(second, 0, 4));
        Assert.Equal(new byte[] { 7 }, NativeBattleCinematicParameterBytes.Read(captured with { Parameter81 = 7 }, 9, 1));
    }

    [Fact]
    public void MissingActorAliasesAndOverflowHeadersRemainUnknown()
    {
        BattlePartyActorModel added = new(new("added"));
        BattleEnemyActorModel original = new(new("target"));
        BattleModelRoster roster = new([added, original]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(3), original)]);
        NativeBattleEffectParameters parameters = new(added, 3, BattleModelTargets.From([original, added]), new(32), 0, 0);
        NativeBattleEffectParameters captured = parameters.CaptureOriginalWindow(aliases);

        Assert.Equal(new byte[] { 3 }, NativeBattleCinematicParameterBytes.Read(captured, 1, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(captured, 0, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(captured, 2, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(captured, 4, 4));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(parameters, 1, 1));
    }
}
