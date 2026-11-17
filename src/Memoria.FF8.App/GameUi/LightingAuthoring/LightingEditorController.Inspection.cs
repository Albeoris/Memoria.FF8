using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public sealed partial class LightingEditorController
{
    private static readonly UiRect InspectionGray = new(270, 198, 138, 28);
    private static readonly UiRect InspectionWrap = new(416, 198, 138, 28);
    private static readonly UiRect InspectionOrbit = new(562, 198, 146, 28);
    private static readonly UiRect InspectionBackdrop = new(716, 198, 137, 28);
    private static readonly UiRect InspectionControls = new(270, 198, 677, 28);
    private Vector2 inspectionAngles;
    private bool orbitingInspection;
    private IReadOnlyList<WalkmeshTriangle> inspectionWalkmesh = [];

    internal bool PreviewGrayMaterial { get; private set; }
    internal bool PreviewWrapLighting { get; private set; }
    internal bool InspectionView { get; private set; }
    internal bool PreviewInspectionBackdrop { get; private set; }

    internal void SetInspectionWalkmesh(IReadOnlyList<WalkmeshTriangle> triangles)
    {
        inspectionWalkmesh = triangles;
    }

    internal FieldCamera? InspectionCamera(FieldCamera native)
    {
        if (!InspectionView)
        {
            return null;
        }
        Matrix4x4 yaw = Matrix4x4.CreateRotationZ(inspectionAngles.X);
        Vector3 axisX = Vector3.TransformNormal(native.AxisX, yaw);
        Matrix4x4 pitch = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axisX), inspectionAngles.Y);
        Vector3 axisY = Vector3.TransformNormal(Vector3.TransformNormal(native.AxisY, yaw), pitch);
        Vector3 axisZ = Vector3.TransformNormal(Vector3.TransformNormal(native.AxisZ, yaw), pitch);
        Vector3 translation = native.Translation + new Vector3(Vector3.Dot(native.AxisX - axisX, Pivot), Vector3.Dot(native.AxisY - axisY, Pivot), Vector3.Dot(native.AxisZ - axisZ, Pivot));
        return native with { AxisX = axisX, AxisY = axisY, AxisZ = axisZ, Translation = translation };
    }

    private static bool InspectionControlsContain(Vector2 point)
    {
        return InspectionControls.Contains(point.X, point.Y);
    }

    private bool TryPressInspectionTool(Vector2 position)
    {
        if (InspectionGray.Contains(position.X, position.Y))
        {
            PreviewGrayMaterial = !PreviewGrayMaterial;
            return true;
        }
        if (InspectionWrap.Contains(position.X, position.Y))
        {
            PreviewWrapLighting = !PreviewWrapLighting;
            return true;
        }
        if (InspectionOrbit.Contains(position.X, position.Y))
        {
            InspectionView = !InspectionView;
            inspectionAngles = Vector2.Zero;
            orbitingInspection = false;
            return true;
        }
        if (InspectionView && InspectionBackdrop.Contains(position.X, position.Y))
        {
            PreviewInspectionBackdrop = !PreviewInspectionBackdrop;
            ClampInspectionAngles();
            return true;
        }
        return false;
    }

    private void ClampInspectionAngles()
    {
        if (PreviewInspectionBackdrop)
        {
            inspectionAngles.X = Math.Clamp(inspectionAngles.X, -0.32f, 0.32f);
            inspectionAngles.Y = Math.Clamp(inspectionAngles.Y, -0.22f, 0.22f);
        }
    }

    private bool HandleInspectionNavigation(KeyboardState keys, MouseState mouse, MouseState oldMouse, Vector2 position, Vector2 previousPosition)
    {
        if (mouse.MiddleButton != ButtonState.Pressed || !InspectionView)
        {
            orbitingInspection = false;
            return false;
        }
        if (oldMouse.MiddleButton == ButtonState.Released)
        {
            orbitingInspection = IsViewportPointer(position) && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
            return orbitingInspection;
        }
        if (!orbitingInspection)
        {
            return false;
        }
        Vector2 delta = (position - previousPosition) * 0.007f;
        inspectionAngles.X = MathF.IEEERemainder(inspectionAngles.X - delta.X, MathF.Tau);
        inspectionAngles.Y = Math.Clamp(inspectionAngles.Y + delta.Y, -1.15f, 1.15f);
        ClampInspectionAngles();
        return true;
    }

    private void DrawInspectionTools(IUiCanvas canvas, UiTheme theme)
    {
        Button(canvas, theme, InspectionGray, "Gray material", PreviewGrayMaterial);
        Button(canvas, theme, InspectionWrap, "Wrap preview", PreviewWrapLighting);
        Button(canvas, theme, InspectionOrbit, InspectionView ? "Game camera" : "3D inspection", InspectionView);
        if (InspectionView)
        {
            Button(canvas, theme, InspectionBackdrop, "Backdrop preview", PreviewInspectionBackdrop);
            Label(canvas, "Alt + MMB", 14, new(861, 198, 88, 28), theme.Foreground);
        }
    }

    private void DrawInspectionWalkmesh(IUiCanvas canvas)
    {
        if (!InspectionView)
        {
            return;
        }
        UiColor color = new(100, 160, 181, 155);
        foreach (WalkmeshTriangle triangle in inspectionWalkmesh)
        {
            DrawWorldLine(canvas, triangle.A, triangle.B, color, 1);
            DrawWorldLine(canvas, triangle.B, triangle.C, color, 1);
            DrawWorldLine(canvas, triangle.C, triangle.A, color, 1);
        }
    }
}
