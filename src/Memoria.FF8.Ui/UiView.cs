namespace Memoria.FF8.Ui;

/// <summary>Controls whether a design space is letterboxed or expanded to the viewport with uniform scaling.</summary>
public enum UiViewportMode
{
    Fit,
    Expand
}

/// <summary>Owns focus, input routing and a uniformly scaled design coordinate space.</summary>
public sealed class UiView
{
    private bool _pointerPressed;
    private bool _hasPointerPosition;
    private float _pointerX;
    private float _pointerY;
    private float _viewportWidth;
    private float _viewportHeight;
    private float _designWidth;
    private float _designHeight;
    private bool _layoutDirty = true;
    private UiViewportMode _viewportMode;
    private UiElement? _focusedElement;

    public UiView(UiElement root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public UiElement Root { get; }
    public float ViewportWidth => _viewportWidth;
    public float ViewportHeight => _viewportHeight;
    public float DesignWidth
    {
        get => _designWidth;
        set
        {
            _designWidth = value;
            InvalidateLayout();
        }
    }

    public float DesignHeight
    {
        get => _designHeight;
        set
        {
            _designHeight = value;
            InvalidateLayout();
        }
    }
    public UiViewportMode ViewportMode
    {
        get => _viewportMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (_viewportMode != value)
            {
                _viewportMode = value;
                InvalidateLayout();
            }
        }
    }

    public Action? Cancel { get; set; }
    public UiElement? FocusedElement
    {
        get
        {
            EnsureLayout();
            EnsureFocus();
            return _focusedElement;
        }
    }

