namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionGeometryTests
{
    [Theory]
    [InlineData(0, 0, 0, 4096)]
    [InlineData(3000, 0, 4000, 4096)]
    [InlineData(0, 0, 2500, 2048)]
    [InlineData(0, 0, 10000, 8192)]
    [InlineData(0, 0, 20000, 16384)]
    public void Native_distance_retains_signed_WORD_scale_and_vector_reduction(short x, short y, short z, short scale)
    {
        Assert.Equal(scale, NativeBattleActionGeometryRules.DistanceScale(new(0, 0, 0), new(x, y, z)));
    }

    [Theory]
    [InlineData(4096, 2048, 2048, 3000)]
    [InlineData(2048, 4096, 4096, -1500)]
    [InlineData(-32768, 4096, 4096, -44000)]
    public void Contact_distance_subtracts_both_model_scales_after_the_native_WORD_store(short distance, short actorScale, short recipientScale, int expected)
    {
        Assert.Equal(expected, NativeBattleActionGeometryRules.ContactDistance(distance, actorScale, recipientScale));
    }

    [Fact]
    public void Unresolved_target_fields_remain_unknown_and_invalid_physical_indices_are_rejected()
    {
        NativeBattleActionGeometry context = new(0, 0xA007);
        context.Validate();
        Assert.Null(context.PhysicalTargetMask);
        Assert.Null(context.ContactDistance);
        Assert.Throws<InvalidDataException>(() => (context with { TargetIndex = 7 }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { PhysicalTargetMask = 0x8000 }).Validate());
    }
}
