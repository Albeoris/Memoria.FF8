using System.Globalization;
using System.Numerics;
using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

public sealed partial class LightingEditorController
{
    private sealed class LightingTransform(FieldLightingScene scene, FieldLight light, LightingTransformTool tool, LightingAxis axis, Vector2 pointer)
    {
        public FieldLightingScene Before { get; } = scene;
        public FieldLight Light { get; } = light;
        public LightingTransformTool Tool { get; } = tool;
        public LightingAxis Axis { get; set; } = axis;
        public Vector2 StartPointer { get; } = pointer;
        public string Numeric { get; set; } = "";
        public bool Dragging { get; set; }
    }

    public void SetTool(LightingTransformTool tool)
    {
        if (SupportsTransform(tool) && CommitPendingEdit())
        {
            if (Selected is not { IsSunlight: true })
            {
                spotTool = tool;
            }
            PickingPivot = false;
        }
    }

    public void BeginTransform(LightingTransformTool tool, LightingAxis axis = LightingAxis.None, bool dragging = false)
    {
        BeginTransform(tool, axis, dragging, pointer);
    }

    private void BeginTransform(LightingTransformTool tool, LightingAxis axis, bool dragging, Vector2 startPointer)
    {
        if (!SupportsTransform(tool))
        {
            Message = "Use Aim to change sunlight direction.";
            return;
        }
        if (!CommitProperty() || Selected is not { } selected)
        {
            return;
        }
        CancelTransform();
        EndSlider(true);
        if (!selected.IsSunlight)
        {
            spotTool = tool;
        }
        interactionPointerOffset = Vector2.Zero;
        transform = new LightingTransform(Draft, selected, tool, axis, startPointer) { Dragging = dragging };
        textUndo.Clear();
        textRedo.Clear();
        Message = tool == LightingTransformTool.Translate ? "Choose an axis or type a signed distance." : "Choose an axis or type signed degrees.";
    }

    private bool SupportsTransform(LightingTransformTool tool)
    {
        return tool == LightingTransformTool.Rotate || Selected is not { IsSunlight: true };
    }

    public void Constrain(LightingAxis axis)
    {
        if (transform is { } active)
        {
            active.Axis = active.Axis == axis ? LightingAxis.None : axis;
            UpdateTransform();
        }
    }

    public bool ConfirmTransform()
    {
        if (transform is null)
        {
            return true;
        }
        if (!UpdateTransform())
        {
            return false;
        }
        History.Commit(Draft);
        transform = null;
        interactionPointerOffset = Vector2.Zero;
        Message = "Transform committed.";
        return true;
    }

    private void FinishPointerTransform()
    {
        if (!ConfirmTransform() && transform is { } active)
        {
            active.Dragging = false;
        }
    }

    public void CancelTransform()
    {
        if (transform is not { } active)
        {
            return;
        }
        transform = null;
        interactionPointerOffset = Vector2.Zero;
        SetDraft(active.Before);
        Message = "Transform cancelled.";
    }

    public void SetTransformNumeric(string numeric)
    {
        if (transform is { } active)
        {
            textUndo.Push(active.Numeric);
            textRedo.Clear();
            active.Numeric = numeric;
            UpdateTransform();
        }
    }

