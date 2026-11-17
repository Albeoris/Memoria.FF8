using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class LightingEditorController
{
    private static readonly UiRect ReceiverToolbar = new(270, 118, 675, 32);
    private static readonly UiRect ReceiverInspector = new(270, 154, 675, 42);
    private static readonly UiRect ReceiverOverlayButton = new(270, 118, 128, 32);
    private static readonly UiRect AddFloorButton = new(402, 118, 84, 32);
    private static readonly UiRect AddWallButton = new(490, 118, 84, 32);
    private static readonly UiRect PreviousReceiverButton = new(578, 118, 28, 32);
    private static readonly UiRect NextReceiverButton = new(716, 118, 28, 32);
    private static readonly UiRect ReceiverEnabledButton = new(750, 118, 90, 32);
    private static readonly UiRect DeleteReceiverButton = new(844, 118, 101, 32);
    private static readonly UiColor WalkmeshReceiverColor = new(83, 204, 249, 155);
    private static readonly UiColor AuthoredReceiverColor = new(255, 171, 92);
    private static readonly UiColor SelectedReceiverColor = new(255, 234, 139);
    private static readonly UiColor DisabledReceiverColor = new(160, 160, 160);
    private Func<IReadOnlyList<WalkmeshTriangle>> nativeReceivers = () => [];
    private bool receiverOverlay;
    private string? selectedReceiverId;
    private string? receiverPlacementKind;
    private Vector3? receiverAnchor;
    private FieldShadowReceiver? SelectedReceiver => Draft.ShadowReceivers.FirstOrDefault(receiver => receiver.Id == selectedReceiverId);
    private bool ReceiverInspectorVisible => receiverOverlay || SelectedReceiver is not null;

    internal void SetReceiverDiagnostics(Func<IReadOnlyList<WalkmeshTriangle>> nativeReceivers)
    {
        this.nativeReceivers = nativeReceivers;
    }

    internal bool ReceiverControlsContain(Vector2 point)
    {
        return ReceiverToolbar.Contains(point.X, point.Y) || ReceiverInspectorVisible && ReceiverInspector.Contains(point.X, point.Y);
    }

    private bool HandleReceiverInput(Vector2 position, bool leftPressed)
    {
        if (!leftPressed)
        {
            return false;
        }
        if (!ReceiverControlsContain(position))
        {
            if (receiverPlacementKind is not null && IsViewportPointer(position))
            {
                PlaceReceiverPoint(position);
                return true;
            }
            return false;
        }
        bool Hit(UiRect bounds)
        {
            return bounds.Contains(position.X, position.Y);
        }
        if (Hit(ReceiverOverlayButton))
        {
            CancelReceiverPlacement();
            receiverOverlay = !receiverOverlay;
        }
        else if (Hit(AddFloorButton))
        {
            BeginReceiverPlacement("floor");
        }
        else if (Hit(AddWallButton))
        {
            BeginReceiverPlacement("wall");
        }
        else if (Hit(PreviousReceiverButton) || Hit(NextReceiverButton))
        {
            CycleReceiver(Hit(PreviousReceiverButton) ? -1 : 1);
        }
        else if (SelectedReceiver is { } selected)
        {
            if (Hit(ReceiverEnabledButton))
            {
                ApplyCommand(ReplaceReceiver(Draft, selected with { Enabled = !selected.Enabled }));
            }
            else if (Hit(DeleteReceiverButton))
            {
                selectedReceiverId = null;
                ApplyCommand(Draft with { ShadowReceivers = Draft.ShadowReceivers.Where(receiver => receiver.Id != selected.Id).ToArray() });
            }
            else
            {
                for (LightingProperty property = LightingProperty.ReceiverX; property <= LightingProperty.ReceiverHeading; property++)
                {
                    if (Hit(ReceiverNumericBounds(property)))
                    {
                        BeginPropertyEdit(property);
                        break;
                    }
                }
            }
        }
        return true;
    }

    private void BeginReceiverPlacement(string kind)
    {
        if (Draft.ShadowReceivers.Count >= FieldLightingSceneIO.MaximumShadowReceivers)
        {
            Message = "This scene has reached the shadow receiver limit.";
            return;
        }
        CancelReceiverPlacement();
        PickingPivot = false;
        receiverPlacementKind = kind;
        receiverOverlay = true;
        Message = kind == "wall" ? "Click the first endpoint of the wall base." : "Click the first corner of the floor. The ground there sets the height.";
    }

    private void CancelReceiverPlacement()
    {
        if (receiverPlacementKind is not null)
        {
            Message = "Receiver placement canceled.";
        }
        receiverPlacementKind = null;
        receiverAnchor = null;
    }

    private void CycleReceiver(int direction)
    {
        CancelReceiverPlacement();
        int count = Draft.ShadowReceivers.Count;
        if (count == 0)
        {
            return;
        }
        int index = Draft.ShadowReceivers.ToList().FindIndex(receiver => receiver.Id == selectedReceiverId);
        index = index < 0 ? direction > 0 ? 0 : count - 1 : (index + count + direction) % count;
        selectedReceiverId = Draft.ShadowReceivers[index].Id;
        receiverOverlay = true;
    }

    private void PlaceReceiverPoint(Vector2 screen)
    {
        if (receiverPlacementKind is not { } kind)
        {
            return;
        }
        if (receiverAnchor is not { } first)
        {
            receiverAnchor = pickGround(screen) ?? ReceiverPointAtHeight(screen, Pivot.Z);
            Message = receiverAnchor is null ? "This ray misses the placement plane. Choose another view or scene pivot." : kind == "wall" ? "Click the other end of the wall base. Edit height in the receiver row." : "Click the opposite corner of the floor.";
            return;
        }
        if (ReceiverPointAtHeight(screen, first.Z) is not { } second || BuildReceiver(kind, first, second) is not { } preview)
        {
            Message = "Choose another point to give the receiver positive width and height or depth.";
            return;
        }
        FieldShadowReceiver receiver = preview with { Id = Guid.NewGuid().ToString("N") };
        try
        {
            ApplyCommand(Draft with { ShadowReceivers = [.. Draft.ShadowReceivers, receiver] });
        }
        catch (InvalidDataException exception)
        {
            Message = exception.Message;
            return;
        }
        selectedReceiverId = receiver.Id;
        receiverPlacementKind = null;
        receiverAnchor = null;
        Message = kind == "wall" ? "Wall receiver added. Set its height, heading or center in the receiver row." : "Floor receiver added. Adjust its dimensions, heading or center in the receiver row.";
    }

    private Vector3? ReceiverPointAtHeight(Vector2 screen, float height)
    {
        if (screenRay(screen) is not { } ray || MathF.Abs(ray.Direction.Z) < 0.00001f)
        {
            return null;
        }
        float distance = (height - ray.Origin.Z) / ray.Direction.Z;
        if (!float.IsFinite(distance) || distance <= 0)
        {
            return null;
        }
        Vector3 point = ray.Origin + ray.Direction * distance;
        return float.IsFinite(point.X) && float.IsFinite(point.Y) ? new Vector3(point.X, point.Y, height) : null;
    }

    private FieldShadowReceiver? BuildReceiver(string kind, Vector3 first, Vector3 second)
    {
        Vector3 difference = second - first;
        float width = kind == "wall" ? difference.Length() : MathF.Abs(difference.X);
        float extent = kind == "wall" ? actorScale * 1.5f : MathF.Abs(difference.Y);
        if (!float.IsFinite(width) || !float.IsFinite(extent) || width < 0.01f || extent < 0.01f)
        {
            return null;
        }
        Vector3 center = first + difference * 0.5f + (kind == "wall" ? Vector3.UnitZ * (extent * 0.5f) : Vector3.Zero);
        return new FieldShadowReceiver
        {
            Id = "",
            Kind = kind,
            Center = center,
            Width = width,
            Extent = extent,
            HeadingDegrees = kind == "wall" ? MathF.Atan2(difference.Y, difference.X) * (180 / MathF.PI) : 0
        };
    }

    private static FieldLightingScene ReplaceReceiver(FieldLightingScene scene, FieldShadowReceiver receiver)
    {
        return scene with { ShadowReceivers = scene.ShadowReceivers.Select(existing => existing.Id == receiver.Id ? receiver : existing).ToArray() };
    }

    private void DrawReceiverTools(IUiCanvas canvas, UiTheme theme)
    {
        Panel(canvas, ReceiverToolbar);
        Button(canvas, theme, ReceiverOverlayButton, "Receivers overlay", receiverOverlay);
        Button(canvas, theme, AddFloorButton, "Add floor", receiverPlacementKind == "floor");
        Button(canvas, theme, AddWallButton, "Add wall", receiverPlacementKind == "wall");
        bool hasReceivers = Draft.ShadowReceivers.Count > 0;
        Button(canvas, theme, PreviousReceiverButton, "<", false, hasReceivers);
        Button(canvas, theme, NextReceiverButton, ">", false, hasReceivers);
        FieldShadowReceiver? selected = SelectedReceiver;
        int selectedIndex = selected is null ? -1 : Draft.ShadowReceivers.ToList().IndexOf(selected);
        string caption = selected is null ? $"{Draft.ShadowReceivers.Count} patches" : $"{(selected.Kind == "wall" ? "Wall" : "Floor")} {selectedIndex + 1}/{Draft.ShadowReceivers.Count}";
        Label(canvas, caption, 13, new(614, 125, 100, 24), theme.Foreground);
        Button(canvas, theme, ReceiverEnabledButton, selected?.Enabled == true ? "Enabled" : "Disabled", selected?.Enabled == true, selected is not null);
        Button(canvas, theme, DeleteReceiverButton, "Remove patch", false, selected is not null);
        if (!ReceiverInspectorVisible)
        {
            return;
        }
        Panel(canvas, ReceiverInspector);
        if (selected is null)
        {
            Label(canvas, "Active walkmesh", 13, new(280, 166, 126, 20), WalkmeshReceiverColor);
            Label(canvas, "Authored patch", 13, new(412, 166, 126, 20), AuthoredReceiverColor);
            Label(canvas, "Disabled patch", 13, new(544, 166, 126, 20), DisabledReceiverColor);
            return;
        }
        string[] labels = ["Center X", "Center Y", "Center Z", "Width", selected.Kind == "wall" ? "Height" : "Depth", "Heading °"];
        for (LightingProperty property = LightingProperty.ReceiverX; property <= LightingProperty.ReceiverHeading; property++)
        {
            UiRect bounds = ReceiverNumericBounds(property);
            bool editing = EditingProperty == property;
            Label(canvas, labels[property - LightingProperty.ReceiverX], 12, new(bounds.X, bounds.Y - 16, bounds.Width, 17), theme.Muted);
            canvas.Fill(bounds, editing ? new UiColor(35, 66, 87) : InputColor);
            Label(canvas, PropertyText(property) + (editing ? "|" : ""), 13, bounds.Inset(3), editing && TextSelected ? ErrorColor : theme.Foreground);
        }
    }

    private static UiRect ReceiverNumericBounds(LightingProperty property)
    {
        return new UiRect(280 + (property - LightingProperty.ReceiverX) * 110, 173, 100, 20);
    }

    private void DrawReceiverGuides(IUiCanvas canvas)
    {
        if (!receiverOverlay)
        {
            return;
        }
        foreach (WalkmeshTriangle triangle in nativeReceivers())
        {
            DrawWorldLine(canvas, triangle.A, triangle.B, WalkmeshReceiverColor, 1);
            DrawWorldLine(canvas, triangle.B, triangle.C, WalkmeshReceiverColor, 1);
            DrawWorldLine(canvas, triangle.C, triangle.A, WalkmeshReceiverColor, 1);
        }
        foreach (FieldShadowReceiver receiver in Draft.ShadowReceivers)
        {
            bool selected = receiver.Id == selectedReceiverId;
            UiColor color = selected ? SelectedReceiverColor : receiver.Enabled ? AuthoredReceiverColor : DisabledReceiverColor;
            DrawReceiverGuide(canvas, receiver, color, selected ? 3 : 2);
        }
        if (receiverPlacementKind is { } kind && receiverAnchor is { } first && IsViewportPointer(pointer) && ReceiverPointAtHeight(pointer, first.Z) is { } second)
        {
            if (BuildReceiver(kind, first, second) is { } preview)
            {
                DrawReceiverGuide(canvas, preview, SelectedReceiverColor, 2);
            }
            else
            {
                DrawWorldLine(canvas, first, second, SelectedReceiverColor, 2);
            }
        }
    }

    private void DrawReceiverGuide(IUiCanvas canvas, FieldShadowReceiver receiver, UiColor color, float width)
    {
        Vector3[] corners = receiver.Corners();
        for (int index = 0; index < corners.Length; index++)
        {
            DrawWorldLine(canvas, corners[index], corners[(index + 1) % corners.Length], color, width);
        }
    }
}
