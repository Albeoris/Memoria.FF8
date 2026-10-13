using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

internal static class NativeField135ZeroCenterCertificate
{
    // TODO(TD-003): replace this field-specific baseline with the shared native camera/wrapping contract
    private const string ProgramHash = "4E838898400B0B44D1FFAD537A77FDD685111A7C25DF34366CD77CACE8A65344";

    public static bool Supports(FieldAssets field, int cameraIndex)
    {
        if (field.Id != 135 || cameraIndex != 0 || field.Cameras.Count != 1 || field.Camera != field.Cameras[0] || Convert.ToHexString(SHA256.HashData(field.Jsm)) != ProgramHash || field.Information is not FieldInformation information || information.ControlDirection != 128 || information.CameraHeight != 200 || information.BackgroundWrapMask != 2 || information.CameraBounds.Count != 8 || information.ScreenBounds.Count != 2 || information.ScreenBounds.Any(bounds => bounds != new FieldBounds(0, 224, 320, 0)))
        {
            return false;
        }
        FieldCamera camera = field.Camera;
        if (camera.AxisX != new Vector3(3947 / 4096f, 1092 / 4096f, 0) || camera.AxisY != new Vector3(200 / 4096f, -725 / 4096f, -4027 / 4096f) || camera.AxisZ != new Vector3(-1074 / 4096f, 3880 / 4096f, -753 / 4096f) || camera.Translation != new Vector3(1050, -430, 5369) || camera.Pan != Vector2.Zero || camera.FocalLength != 270)
        {
            return false;
        }
        for (int index = 0; index < information.CameraBounds.Count; index++)
        {
            FieldBounds expected = index == 1 ? new FieldBounds(-48, 48, 176, -176) : new FieldBounds(-112, 112, 160, -160);
            if (information.CameraBounds[index] != expected)
            {
                return false;
            }
        }
        if (field.Background.Count != 2131 || field.Background.Count(tile => tile.Layer == 0) != 2021 || field.Background.Count(tile => tile.Layer == 1) != 110)
        {
            return false;
        }
        foreach (BackgroundTile tile in field.Background)
        {
            // 475D00 forces C=(0,0). With PAN0 and unchanged normal records,
            // 475480 tests origins against (-192,176)x(-144,128). Prove
            // both relocation branches and the boundary cull unreachable.
            if (tile.X <= -192 || tile.X >= 176 || tile.Y <= -144 || tile.Y >= 128 || tile.Layer is < 0 or > 1)
            {
                return false;
            }
            if (tile.Layer == 0 && (tile.X is < -160 or > 144 || tile.Y is < -112 or > 96) || tile.Layer == 1 && (tile.X is < -176 or > 160 || tile.Y is < -112 or > -48))
            {
                return false;
            }
        }
        return HasExtent(field, 0, -160, 160, -112, 112) && HasExtent(field, 1, -176, 176, -112, -32);
    }

    private static bool HasExtent(FieldAssets field, int layer, int left, int right, int top, int bottom)
    {
        BackgroundTile[] tiles = field.Background.Where(tile => tile.Layer == layer).ToArray();
        return tiles.Min(tile => tile.X) == left && tiles.Max(tile => tile.X) + 16 == right && tiles.Min(tile => tile.Y) == top && tiles.Max(tile => tile.Y) + 16 == bottom;
    }
}
