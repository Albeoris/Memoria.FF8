# Retained UI framework

`Memoria.FF8.Ui` is a renderer-independent .NET 10 library for panels, lists, buttons, labels, settings, images and progress displays. It has no MonoGame or installed-game dependency. The application connects it through an `IUiCanvas` adapter and builds live menus in `GameUi/MenuController`.

Templates declare layout. `Bounds` is the output of layout and has a protected setter; templates do not assign element positions. Pixel track sizes and preferred width/height describe dimensions. Use weighted grids, spans, normalized anchors, margins and alignment to place content.

The detailed element contract and control properties are in the [UI library README](../../src/Memoria.FF8.Ui/README.md). The [Debug Room guide](../players/debug-room.md) describes previewing screens, and the [control API](../reference/control-api.md) describes automation.

## Layout and drawing

`IUiCanvas.AlignedText` places text at the start, center or end of a rectangle using the active canvas's glyph measurements. Center/end alignment requires `MeasureText`; text and measurement use the same font size and available width.

Create a root container, add its children, construct `UiView` and call `Resize`. Set both `DesignWidth` and `DesignHeight` to fit and letterbox a design rectangle; drawing, clipping and pointer coordinates use the same mapping. Without a design size, layout fills the viewport. `InvalidateLayout()` requests arrangement after changes to children, sizing or track definitions. Repeated resize calls with unchanged dimensions reuse layout.

`UiGrid` supports `Auto`, `Pixel(size)` and `Star(weight)` tracks. Assign `GridRow`, `GridColumn`, row/column spans and alignment on children. With no explicit track definitions or placements, `Columns` supplies equal tracks and children flow by row. Use a grid for unequal rows. `UiAnchorPanel` supplies parent-relative edges through `UiAnchors`. `UiPanel` fills its content rectangle, and `UiStack` divides available space equally among visible children.

Draw with `UiView.Draw(canvas, theme)`. `IUiCanvas.PushClip` intersects the current clip and `PopClip` restores it. All rectangles and text use one coordinate space. Font selection and actual text measurement belong to the adapter. Children draw in order and hit-test in reverse order. Ancestor visibility, enabled state and clipping also constrain input.

The application and raster preview adapters automatically scroll ordinary text when wrapping exceeds the rectangle's fixed height. A single paragraph becomes a horizontally scrolling line; explicit line breaks remain intact and an overflowing multiline block scrolls vertically. Text that fits keeps its normal wrapping and alignment. `UiTextOverflow` measures through the adapter and retains animation across rebuilt views. Call the adapter's `BeginFrame(TimeSpan)` once per presented frame: the text pauses for 1.5 seconds at the beginning, moves at 1.5 font sizes per second, pauses for 2 seconds at the end, then repeats. Changing text, font, bounds or visibility restarts the cycle. The original rectangle and ancestor clips remain in effect, including for HELP panels.

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

## Focus and input

`UiView.Key(UiKey)` routes input to the focused control, then handles unconsumed arrows and Tab as focus navigation. Confirm activates a button, confirms a list row or changes a toggle; Cancel invokes the view callback. `Focus(element)` accepts a visible, enabled control in the tree. Focus transitions notify `FocusChanged`; removing or disabling the focused element recovers focus on the next draw or input.

`Pointer(x, y, pressed)` activates on a press edge. Feed release events too. Lists select and confirm the clicked row, buttons activate, toggles change and sliders select the nearest step. Setting a value or selection programmatically is silent; user input invokes callbacks. Keep physical pointer coordinates at the view boundary so letterbox conversion is shared with drawing.

The application supports keyboard, controller and mouse menus. Gameplay bindings are configurable, so use the player's current Controls screen for assignments. The UI host drains closing input before returning to gameplay and pauses/resumes movie output with menu ownership.

## Registering a menu

Register a `MenuScreenDefinition` with a stable ID, title/help, optional entries/details callbacks, layout and optional `BuildContent`. `MainCommand = true` adds it to the main menu. Registered screens are available to the Menu gallery. `BuildContent` receives a panel filling its allocated parent; add a layout container to it.

```csharp
controller.Register(new MenuScreenDefinition
{
    Id = "mod-options",
    Title = "Mod options",
    Help = "Configure the mod.",
    MainCommand = true,
    BuildContent = (_, root) =>
    {
        UiGrid grid = new() { Columns = 1, Gap = 12 };
        grid.RowDefinitions.Add(UiGridLength.Auto);
        grid.RowDefinitions.Add(UiGridLength.Star());
        grid.Children.Add(new UiLabel { Text = "Mod options", FontSize = 24 });
        grid.Children.Add(new UiButton { GridRow = 1, Text = "Apply", Click = ApplyModOptions });
        root.Children.Add(grid);
    }
});
```

