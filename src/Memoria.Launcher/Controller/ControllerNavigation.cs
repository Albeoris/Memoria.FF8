using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Memoria.Launcher.Controller;

internal sealed class ControllerNavigation : IDisposable
{
    private static readonly ConditionalWeakTable<ListBox, Action> ListCommitActions = new();
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly ControllerInput input = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly IDisposable windowOpened;
    private readonly IDisposable windowClosed;
    private Window? activeWindow;
    private Control? controllerFocus;
    private ListBox? editingList;
    private PixelPoint? lastPointerPosition;
    private bool controllerMode;
    private int inputScope;

    public ControllerNavigation(IClassicDesktopStyleApplicationLifetime desktop)
    {
        this.desktop = desktop;
        windowOpened = Window.WindowOpenedEvent.AddClassHandler<Window>(OnOpened);
        windowClosed = Window.WindowClosedEvent.AddClassHandler<Window>(OnClosed);
        desktop.Exit += OnExit;
        timer.Tick += Poll;
        timer.Start();
    }

    public static bool IsControllerMode(Window window)
    {
        return Application.Current is LauncherApplication { Navigation: { } navigation }
            && navigation.controllerMode && ReferenceEquals(window, navigation.activeWindow);
    }

    public static void SetListCommitAction(ListBox list, Action action)
    {
        ListCommitActions.Add(list, action);
    }

    public void Dispose()
    {
        desktop.Exit -= OnExit;
        timer.Stop();
        timer.Tick -= Poll;
        windowOpened.Dispose();
        windowClosed.Dispose();
        foreach (Window window in desktop.Windows)
        {
            Detach(window);
        }
        input.Dispose();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Dispose();
    }

    private void OnOpened(Window window, RoutedEventArgs e)
    {
        Detach(window);
        SetActiveWindow(null);
        window.Deactivated += OnDeactivated;
        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
    }

    private void OnClosed(Window window, RoutedEventArgs e)
    {
        Detach(window);
        if (ReferenceEquals(window, activeWindow))
        {
            SetActiveWindow(null);
        }
    }

