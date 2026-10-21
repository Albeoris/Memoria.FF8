# Retained UI API

`Memoria.FF8.Ui` is a renderer-independent .NET 10 library. It references neither MonoGame nor native game assets. The grayscale panels, bevels and cursor are drawn procedurally.

Implement `IUiCanvas` to connect a renderer. `PushClip` intersects its rectangle with the current clip; `PopClip` restores the previous clip. Text uses a font size and rectangle in the same coordinate space as fills and lines. Font selection and text measurement belong to the adapter.

Create a root layout container, add children to `Children`, and call `UiView.Resize` to arrange it. Templates declare grid cells, normalized anchors, margins and alignment; they never assign coordinates. `Bounds` has a protected setter and is produced by `Arrange`. `UiView.Draw(canvas, theme)` renders the tree in child order; hit testing searches children in reverse order. Rectangles include their left/top edges and exclude right/bottom. Ancestor visibility, enabled state and clipping determine whether descendants can receive focus or pointer input.

`UiView.Key(UiKey)` routes keys to focus, then moves focus for unhandled arrows and Tab. Confirm activates buttons, confirms list rows or changes toggles. Cancel invokes `UiView.Cancel`. `Focus(element)` selects a visible enabled control in the tree. `UiElement.FocusChanged(bool)` reports real focus transitions, including keyboard navigation, pointer presses and focus recovery. Removing or disabling the focused element moves focus to the first eligible control at the next input or draw.

`Pointer(x, y, pressed)` activates on the press transition. Feed release events too. Lists select and confirm the clicked row; buttons activate; toggles change; sliders map the pointer to the nearest step. Programmatic selection/value assignment is silent; input changes invoke callbacks.

Set both `DesignWidth` and `DesignHeight`, then call `Resize(viewportWidth, viewportHeight)` to fit and letterbox the design rectangle. The view maps drawing, clipping and pointer coordinates using the same scale. Without design dimensions, resize arranges the root to the viewport. Repeated calls with unchanged dimensions reuse layout. Call `InvalidateLayout()` after modifying child collections, sizing, margins, captions or track definitions; the next resize, draw or input arranges the tree. Changing design dimensions invalidates automatically.

`UiGrid.RowDefinitions` and `ColumnDefinitions` accept `UiGridLength.Star(weight)`, `Pixel(size)` and `Auto`. Pixels reserve a dimension, Auto requests intrinsic size, and Star divides remaining space proportionally. `GridRow`, `GridColumn`, `GridRowSpan` and `GridColumnSpan` place children; invalid spans fail immediately. With no definitions, rows or attached placements, `Columns` supplies equal tracks and children flow by row. `Rows` allows explicit equal row tracks. These options use the same track sizing and arrangement path. When committed sizes exceed available space they shrink proportionally; all child rendering remains clipped to ancestors.

`UiAnchorPanel` interprets each child's `UiAnchors(left, top, right, bottom)` as parent-relative edges; the default is the full parent rectangle. Anchors must be finite and ordered. Values outside0..1 support offscreen scripted UI and are clipped by the parent. Both grids and anchors apply `Margin` (`UiThickness`), nullable fixed `Width`/`Height`, and `HorizontalAlignment`/`VerticalAlignment` (`Stretch`, `Start`, `Center`, `End`) to each cell before recursively arranging it. `UiThickness` accepts one value, horizontal/vertical values or four edges; a float implicitly means uniform thickness.

`UiPanel` stretches children into its content rectangle, accounting for `Padding` and optional `CaptionHeight`. `UiElement` stretches children as overlays. `UiStack` divides available space equally between visible children with `Gap` and `Padding`, then applies their margins and alignment. Use a grid when rows need different weights or sizes. Before drawing a resized view, labels measure using the canvas's actual font metrics and the grid's allocated column width. Canvases without text measurement fall back to estimated character advances. Pixel dimensions describe sizes and spacing, never template coordinates.

Controls:

Selection hands are drawn after content in the viewport-clipped adornment pass. They may extend beyond controls and panels, with a four-unit gap before the content. Pointer size does not move labels or change hit targets.

