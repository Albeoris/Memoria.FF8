using Memoria.FF7.Formats;
using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7NativeRowPopupTests
{
    [Theory]
    [InlineData(0x31)]
    [InlineData(0x32)]
    public void Native_non_numeric_result_flag_suppresses_the_zero_popup(int marker)
    {
        BattleUiTests.TestCanvas canvas = new();
        Ff7Scene.DrawBattleDamagePopup(canvas, new(255, 255, 255), 1, new(0, 0, 960, 540), Camera(), new(0, 0, 1000), new(false, false, 0), 0, marker, suppressNumericPopup: true);
        Assert.Empty(canvas.TextDraws);
    }

    [Theory]
    [InlineData(0x31, 0)]
    [InlineData(0x32, 0)]
    [InlineData(0x31, 7)]
    [InlineData(0x32, 7)]
    public void Reaction_marker_alone_does_not_suppress_an_ordinary_native_numeric_result(int marker, int damage)
    {
        BattleUiTests.TestCanvas canvas = new();
        Ff7Scene.DrawBattleDamagePopup(canvas, new(255, 255, 255), 1, new(0, 0, 960, 540), Camera(), new(0, 0, 1000), new(false, false, damage), 0, marker);
        Assert.NotEmpty(canvas.TextDraws);
        Assert.All(canvas.TextDraws, draw => Assert.Equal(damage.ToString(System.Globalization.CultureInfo.InvariantCulture), draw.Text));
    }

    [Theory]
    [InlineData(true, 0, "Miss")]
    [InlineData(false, -3, "Full Cure")]
    [InlineData(false, -2, "Death")]
    public void Ordinary_miss_and_special_result_labels_keep_their_existing_owner(bool missed, int marker, string expected)
    {
        BattleUiTests.TestCanvas canvas = new();
        Ff7Scene.DrawBattleDamagePopup(canvas, new(255, 255, 255), 1, new(0, 0, 960, 540), Camera(), new(0, 0, 1000), new(missed, false, 0), 0, marker, suppressNumericPopup: false);
        Assert.NotEmpty(canvas.TextDraws);
        Assert.All(canvas.TextDraws, draw => Assert.Equal(expected, draw.Text));
    }

    private static SceneCameraPlacement Camera() => new(Vector3.Zero, new(0, 0, 1000)) { FocalLength = 480 };
}
