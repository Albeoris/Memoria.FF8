namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectIdentityTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(38, 37)]
    [InlineData(185, 184)]
    [InlineData(201, 200)]
    [InlineData(205, 204)]
    [InlineData(222, 221)]
    [InlineData(400, 399)]
    public void Binding_effects_use_the_original_zero_based_table_index(int binding, int nativeIndex)
    {
        Assert.Equal(nativeIndex, NativeBattleEffectIdentity.IndexFromBindingEffect(binding));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(401)]
    public void A_native_index_cannot_be_passed_as_an_invalid_binding_effect(int effect)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleEffectIdentity.IndexFromBindingEffect(effect));
    }

    [Theory]
    [InlineData(201, 200, "mag200_b.00")]
    [InlineData(205, 204, "mag204_b.00")]
    [InlineData(223, 222, "mag222_b.00")]
    public void Native_resource_profiles_keep_their_actual_filenames_and_distinct_binding_identity(int effect, int nativeIndex, string originalFile)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        Assert.Equal(effect, profile.Effect);
        Assert.Equal(originalFile, profile.InitialFile);
        Assert.Equal(nativeIndex, NativeBattleEffectIdentity.IndexFromBindingEffect(profile.Effect));
    }
}