- `UiPanel`: procedural panel with optional `Caption`, `CaptionHeight`, `Padding`, `DrawBackground`. Captions center their visible glyphs on the top border with a black outline; `CaptionFontSize` defaults to the theme size. `UiView` draws frame captions as viewport-clipped adornments after content, so nested panel/grid clips do not cut off their upper halves. Content still respects panel and ancestor clips.
- `UiLabel`: `Text`, `FontSize`, optional `Muted`. `TextFlow=SingleLine` keeps row labels on one line; `TextFlow=Wrap` wraps prose within its width. `Auto` retains height-based flow selection. The live and preview adapters scroll overflowing single rows horizontally or wrapped prose vertically, pausing at both ends. They preserve the requested font size when a complete line fits; smaller viewports proportionally reduce the font to fit one full line. Fitting text remains stationary. Combine related prose into one label to scroll it as one readable block.
- `UiButton`: `Text`, `Click` callback, optional `FontSize` (zero uses the theme) and `PointerSize` (default 10; zero hides the pointer). The hand sits before the leftmost visible child, or before the button's own text when there are no visible children. `DrawBackground=false` removes the panel. `HighlightSelection=false` removes the focused fill while retaining the pointer. Both default to true.
- `UiList`: settable `Items`, `SelectedIndex`, `RowHeight`, `ScrollOffset`, `SelectionChanged` and `Confirmed` callbacks. Up/Down and PageUp/PageDown keep keyboard selection visible. Empty lists have selection -1. `ShowSelection=false` suppresses the highlight and pointer for scrollable text panels; `HighlightSelection=false` suppresses only the fill. `Orientation=UiOrientation.Horizontal` displays all commands in equal-width, full-height cells, navigates with Left/Right, and has no scrolling. Vertical remains the default. `EnabledItems` supplies per-item text availability: false entries are muted but remain selectable and invoke callbacks so the owner can validate the action. Missing entries are enabled. `PointerSize` defaults to 10 and `TextInset` to 23; the hand sits before this inset, leaving row images clear. `ShowScrollbar=false` hides the scroll indicator while preserving scrolling.
- `UiToggle`: `Text`, boolean `Value`, `ValueChanged`; Left sets Off and Right sets On.
- `UiSlider`: `Text`, finite `Min`, `Max`, positive `Step`, clamped `Value`, `ValueChanged`; Left/Right change by one step. Set Max before Min when raising the range above its default 0..1.
- `UiSettingRow`: common base for aligned setting labels and inline values. `LabelWidthRatio` defaults to 0.45; `TextInset` reserves the cursor gutter and `RightInset` reserves the outer edge. `ColumnGap` reserves spacing around the shared label/value boundary. `LabelBounds` and `ValueBounds` are derived from arranged bounds. `FontSize` defaults to the theme and `PointerSize` defaults to 10.
- `UiChoiceRow`: `Text`, `Items`, `SelectedIndex`, `SelectionChanged`; selected options are bright and other options are muted, independently of row focus. `OptionGap` separates visible option cells. Left/Right clamp to adjacent options and Confirm cycles. A pointer press selects a value column; pressing the label only focuses. More than `MaxVisibleItems` choices (default 3), or options whose measured widths cannot fit the available cells, display the current option with chevrons, and presses in the left/right halves step through options. Empty collections have selection -1.
- `UiNumericSlider`: the same range, keyboard and callback contract as `UiSlider`, with a same-row label, beveled purple bar and numeric value. `ValueWidth`, `ValueGap` and `TrackHeight` control the value area; `ValueFormat` uses invariant numeric formatting (default `"0"`). Presses in the bar area select the nearest step; label and number presses only focus. `UiTheme.SliderFill`, `SliderLight` and `SliderDark` supply the bevel colors. Narrow rows reserve the numeric value before allocating the bar.
- `UiProgressBar`: `Min`, `Max`, `Value`, optional `Text`; clamps the displayed fraction and renders invalid/empty ranges as zero. Does not receive focus.
- `UiPortrait`: integer `Seed`, string `Initials`; an original grayscale geometric identity placeholder, not a reproduction of game portrait assets.
- `UiStack`: `Orientation`, `Gap`, `Padding`.
- `UiGrid`: `RowDefinitions`, `ColumnDefinitions`, positive `Columns`, optional `Rows`, `Gap`, `Padding`.
- `UiAnchorPanel`: normalized child `Anchors`, `Padding`.

Example template without absolute coordinates:

```csharp
UiGrid root = new() { Gap = 8, Padding = 12 };
root.ColumnDefinitions.Add(UiGridLength.Star(3));
root.ColumnDefinitions.Add(UiGridLength.Star());
root.RowDefinitions.Add(UiGridLength.Star());
UiPanel party = new() { Caption = "Party", Padding = 8 };
party.Children.Add(new UiList { Items = new List<string> { "Squall", "Quistis" } });
root.Children.Add(party);
root.Children.Add(new UiButton { GridColumn = 1, Text = "Status", Height = 40, VerticalAlignment = UiAlignment.End });
UiView view = new(root);
view.Resize(viewportWidth, viewportHeight);
```

`dotnet run --project tests/Memoria.FF8.Ui.Tests` runs headless contract tests.

Vertical lists expand individual rows when the canvas reports wrapped text taller than the configured row height. `AutoRowHeight=false` preserves fixed row heights. The measured row geometry drives scrolling, pagination, pointer placement and hit testing; the pagination footer remains separate. Measurements are cached until row text, font size, available text width or fitted row height changes. Canvases without text measurement retain fixed rows. `SecondaryAlignment=UiAlignment.End` right-aligns secondary text within its column when measurements are available.

Set `UiList.TextFlow=SingleLine` for command and item-name rows that should scroll horizontally instead of wrapping. Automatic row height then reserves one measured font line and paginates accordingly. When enlarged artwork and pointer gutters would consume more than half a row, the list reduces those gutters and preserves at least forty percent of the row for the primary name, apart from the secondary-column gap.
