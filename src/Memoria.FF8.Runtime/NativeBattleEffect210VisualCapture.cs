using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect210VisualCapture
{
    internal static Vector3 World(NativeBattleGeometryRegisters registers, NativeBattleVector vertex, NativeBattleCameraProjection projection)
    {
        NativeBattleActionMatrix matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(registers).ReadMatrix();
        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, vertex), projection);
    }

    internal static NativeBattleGeometryPrimitive Screen(NativeBattleGeometryPrimitive primitive, NativeBattlePilotVisualIdentity identity, float scale, bool continuous)
    {
        bool quad = (primitive.Words[1] & 0x08000000) != 0;
        int stride = (primitive.Words[1] & 0x14000000) == 0 ? 1 : 2;
        Vector3 Point(int corner)
        {
            uint packed = primitive.Words[2 + corner * stride];
            return new(unchecked((short)packed), unchecked((short)(packed >> 16)), 0);
        }
        Vector4 depths = primitive.VertexPayload4.Length == 4 ? new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) : Vector4.Zero;
        return primitive with { PilotGeometry = new(identity, Point(0), Point(1), Point(2), quad ? Point(3) : default, (byte)(quad ? 4 : 3), continuous) { ScreenSpace = true, ScreenScale = scale, SourceDepths = depths, UseSourceDepths = true } };
    }
}
