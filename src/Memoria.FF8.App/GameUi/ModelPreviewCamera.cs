using System.Numerics;

namespace Memoria.FF8.GameUi;

public enum ModelPreviewView
{
    Front,
    Side,
    Top,
    Reset
}

/// <summary>Orthographic inspection camera with unrestricted screen-space orbit, roll and pan.</summary>
public sealed class ModelPreviewCamera
{
    public Quaternion Orientation { get; private set; } = Quaternion.Identity;
    public Vector2 Pan { get; private set; }
    public float Zoom { get; private set; } = 1;
    public float Pitch { get; private set; }
    public float Yaw { get; private set; }

    public void Rotate(float horizontal, float vertical, float roll = 0)
    {
        if (!float.IsFinite(horizontal) || !float.IsFinite(vertical) || !float.IsFinite(roll))
        {
            return;
        }
        Yaw = MathF.IEEERemainder(Yaw + horizontal, MathF.Tau);
        Pitch = MathF.IEEERemainder(Pitch + vertical, MathF.Tau);
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(Orientation) * Matrix4x4.CreateRotationY(horizontal) * Matrix4x4.CreateRotationX(vertical) * Matrix4x4.CreateRotationZ(roll);
        Orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation));
    }

    public void PanBy(float horizontal, float vertical)
    {
        if (float.IsFinite(horizontal) && float.IsFinite(vertical))
        {
            Pan += new Vector2(horizontal, vertical);
        }
    }

    public void ZoomBy(float factor)
    {
        if (float.IsFinite(factor) && factor > 0)
        {
            Zoom = Math.Clamp(Zoom * factor, 0.1f, 10f);
        }
    }

    public void SetView(float yaw = 0, float pitch = 0)
    {
        Orientation = Quaternion.Identity;
        Pitch = 0;
        Yaw = 0;
        Rotate(yaw, pitch);
    }

    public void Reset()
    {
        SetView();
        Pan = Vector2.Zero;
        Zoom = 1;
    }
}
