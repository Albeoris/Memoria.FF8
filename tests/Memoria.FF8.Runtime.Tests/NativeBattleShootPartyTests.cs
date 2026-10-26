namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShootPartyTests
{
    [Fact]
    public void Old_one_character_preparation_is_rejected_before_the_ball_uses_an_unallocated_rig()
    {
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index == 0 ? (ushort)3 : (ushort)0)).ToArray();
        Assert.StartsWith("SHOOT_PARTY_PRECONDITION:", Assert.Throws<InvalidDataException>(() => NativeBattleShootParty.RequireBallOwner(models)).Message);
        models[1] = models[1] with { Flags0 = 3 };
        NativeBattleShootParty.RequireBallOwner(models);
        Assert.Equal((ushort)0, models[2].Flags0);
    }
}