    public void Resize(float width, float height)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport dimensions must be finite and positive.");
        }

        if (_viewportWidth != width || _viewportHeight != height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
            InvalidateLayout();
        }

        EnsureLayout();
    }

    public void InvalidateLayout()
    {
        _layoutDirty = true;
    }

    private void EnsureLayout()
    {
        if (!_layoutDirty || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        Root.Arrange(LayoutBounds());
        _layoutDirty = false;
    }

    public void Draw(IUiCanvas canvas, UiTheme theme)
    {
        if (BindThemeMetrics(Root, theme))
        {
            InvalidateLayout();
        }
        EnsureLayout();
        EnsureFocus();
        GetTransform(out float scale, out float offsetX, out float offsetY);
        IUiCanvas transformed = scale == 1 && offsetX == 0 && offsetY == 0 ? canvas : new ScaledCanvas(canvas, scale, offsetX, offsetY);
        if (theme.FontFamily is { } fontFamily)
        {
            transformed = new UiFontCanvas(transformed, fontFamily);
        }
        if (BindTextMeasurements(Root, transformed) && _viewportWidth > 0 && _viewportHeight > 0)
        {
            Root.Arrange(LayoutBounds());
        }
        Root.Draw(transformed, theme);
        // Frame captions and selection pointers can extend beyond the panels' content clips.
        canvas.PushClip(new UiRect(0, 0, _viewportWidth, _viewportHeight));
        try
        {
            Root.DrawAdornments(transformed, theme);
        }
        finally
        {
            canvas.PopClip();
        }
    }

    private static bool BindThemeMetrics(UiElement element, UiTheme theme)
    {
        bool changed = false;
        if (element is UiPanel panel)
        {
            float required = panel.DrawBackground && !string.IsNullOrEmpty(panel.Caption) ? theme.CaptionContentHeight(panel.CaptionFontSize) : 0;
            if (panel.ThemeCaptionHeight != required)
            {
                panel.ThemeCaptionHeight = required;
                changed = true;
            }
        }
        foreach (UiElement child in element.Children)
        {
            changed |= BindThemeMetrics(child, theme);
        }
        return changed;
    }

    private static bool BindTextMeasurements(UiElement element, IUiCanvas canvas)
    {
        bool measured = false;
        if (element is UiLabel label)
        {
            label.MeasurementCanvas = canvas;
            measured = true;
        }
        foreach (UiElement child in element.Children)
        {
            measured |= BindTextMeasurements(child, canvas);
        }
        return measured;
    }

    public bool Focus(UiElement? element)
    {
        EnsureLayout();
        if (element != null && !FocusableElements().Contains(element))
        {
            return false;
        }

        SetFocus(element);
        return true;
    }

    public bool Key(UiKey key)
    {
        EnsureLayout();
        EnsureFocus();
        if (key == UiKey.Cancel)
        {
            Cancel?.Invoke();
            return Cancel != null;
        }

        if (key != UiKey.Tab && _focusedElement?.Key(key) == true)
        {
            return true;
        }

        int direction = key switch
        {
            UiKey.Tab or UiKey.Down or UiKey.Right => 1,
            UiKey.Up or UiKey.Left => -1,
            _ => 0
        };
        if (direction == 0)
        {
            return false;
        }

        List<UiElement> candidates = FocusableElements().ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        int index = _focusedElement == null ? -1 : candidates.IndexOf(_focusedElement);
        int next = (index + direction + candidates.Count) % candidates.Count;
        SetFocus(candidates[next]);
        return true;
    }

    public (float X, float Y) ToDesignPoint(float x, float y)
    {
        EnsureLayout();
        GetTransform(out float scale, out float offsetX, out float offsetY);
        return ((x - offsetX) / scale, (y - offsetY) / scale);
    }

    /// <summary>A press transition activates once; holding the pointer does not repeat activation.</summary>
    public void Pointer(float x, float y, bool pressed)
    {
        EnsureLayout();
        bool activate = pressed && !_pointerPressed;
        _pointerPressed = pressed;
        bool moved = !_hasPointerPosition || x != _pointerX || y != _pointerY;
        _hasPointerPosition = true;
        _pointerX = x;
        _pointerY = y;
        if (!moved && !activate)
        {
            return;
        }
        EnsureFocus();
        GetTransform(out float scale, out float offsetX, out float offsetY);
        float designX = (x - offsetX) / scale;
        float designY = (y - offsetY) / scale;
        UiElement? hit = Root.HitTest(designX, designY);
        if (hit == null)
        {
            return;
        }

        UiElement? focusTarget = FindFocusableAncestor(Root, hit, null);
        if (focusTarget != null)
        {
            if (moved)
            {
                focusTarget.PointerHover(designX, designY);
            }
            if (activate)
            {
                SetFocus(focusTarget);
                focusTarget.Pointer(designX, designY);
            }
        }
    }

    private static UiElement? FindFocusableAncestor(UiElement node, UiElement target, UiElement? ancestor)
    {
        UiElement? current = node.Focusable ? node : ancestor;
        if (ReferenceEquals(node, target))
        {
            return current;
        }

        foreach (UiElement child in node.Children)
        {
            UiElement? result = FindFocusableAncestor(child, target, current);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private IEnumerable<UiElement> FocusableElements()
    {
        return TraverseFocusable(Root, Root.Bounds);
    }

    private static IEnumerable<UiElement> TraverseFocusable(UiElement element, UiRect clip)
    {
        UiRect visibleBounds = clip.Intersect(element.Bounds);
        if (!element.Visible || !element.Enabled || visibleBounds.IsEmpty)
        {
            yield break;
        }

        if (element.Focusable)
        {
            yield return element;
        }

        foreach (UiElement child in element.Children)
        {
            foreach (UiElement candidate in TraverseFocusable(child, visibleBounds))
            {
                yield return candidate;
            }
        }
    }

    private void EnsureFocus()
    {
        List<UiElement> candidates = FocusableElements().ToList();
        if (_focusedElement == null || !candidates.Contains(_focusedElement))
        {
            SetFocus(candidates.FirstOrDefault());
        }
    }

    private void SetFocus(UiElement? element)
    {
        if (ReferenceEquals(_focusedElement, element))
        {
            return;
        }

        UiElement? previous = _focusedElement;
        if (previous != null)
        {
            previous.IsFocused = false;
        }

        _focusedElement = element;
        if (element != null)
        {
            element.IsFocused = true;
        }

        previous?.FocusChanged?.Invoke(false);
        element?.FocusChanged?.Invoke(true);
    }

    private UiRect LayoutBounds()
    {
        if (ViewportMode == UiViewportMode.Expand)
        {
            GetTransform(out float scale, out _, out _);
            return new UiRect(0, 0, _viewportWidth / scale, _viewportHeight / scale);
        }
        return new UiRect(0, 0, DesignWidth > 0 ? DesignWidth : _viewportWidth, DesignHeight > 0 ? DesignHeight : _viewportHeight);
    }

    private void GetTransform(out float scale, out float offsetX, out float offsetY)
    {
        scale = 1;
        offsetX = 0;
        offsetY = 0;
        if (DesignWidth > 0 && DesignHeight > 0 && _viewportWidth > 0 && _viewportHeight > 0)
        {
            scale = Math.Min(_viewportWidth / DesignWidth, _viewportHeight / DesignHeight);
            if (ViewportMode == UiViewportMode.Fit)
            {
                offsetX = (_viewportWidth - DesignWidth * scale) / 2;
                offsetY = (_viewportHeight - DesignHeight * scale) / 2;
            }
        }
    }

    private sealed class ScaledCanvas(IUiCanvas canvas, float scale, float offsetX, float offsetY) : IUiCanvas
    {
        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            UiSize measured = canvas.MeasureText(text, fontSize * scale, maximumWidth * scale);
            return new UiSize(measured.Width / scale, measured.Height / scale);
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? fontFamily)
        {
            UiSize measured = canvas.MeasureText(text, fontSize * scale, maximumWidth * scale, fontFamily);
            return new UiSize(measured.Width / scale, measured.Height / scale);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            canvas.Text(text, fontSize * scale, MapText(bounds), color, fontFamily, verticalAlignment, flow);
        }

        public void Paper(UiRect bounds, UiColor color)
        {
            canvas.Paper(Map(bounds), color);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            canvas.OutlinedText(text, fontSize * scale, MapText(bounds), color, outline, thickness * scale);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            canvas.Fill(Map(bounds), color);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            canvas.Text(text, fontSize * scale, MapText(bounds), color);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily)
        {
            canvas.Text(text, fontSize * scale, MapText(bounds), color, fontFamily);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            canvas.Text(text, fontSize * scale, MapText(bounds), color, verticalAlignment);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            canvas.Text(text, fontSize * scale, MapText(bounds), color, verticalAlignment, flow);
        }

        public void MenuPointer(UiRect bounds)
        {
            canvas.MenuPointer(Map(bounds));
        }

        public bool ModPreview(string url, UiRect bounds)
        {
            return canvas.ModPreview(url, Map(bounds));
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            canvas.Image(source, Map(bounds));
        }

        public void ImageStretched(UiImageSource source, UiRect bounds)
        {
            canvas.ImageStretched(source, Map(bounds));
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            canvas.Line(x1 * scale + offsetX, y1 * scale + offsetY, x2 * scale + offsetX, y2 * scale + offsetY, color, thickness * scale);
        }

        public void PushClip(UiRect bounds)
        {
            canvas.PushClip(Map(bounds));
        }

        public void PopClip()
        {
            canvas.PopClip();
        }

        private UiRect MapText(UiRect bounds)
        {
            return new UiRect(bounds.X * scale + offsetX, bounds.Y * scale + offsetY, bounds.Width * scale, bounds.Height * scale);
        }

        private UiRect Map(UiRect bounds)
        {
            float left = bounds.X * scale + offsetX;
            float top = bounds.Y * scale + offsetY;
            float right = bounds.Right * scale + offsetX;
            float bottom = bounds.Bottom * scale + offsetY;
            // Transform shared edges identically so translucent fills neither overlap nor leave gaps.
            return new UiRect(left, top, right - left, bottom - top);
        }
    }
}
