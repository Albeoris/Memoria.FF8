using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private PcPolygonModel? limitPreparationMesh;
    private PcTexture[]? limitPreparationTextures;

    private void DrawLimitPreparation(ITriangleRenderer renderer, Ff7BattleLimitPreparationDraw draw, CameraView view)
    {
        limitPreparationMesh ??= PcPolygonModel.Read(Read(MagicArchive(), "jo_p01.p"));
        limitPreparationTextures ??= [PcTexture.Read(Read(MagicArchive(), "jo_b03.tex"))];
        float transverse = draw.TransverseScale / 4096f;
        float vertical = draw.VerticalScale / 4096f;
        float opacity = Ff7BattleSearchScopeEffect.Alpha((short)draw.Transparency) / 255f;
        Matrix4x4 translation = Matrix4x4.CreateTranslation(draw.Position.X, 0, draw.Position.Z);
        int[] flags = [0, 1, 5, 4];
        foreach (int flag in flags)
        {
            float x = (flag & 1) != 0 ? -transverse : transverse;
            float z = (flag & 4) != 0 ? -transverse : transverse;
            Matrix4x4 transform = Matrix4x4.CreateScale(x, vertical, z) * translation;
            DrawModel(renderer, limitPreparationMesh, limitPreparationTextures, transform, view, new() { Cull = true }, opacity);
        }
    }
}
