namespace Memoria.FF8.Ui.Tests;

public sealed class UiColumnPickerTests
{
    [Fact]
    public void ColumnsMeasureNamesAndQuantitiesIndependentlyAndKeepTheRequestedGutters()
    {
        UiColumnPicker picker = new() { RowHeight = 26 };
        UiLabel firstName = new() { Text = "Fire", Width = 40 };
        UiLabel firstQuantity = new() { Text = "100", Width = 12 };
        UiLabel secondName = new() { Text = "Thundaga", Width = 80 };
        UiLabel secondQuantity = new() { Text = "10000", Width = 24 };
        UiLabel thirdName = new() { Text = "Ice", Width = 32 };
        UiLabel thirdQuantity = new() { Text = "100", Width = 16 };
        UiButton first = new();
        UiButton second = new();
        UiButton third = new();
        picker.Add(first, firstName, firstQuantity);
        picker.Add(second, secondName, secondQuantity);
        picker.Add(third, thirdName, thirdQuantity);
        picker.Arrange(new(10, 20, 200, 52));
        Assert.Equal(2, picker.RowCount);
        Assert.Equal(80, firstName.Bounds.Width);
        Assert.Equal(firstQuantity.Bounds.X, secondQuantity.Bounds.X);
        Assert.Equal(8, firstQuantity.Bounds.X - firstName.Bounds.Right);
        Assert.Equal(24, firstQuantity.Bounds.Width);
        Assert.Equal(16, third.Bounds.X - first.Bounds.Right);
        Assert.Equal(first.Bounds.Y, third.Bounds.Y);
        Assert.Equal(first.Bounds.Bottom, second.Bounds.Y);
        Assert.False(picker.Focusable);
    }

    [Fact]
    public void ArrowsFollowColumnFlowAndRevealWholeColumnsThroughTheSharedView()
    {
        (UiColumnPicker picker, UiButton[] buttons, UiView view) = Create();
        Assert.True(view.Focus(buttons[0]));
        Assert.Equal(0, picker.FirstVisibleColumn);
        Assert.False(buttons[4].Visible);
        Assert.True(view.Key(UiKey.Down));
        Assert.Same(buttons[1], view.FocusedElement);
        Assert.True(view.Key(UiKey.Down));
        Assert.Same(buttons[2], view.FocusedElement);
        Assert.True(view.Key(UiKey.Right));
        Assert.Same(buttons[4], view.FocusedElement);
        Assert.Equal(1, picker.FirstVisibleColumn);
        Assert.False(buttons[0].Visible);
        Assert.True(buttons[4].Visible);
        Assert.True(buttons[4].Bounds.Right <= picker.Bounds.Right);
        Assert.True(view.Key(UiKey.Left));
        Assert.Same(buttons[2], view.FocusedElement);
        Assert.True(view.Key(UiKey.Left));
        Assert.Same(buttons[0], view.FocusedElement);
        Assert.Equal(0, picker.FirstVisibleColumn);
        Assert.True(view.Key(UiKey.Up));
        Assert.Same(buttons[0], view.FocusedElement);
    }

    [Fact]
    public void InitialSelectionAndTheIncompleteFinalColumnRemainReachable()
    {
        (UiColumnPicker picker, UiButton[] buttons, UiView view) = Create(5, 4);
        Assert.Equal(1, picker.FirstVisibleColumn);
        Assert.True(view.Focus(buttons[3]));
        Assert.True(view.Key(UiKey.Right));
        Assert.Same(buttons[4], view.FocusedElement);
        Assert.True(view.Key(UiKey.Right));
        Assert.Same(buttons[4], view.FocusedElement);
        Assert.True(view.Key(UiKey.Up));
        Assert.Same(buttons[3], view.FocusedElement);
    }

    private static (UiColumnPicker Picker, UiButton[] Buttons, UiView View) Create(int count = 6, int initial = 0)
    {
        UiColumnPicker picker = new() { RowHeight = 26, InitialIndex = initial };
        UiButton[] buttons = new UiButton[count];
        for (int index = 0; index < count; index++)
        {
            buttons[index] = new UiButton();
            picker.Add(buttons[index], new UiLabel { Text = "Magic", Width = 40 }, new UiLabel { Text = "100", Width = 20 });
        }
        UiView view = new(picker);
        view.Resize(160, 52);
        return (picker, buttons, view);
    }
}
