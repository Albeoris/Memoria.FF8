namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRemainingComponentRequestTests
{
    [Theory]
    [InlineData((ushort)253, (ushort)283, (byte)3)]
    [InlineData((ushort)254, (ushort)281, (byte)5)]
    [InlineData((ushort)255, (ushort)282, (byte)4)]
    public void Original_trio_components_select_their_own_authored_actor_in_the_preserved_formation(ushort effect, ushort action, byte caster)
    {
        NativeEffectFixtureRequest request = NativeBattleRemainingComponentSources.OriginalRequest(effect);
        Assert.Equal(794, request.EncounterId);
        Assert.True(request.PreserveOriginalFormationEnemies);
        Assert.Equal(caster, request.CasterActorIndex);
        Assert.Equal((byte)8, request.Command);
        Assert.Equal(action, request.ActionId);
        Assert.Equal(effect, request.Effect);
        Assert.Null(request.ControlledShellEffect);
        Assert.Equal((byte)0, request.TargetActorIndex);
    }

    [Fact]
    public void Original265_component_keeps_the_authored_hidden_companion_as_physical_caster4()
    {
        NativeEffectFixtureRequest request = NativeBattleRemainingComponentSources.OriginalRequest(265);
        Assert.Equal(363, request.EncounterId);
        Assert.True(request.PreserveOriginalFormationEnemies);
        Assert.Equal((byte)4, request.CasterActorIndex);
        Assert.Equal((byte)8, request.Command);
        Assert.Equal((ushort)259, request.ActionId);
        Assert.Equal((ushort)265, request.Effect);
        Assert.Null(request.ControlledShellEffect);
        Assert.Equal((byte)0, request.TargetActorIndex);
    }

}
