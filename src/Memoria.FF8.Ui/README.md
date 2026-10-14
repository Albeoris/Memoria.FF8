# Retained UI API

`Memoria.FF8.Ui` is a renderer-independent .NET 10 library. It references neither MonoGame nor native game assets. The grayscale panels, bevels and cursor are drawn procedurally.

Implement `IUiCanvas` to connect a renderer. `PushClip` intersects its rectangle with the current clip; `PopClip` restores the previous clip. Text uses a font size and rectangle in the same coordinate space as fills and lines. Font selection and text measurement belong to the adapter.

Create a root `UiElement` or `UiPanel`, set its `Bounds`, and add children to `Children`. Child bounds are absolute design coordinates. `UiView.Draw(canvas, theme)` renders the tree in child order; hit testing searches children in reverse order. Rectangles include their left/top edges and exclude right/bottom. Ancestor visibility, enabled state and clipping determine whether descendants can receive focus or pointer input.

`UiView.Key(UiKey)` routes keys to focus, then moves focus for unhandled arrows and Tab. Confirm activates buttons, confirms list rows or changes toggles. Cancel invokes `UiView.Cancel`. `Focus(element)` selects a visible enabled control in the tree. Removing or disabling the focused element moves focus to the first eligible control at the next input or draw.

`Pointer(x, y, pressed)` activates on the press transition. Feed release events too. Lists select and confirm the clicked row; buttons activate; toggles change; sliders map the pointer to the nearest step. Programmatic selection/value assignment is silent; input changes invoke callbacks.

Set both `DesignWidth` and `DesignHeight`, then call `Resize(viewportWidth, viewportHeight)` to fit and letterbox the design rectangle. The view maps drawing, clipping and pointer coordinates using the same scale. Without design dimensions, resize arranges the root to the viewport. `UiStack` and `UiGrid` arrange children into equal cells when their `Arrange` method runs; call it again after adding or removing children. Ordinary panels retain manually assigned child bounds.

Controls:

- `UiPanel`: procedural panel with optional `Caption`.
- `UiLabel`: `Text`, `FontSize`, optional `Muted`.
- `UiButton`: `Text`, `Click` callback.
- `UiList`: settable `Items`, `SelectedIndex`, `RowHeight`, `ScrollOffset`, `SelectionChanged` and `Confirmed` callbacks. Up/Down and PageUp/PageDown keep keyboard selection visible. Empty lists have selection -1.
- `UiToggle`: `Text`, boolean `Value`, `ValueChanged`; Left sets Off and Right sets On.
- `UiSlider`: `Text`, finite `Min`, `Max`, positive `Step`, clamped `Value`, `ValueChanged`; Left/Right change by one step. Set Max before Min when raising the range above its default 0..1.
- `UiProgressBar`: `Min`, `Max`, `Value`, optional `Text`; clamps the displayed fraction and renders invalid/empty ranges as zero. Does not receive focus.
- `UiPortrait`: integer `Seed`, string `Initials`; an original grayscale geometric identity placeholder, not a reproduction of game portrait assets.
- `UiStack`: `Orientation`, `Gap`, `Padding`.
- `UiGrid`: positive `Columns`, `Gap`, `Padding`.

`dotnet run --project recreate/product/tests/Memoria.FF8.Ui.Tests` runs headless contract tests.