    private bool UpdateTransform()
    {
        if (transform is not { } active)
        {
            return true;
        }
        Vector2 current = pointer - interactionPointerOffset;
        Vector2 delta = current - active.StartPointer;
        bool numeric = active.Numeric.Length != 0;
        float amount = 0;
        if (numeric && (!float.TryParse(active.Numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out amount) || !float.IsFinite(amount)))
        {
            Message = "Enter a finite signed number before confirming or saving.";
            return false;
        }
        FieldLight edited = active.Light;
        if (active.Tool == LightingTransformTool.Translate)
        {
            Vector3 offset;
            if (active.Axis != LightingAxis.None)
            {
                Vector3 axis = LightingEditorGeometry.AxisVector(active.Axis);
                Vector2? start = project(active.Light.Position);
                Vector2? end = project(active.Light.Position + axis * actorScale);
                Vector2 projectedAxis = start is { } first && end is { } second ? (second - first) / actorScale : Vector2.Zero;
                float? startParameter = LightingEditorGeometry.AxisParameter(screenRay(active.StartPointer), active.Light.Position, axis);
                float? endParameter = LightingEditorGeometry.AxisParameter(screenRay(current), active.Light.Position, axis);
                if (!numeric && startParameter is { } initial && endParameter is { } final)
                {
                    amount = final - initial;
                }
                else if (!numeric && projectedAxis.LengthSquared() < 0.000001f)
                {
                    Message = "This axis points into the camera. Type a signed distance for an exact move.";
                    return true;
                }
                else if (!numeric)
                {
                    amount = Vector2.Dot(delta, projectedAxis) / projectedAxis.LengthSquared();
                }
                offset = axis * amount;
            }
            else
            {
                Vector3 normal = screenRay(active.StartPointer)?.Direction ?? Vector3.UnitZ;
                Vector3? start = LightingEditorGeometry.IntersectPlane(screenRay(active.StartPointer), active.Light.Position, normal);
                Vector3? end = LightingEditorGeometry.IntersectPlane(screenRay(current), active.Light.Position, normal);
                offset = start is { } first && end is { } second ? second - first : Vector3.Zero;
                if (numeric)
                {
                    offset = (offset.LengthSquared() > 0.00001f ? Vector3.Normalize(offset) : Vector3.UnitX) * amount;
                }
            }
            edited = edited with { Position = active.Light.Position + offset };
        }
        else
        {
            Vector3 axis = active.Axis == LightingAxis.None && active.Tool == LightingTransformTool.Rotate ? screenRay(active.StartPointer)?.Direction ?? Vector3.UnitZ : LightingEditorGeometry.AxisVector(active.Axis);
            Vector3 center = TransformCenter(active.Light, active.Tool);
            if (!numeric)
            {
                Vector3 normal = Vector3.Normalize(axis);
                Vector3? start = LightingEditorGeometry.IntersectPlane(screenRay(active.StartPointer), center, normal);
                Vector3? end = LightingEditorGeometry.IntersectPlane(screenRay(current), center, normal);
                if (start is { } first && end is { } second && (first - center).LengthSquared() > 0.001f && (second - center).LengthSquared() > 0.001f)
                {
                    Vector3 a = Vector3.Normalize(first - center);
                    Vector3 b = Vector3.Normalize(second - center);
                    amount = MathF.Atan2(Vector3.Dot(normal, Vector3.Cross(a, b)), Vector3.Dot(a, b)) * 180 / MathF.PI;
                }
                else
                {
                    amount = delta.X - delta.Y;
                }
            }
            edited = edited with { Direction = Vector3.Normalize(LightingEditorGeometry.Rotate(active.Light.Direction, axis, amount)) };
            if (active.Tool == LightingTransformTool.Orbit)
            {
                edited = edited with { Position = Pivot + LightingEditorGeometry.Rotate(active.Light.Position - Pivot, axis, amount) };
            }
        }
        try
        {
            FieldLightingScene candidate = ReplaceLight(active.Before, edited);
            FieldLightingSceneIO.Validate(candidate);
            SetDraft(candidate);
            return true;
        }
        catch (InvalidDataException exception)
        {
            Message = exception.Message;
            return false;
        }
    }

    internal IReadOnlyList<LightingGizmoSegment> GizmoSegments()
    {
        List<LightingGizmoSegment> result = [];
        if (Selected is not { } selected)
        {
            return result;
        }
        Vector3 center = TransformCenter(selected, ActiveTool);
        Vector2? projectedCenter = project(center);
        if (projectedCenter is null)
        {
            return result;
        }
        float longest = 1;
        foreach (LightingAxis axis in new[] { LightingAxis.X, LightingAxis.Y, LightingAxis.Z })
        {
            if (project(center + LightingEditorGeometry.AxisVector(axis) * actorScale) is { } projectedEnd)
            {
                longest = Math.Max(longest, Vector2.Distance(projectedCenter.Value, projectedEnd));
            }
        }
        float length = actorScale * 72 / longest;
        foreach (LightingAxis axis in new[] { LightingAxis.X, LightingAxis.Y, LightingAxis.Z })
        {
            Vector3 direction = LightingEditorGeometry.AxisVector(axis);
            if (ActiveTool == LightingTransformTool.Translate)
            {
                if (project(center + direction * length) is { } end)
                {
                    result.Add(new(projectedCenter.Value, end, axis, ActiveTool));
                }
            }
            else
            {
                foreach ((Vector3 start, Vector3 end) in LightingEditorGeometry.Ring(center, direction, length))
                {
                    if (project(start) is { } first && project(end) is { } second)
                    {
                        result.Add(new(first, second, axis, ActiveTool));
                    }
                }
            }
        }
        return result;
    }

    private Vector3 TransformCenter(FieldLight light, LightingTransformTool tool)
    {
        return light.IsSunlight || tool == LightingTransformTool.Orbit ? Pivot : light.Position;
    }

    private LightingGizmoSegment? HitGizmo(Vector2 position)
    {
        LightingGizmoSegment? nearest = null;
        float distance = 9;
        foreach (LightingGizmoSegment segment in GizmoSegments())
        {
            float candidate = LightingEditorGeometry.DistanceToSegment(position, segment.Start, segment.End);
            if (candidate < distance)
            {
                distance = candidate;
                nearest = segment;
            }
        }
        return nearest;
    }
}
