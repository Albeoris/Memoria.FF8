namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect174ModelMemoryTests
{
    [Fact]
    public void Rotation_word_does_not_overwrite_adjacent_angles_or_padding()
    {
        NativeBattleGraphicsModel model = Model();
        NativeBattleGraphicsModel result = BattleSession.ApplyNativeEffect174ModelBytes(model, new(0, 12, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-321)), 2));
        Assert.Equal((short)-321, result.Rotation12);
        Assert.Equal(model.Rotation14, result.Rotation14);
        Assert.Equal(model.Rotation16, result.Rotation16);
        Assert.Equal(model.RotationPadding18, result.RotationPadding18);
    }

    [Theory]
    [InlineData(6, 44)]
    [InlineData(8, -77)]
    public void Position_write_preserves_W_unless_the_native_copy_includes_it(int length, short expectedW)
    {
        NativeBattleGraphicsModel model = Model();
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-11)).Write(2, BitConverter.GetBytes((short)22)).Write(4, BitConverter.GetBytes((short)-33)).Write(6, BitConverter.GetBytes((short)-77));
        NativeBattleGraphicsModel result = BattleSession.ApplyNativeEffect174ModelBytes(model, new(0, 28, source, length));
        Assert.Equal(new NativeBattleVector(-11, 22, -33), result.Transform!.Position28);
        Assert.Equal(expectedW, result.Transform.Vertical34);
        Assert.Equal(model.Transform!.Scale38, result.Transform.Scale38);
    }

    [Fact]
    public void Full_angle_restore_preserves_unknown_padding_without_reading_it()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-10)).Write(2, BitConverter.GetBytes((short)20)).Write(4, BitConverter.GetBytes((short)-30)).MarkUnknown(6, 2);
        NativeBattleGraphicsModel result = BattleSession.ApplyNativeEffect174ModelBytes(Model(), new(0, 12, source, 8));
        Assert.Equal((short)-10, result.Rotation12);
        Assert.Equal((short)20, result.Rotation14);
        Assert.Equal((short)-30, result.Rotation16);
        Assert.Null(result.RotationPadding18);
    }

    private static NativeBattleGraphicsModel Model() => new(0, 2) { Rotation12 = 1, Rotation14 = 2, Rotation16 = 3, RotationPadding18 = 0xCAFE, Transform = new(new(0, 0, 0), new(11, 22, 33), 44, 0, 700) };
}
