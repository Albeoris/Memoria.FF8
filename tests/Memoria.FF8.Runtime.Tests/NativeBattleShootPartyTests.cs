using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShootPartyTests
{
    [Fact]
    public void Old_one_character_preparation_is_rejected_before_the_ball_uses_an_unallocated_rig()
    {
        (BattleActorModels models, NativeBattleModelBoundary bindings) = OriginalModels([(0, new(3))]);
        Assert.StartsWith("SHOOT_PARTY_PRECONDITION:", Assert.Throws<InvalidDataException>(() => NativeBattleShootParty.RequireBallOwner(models, bindings)).Message);
        (bindings, models) = bindings.BindOriginalSlot(NativeBattleModelSlot.Decode(1), OriginalOwner(1), models);
        models = models.Set(OriginalOwner(1), new(3));
        NativeBattleShootParty.RequireBallOwner(models, bindings);
        Assert.Equal((ushort)0, bindings.ReadOriginalCell(NativeBattleModelSlot.Decode(2), models).Flags0);
    }
}
