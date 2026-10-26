namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLazyBone502170Tests
{
    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)4)]
    [InlineData((ushort)0xFFFD)]
    public void Unloaded_model_returns_exact_position_words_without_resolving_a_resource(ushort flags)
    {
        NativeBattleGraphicsModel model = new(2, flags) { Transform = new(new(100, 200, 300), new(-32768, 1234, 32767), 55, 66, 4096) };
        NativeBattleVector actual = NativeBattlePoseMatrices.ReadBoneLazy(model, () => throw new InvalidOperationException("No skeleton belongs to the unloaded physical row."), 0xF1, 2048);
        Assert.Equal(new NativeBattleVector(-32768, 1234, 32767), actual);
    }

    [Fact]
    public void Loaded_model_still_requires_its_current_resource()
    {
        NativeBattleGraphicsModel model = new(1, 3) { Transform = new(new(0, 0, 0), new(1, 2, 3), 0, 0, 4096) };
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseMatrices.ReadBoneLazy(model, () => throw new InvalidDataException("The loaded row has no original resource."), 0xF1, 0));
    }

    [Fact]
    public void Unloaded_model_does_not_invent_missing_position_words()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadBoneLazy(new(2, 0), () => throw new InvalidOperationException(), 0xF0, 4096));
    }
}
