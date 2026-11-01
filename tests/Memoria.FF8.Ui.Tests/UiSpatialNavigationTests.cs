namespace Memoria.FF8.Ui.Tests;

public sealed class UiSpatialNavigationTests
{
    [Theory]
    [InlineData(UiKey.Up, 1)]
    [InlineData(UiKey.Down, 2)]
    [InlineData(UiKey.Left, 3)]
    [InlineData(UiKey.Right, 4)]
    public void ArrowsChooseAlignedNeighboursRegardlessOfTreeOrder(UiKey direction, int expected)
    {
        UiButton origin = At(100, 100, 40, 30);
        UiButton up = At(100, 40, 40, 30);
        UiButton down = At(100, 160, 40, 30);
        UiButton left = At(40, 100, 40, 30);
        UiButton right = At(160, 100, 40, 30);
        UiButton diagonal = At(180, 40, 20, 30);
        UiButton[] controls = [origin, up, down, left, right];
        UiView view = View(diagonal, right, down, origin, up, left);
        Assert.True(view.Focus(origin));
        Assert.True(view.Key(direction));
        Assert.Same(controls[expected], view.FocusedElement);
    }

    [Fact]
    public void DifferentWidthInTheSameColumnIsNotAHorizontalNeighbourAndArrowsDoNotWrap()
    {
        UiButton origin = At(20, 20, 100, 30);
        UiButton sameColumn = At(20, 70, 200, 30);
        UiButton right = At(150, 20, 30, 30);
        UiView view = View(sameColumn, origin, right);
        view.Focus(origin);
        Assert.True(view.Key(UiKey.Right));
        Assert.Same(right, view.FocusedElement);
        Assert.False(view.Key(UiKey.Right));
        Assert.Same(right, view.FocusedElement);
        Assert.True(view.Key(UiKey.Tab));
        Assert.Same(sameColumn, view.FocusedElement);
    }

    [Fact]
    public void NavigationHonorsVisibilityEnabledAncestorsClippingAndScope()
    {
        UiButton origin = At(10, 100, 20, 20);
        UiButton hidden = At(40, 100, 20, 20);
        hidden.Visible = false;
        UiButton disabled = At(50, 100, 20, 20);
        disabled.Enabled = false;
        UiButton inactive = At(60, 100, 20, 20);
        UiButton blockedChild = At(70, 100, 20, 20);
        FixedLayout disabledParent = new() { Enabled = false };
        disabledParent.Arrange(new(0, 0, 500, 500));
        disabledParent.Children.Add(blockedChild);
        UiButton clipped = At(80, 100, 20, 20);
        FixedLayout clip = new();
        clip.Arrange(new(0, 0, 10, 10));
        clip.Children.Add(clipped);
        UiButton target = At(100, 100, 20, 20);
        UiView view = View(origin, hidden, disabled, inactive, disabledParent, clip, target);
        view.Focus(origin);
        Assert.True(view.MoveFocus(UiKey.Right, element => element != inactive));
        Assert.Same(target, view.FocusedElement);
    }

    [Fact]
    public void NearestAncestorCanRevealOrBlockNavigationBeforeTheGenericSearch()
    {
        UiButton origin = At(10, 10, 20, 20);
        UiButton next = At(80, 10, 20, 20);
        NavigationContainer inner = new() { Target = next };
        inner.Arrange(new(0, 0, 200, 200));
        inner.Children.Add(origin);
        inner.Children.Add(next);
        NavigationContainer outer = new() { Target = origin };
        outer.Arrange(new(0, 0, 400, 400));
        outer.Children.Add(inner);
        UiView view = new(outer);
        view.Focus(origin);
        Assert.True(view.Key(UiKey.Right));
        Assert.Same(next, view.FocusedElement);
        Assert.Equal(1, inner.Calls);
        Assert.Equal(0, outer.Calls);
        inner.Target = null;
        Assert.True(view.Key(UiKey.Left));
        Assert.Same(next, view.FocusedElement);
    }

    private static UiButton At(float x, float y, float width, float height)
    {
        UiButton button = new();
        button.Arrange(new(x, y, width, height));
        return button;
    }

    private static UiView View(params UiElement[] children)
    {
        FixedLayout root = new();
        root.Arrange(new(0, 0, 500, 500));
        foreach (UiElement child in children)
        {
            root.Children.Add(child);
        }
        return new(root);
    }

    private class FixedLayout : UiElement
    {
        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
        }
    }

    private sealed class NavigationContainer : FixedLayout
    {
        public UiElement? Target { get; set; }
        public int Calls { get; private set; }

        public override bool TryNavigate(UiElement origin, UiKey key, out UiElement? target)
        {
            Calls++;
            target = Target;
            return true;
        }
    }
}