The host provides live `MenuData` and an action handler to `MenuController`. Live data is cached while the menu is open and refreshed after supported host actions. `MenuData` carries character/GF/item/magic/card/recipe/save collections, settings, optional currency/time/rank, availability information and `SupportedActions`. Unknown numeric values remain nullable; empty banks do not receive demonstration entries. `MenuAction` carries `Kind`, `SubjectId`, `TargetId` and `Value`. Only an exact `Kind` or `Kind:SubjectId` key in `SupportedActions` authorizes backend dispatch. Unavailable operations must explain their boundary and leave game state intact. Demonstration screens use isolated sample data; they do not establish support for a live game operation. A live handler must submit a transaction to its existing engine owner and preserve capture/restore behavior.

Host preferences such as UI scale and audio volume are separate from game-owned state. Save/Load delegates to the existing [player save flow](../players/saves.md); menus must not create an alternative persistence or replay mechanism.

## Verification

Run `dotnet run --project tests/Memoria.FF8.Ui.Tests` from the product root for headless layout, clipping, focus and control checks. App-level tests cover live menu behavior, input routing and host integration. Render checks should include a small viewport and pointer/focus navigation. A demonstration preview proves layout only; use a current standalone save for a live-operation check. See [building and testing](building-and-testing.md) for the supported test entry points.


## Swappable presentation

`MenuController` owns data, actions, selected identities and navigation history. `IMenuPresentation` owns `Compose`, design dimensions, root padding and `CreateTheme`; assign `MenuController.PresentationProvider` to supply a presentation, or use `Presentation` to select the built-in Vector and Classic providers. Replacing the provider invalidates only the retained view. It does not open a screen, execute an action or rewrite a gameplay snapshot.

Vector is the installation default, including when loading preferences written before presentation selection existed. Classic preserves the earlier layout and gray bevels. F7 and Config → Interface → UI presentation persist the choice independently of saved games. The main menu and Junction attribute/stock pages use Vector's 960×540 composition; command/ability assignment and other menu pages retain their existing 960×672 composition (magazines use 960×720) under the selected theme. The viewport uniformly scales and letterboxes each composition, including pointer mapping.

The Vector design uses these procedural elements:

| Element | Drawing and composition |
| --- | --- |
| Window edge | Thin silver top/left edges, softer bottom/right edges. |
| Panel material | Vertical navy gradient with alpha 182–202, multiplied by requested panel opacity. The live scene remains behind the menu. |
| Selected row | Blue gradient, narrow red leading strip and fine upper highlight. |
| Tabs | Red underline; Junction keeps the active underline while selecting an attribute or spell. |
| Separators | Low-alpha horizontal rules in tables, command lists and readouts; brighter header/footer rules. |
| HP | Clamped current/max ratio, dark track, cyan gradient, bright upper edge and endpoint. Missing values produce an empty track. |
| Typography | Georgia by default, pale foreground and blue secondary labels. A selected nondefault UI font overrides it. `UiFontCanvas` measures and draws with matching fonts. |
| Header | Small spaced MENU label, large title, subtitle, geometric crest and vertical separator. |
| Main menu | Three active-party cards, reserve grid, full-height right command column with play time/rank/currency, location and input hints below. Reserve space expands for additional characters. |
| Junction | Character summary at left, nine-row stat/magic/value table at center, command pane at right. Selection pages reuse existing controller entries/actions. |
| Ornament | Low-opacity geometric feather strokes on tall panels. |
| Input hints | Current keyboard/controller bindings and fixed F7 shortcut. |

`UiTheme` provides virtual `Panel`, `Caption`, `Pointer`, `Selection`, `RowSeparator`, `NumericSlider` and `Progress` hooks. Its default implementation remains Classic; `VectorUiTheme` overrides the material and selection grammar. Optional `FontFamily` preserves explicit per-element font overrides. Renderer adapters implement family-aware measurement and alignment/flow text overloads; the scaled canvas forwards these in design coordinates. Original artwork remains supplied by the existing resource provider. The presentation contains no background illustrations or replacement portraits.

Preview either presentation with `--menu-preview --ui vector` or `--ui classic`; pass `--resolution` to compare identical output sizes. A preview does not advance gameplay.