    private void Detach(Window window)
    {
        window.Deactivated -= OnDeactivated;
        window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        window.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        window.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel);
        window.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, activeWindow))
        {
            SetActiveWindow(null);
        }
    }

    private void SetActiveWindow(Window? window)
    {
        activeWindow = window;
        editingList = null;
        controllerFocus = null;
        inputScope++;
        input.Suppress();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        LeaveControllerMode(false);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        LeaveControllerMode(true);
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        LeaveControllerMode(true);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Window window && e.Pointer.Type == PointerType.Mouse)
        {
            PixelPoint position = window.PointToScreen(e.GetPosition(window));
            if (lastPointerPosition is not PixelPoint previous || Math.Abs(position.X - previous.X) >= 4 || Math.Abs(position.Y - previous.Y) >= 4)
            {
                if (lastPointerPosition.HasValue)
                {
                    LeaveControllerMode(true);
                }
                lastPointerPosition = position;
            }
        }
    }

    private void LeaveControllerMode(bool pointer)
    {
        if (!controllerMode)
        {
            return;
        }

        controllerMode = false;
        inputScope++;
        input.Suppress();
        editingList = null;
        if (pointer && controllerFocus is { IsFocused: true })
        {
            activeWindow?.FocusManager?.ClearFocus();
        }
        controllerFocus = null;
    }

    private void Poll(object? sender, EventArgs e)
    {
        Window? window = desktop.Windows.LastOrDefault(window => window.IsActive && window.IsVisible && window.WindowState != WindowState.Minimized);
        if (!ReferenceEquals(activeWindow, window))
        {
            SetActiveWindow(window);
        }

        int scope = inputScope;
        input.Poll(window is not null, action =>
        {
            if (window is { IsActive: true } && inputScope == scope)
            {
                Handle(window, action);
            }
        });
    }

    private void Handle(Window window, ControllerAction action)
    {
        controllerMode = true;
        Control[] candidates = window.GetVisualDescendants().OfType<Control>().Where(IsCandidate).ToArray();
        if (candidates.OfType<ComboBox>().FirstOrDefault(combo => combo.IsDropDownOpen) is ComboBox openCombo)
        {
            HandleComboBox(window, openCombo, action);
            return;
        }

        var focused = window.FocusManager?.GetFocusedElement() as Control;
        Control? current = focused is ListBoxItem ? focused.FindAncestorOfType<ListBox>() : focused;
        if (current is not null && !candidates.Contains(current))
        {
            current = null;
        }
        if (editingList is ListBox list && candidates.Contains(list))
        {
            HandleList(window, list, action);
            return;
        }
        editingList = null;

        if (action is ControllerAction.Cancel)
        {
            if (candidates.OfType<Button>().FirstOrDefault(button => button.IsCancel) is Button cancel)
            {
                new ButtonAutomationPeer(cancel).Invoke();
            }
            else if (window is not LauncherWindow)
            {
                window.Close();
            }
            return;
        }

        if (action is ControllerAction.Previous or ControllerAction.Next)
        {
            CycleTab(window, current, candidates, action == ControllerAction.Previous ? -1 : 1);
            return;
        }

        if (action is ControllerAction.ScrollUp or ControllerAction.ScrollDown)
        {
            Scroll(window, current, action == ControllerAction.ScrollUp ? -1 : 1);
            return;
        }

        if (current is null)
        {
            Control? initial = candidates.OfType<Button>().FirstOrDefault(button => button.IsDefault) ?? candidates.FirstOrDefault();
            if (initial is not null)
            {
                Focus(window, initial);
            }
            return;
        }

        if (action == ControllerAction.Confirm)
        {
            Focus(window, current);
            Activate(window, current);
            return;
        }

        if (GetBounds(window, current) is Rect origin)
        {
            var targets = candidates.Where(control => !ReferenceEquals(control, current))
                .Select(control => (Control: control, Bounds: GetBounds(window, control)))
                .Where(candidate => candidate.Bounds.HasValue)
                .Select(candidate => (candidate.Control, candidate.Bounds.GetValueOrDefault()));
            current = SpatialNavigation.FindNext(origin, targets, action) ?? current;
        }
        Focus(window, current);
    }

    private static bool IsCandidate(Control control)
    {
        if (!control.Focusable || !control.IsEffectivelyVisible || !control.IsEffectivelyEnabled || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return false;
        }

        // Ignore template internals and navigate compound controls as a unit.
        return control.TemplatedParent is null && control is Button or ComboBox or ListBox { ItemCount: > 0 } or TabItem or TextBox { IsReadOnly: false }
            || control is ToggleButton { TemplatedParent: Expander };
    }

    private static Rect? GetBounds(Window window, Control control)
    {
        if (control.TransformToVisual(window) is Matrix transform)
        {
            return new Rect(control.Bounds.Size).TransformToAABB(transform);
        }
        return null;
    }

    private void Focus(Window window, Control control)
    {
        if (ReferenceEquals(window.FocusManager?.GetFocusedElement(), control) && !ReferenceEquals(controllerFocus, control))
        {
            // Reapply focus after pointer input to request Avalonia's directional focus adorner.
            window.FocusManager?.ClearFocus();
        }
        if (control.Focus(NavigationMethod.Directional))
        {
            controllerFocus = control;
            control.BringIntoView();
        }
    }

    private void Activate(Window window, Control control)
    {
        switch (control)
        {
            case Button button:
                new ButtonAutomationPeer(button).Invoke();
                break;
            case ComboBox combo when combo.ItemCount > 0:
                combo.IsDropDownOpen = true;
                break;
            case ListBox list:
                editingList = list;
                FocusListItem(window, list, Math.Max(0, list.SelectedIndex));
                break;
            case TabItem tab:
                tab.IsSelected = true;
                break;
            case TextBox text when window is not ControllerTextEntry:
                _ = ControllerTextEntry.EditAsync(window, text);
                break;
        }
    }

    private void HandleComboBox(Window window, ComboBox combo, ControllerAction action)
    {
        int focusedIndex = combo.GetRealizedContainers().Where(item => item.IsFocused).Select(combo.IndexFromContainer).FirstOrDefault(-1);
        if (action is ControllerAction.Confirm or ControllerAction.Cancel)
        {
            combo.IsDropDownOpen = false;
            Focus(window, combo);
            if (action == ControllerAction.Confirm && focusedIndex >= 0)
            {
                combo.SelectedIndex = focusedIndex;
            }
            return;
        }

        if (action is ControllerAction.Up or ControllerAction.Down)
        {
            int direction = action == ControllerAction.Up ? -1 : 1;
            int index = focusedIndex >= 0 ? focusedIndex + direction : Math.Max(0, combo.SelectedIndex);
            for (; index >= 0 && index < combo.ItemCount; index += direction)
            {
                combo.ScrollIntoView(index);
                if (combo.ContainerFromIndex(index) is Control item && item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Directional))
                {
                    controllerFocus = item;
                    break;
                }
            }
        }
    }

    private void HandleList(Window window, ListBox list, ControllerAction action)
    {
        switch (action)
        {
            case ControllerAction.Cancel:
            case ControllerAction.Left:
            case ControllerAction.Right:
                editingList = null;
                Focus(window, list);
                break;
            case ControllerAction.Confirm:
                editingList = null;
                Focus(window, list);
                if (ListCommitActions.TryGetValue(list, out Action? commit))
                {
                    commit();
                }
                break;
            case ControllerAction.Up:
            case ControllerAction.Down:
                int direction = action == ControllerAction.Up ? -1 : 1;
                FocusListItem(window, list, Math.Clamp(list.SelectedIndex + direction, 0, list.ItemCount - 1));
                break;
            case ControllerAction.ScrollUp:
            case ControllerAction.ScrollDown:
                Scroll(window, list, action == ControllerAction.ScrollUp ? -1 : 1);
                break;
        }
    }

    private void FocusListItem(Window window, ListBox list, int index)
    {
        list.SelectedIndex = index;
        list.ScrollIntoView(index);
        if (list.ContainerFromIndex(index) is Control item)
        {
            Focus(window, item);
        }
    }

    private void CycleTab(Window window, Control? current, Control[] candidates, int direction)
    {
        TabControl? tabs = current?.GetVisualAncestors().OfType<TabControl>().FirstOrDefault()
            ?? candidates.OfType<TabItem>().FirstOrDefault()?.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();
        if (tabs is null)
        {
            return;
        }

        TabItem[] headers = candidates.OfType<TabItem>().Where(tab => ReferenceEquals(tab.FindAncestorOfType<TabControl>(), tabs)).ToArray();
        if (headers.Length == 0)
        {
            return;
        }

        int index = Math.Max(0, Array.FindIndex(headers, tab => tab.IsSelected));
        TabItem next = headers[(index + direction + headers.Length) % headers.Length];
        next.IsSelected = true;
        Focus(window, next);
    }

    private static void Scroll(Window window, Control? current, int direction)
    {
        IEnumerable<ScrollViewer> scrolls = current is null
            ? window.GetVisualDescendants().OfType<ScrollViewer>()
            : current.GetVisualDescendants().OfType<ScrollViewer>()
                .Concat(current.GetVisualAncestors().OfType<ScrollViewer>())
                .Concat(window.GetVisualDescendants().OfType<ScrollViewer>());
        foreach (ScrollViewer scroll in scrolls)
        {
            if (!scroll.IsEffectivelyVisible || scroll.Viewport.Height <= 0)
            {
                continue;
            }
            double y = Math.Clamp(scroll.Offset.Y + direction * 40, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
            if (Math.Abs(y - scroll.Offset.Y) > 0.01)
            {
                scroll.Offset = new Vector(scroll.Offset.X, y);
                break;
            }
        }
    }
}
