using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>88AF90's scene-node matrix, explicit +12 vertex pointer and immediate572200 submission.</summary>
public static class NativeBattleCuraScenePackets
{
    public static NativeBattleModulePacketBatch Emit88AF90(NativeBattleCuraSceneResource scene, NativeBattleGfAnimationNode node, NativeBattleActionMatrix parentCamera, NativeBattleCameraProjection projection, uint contextFlags, int? sceneFade, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale.X == 0 && scale.Y == 0 && scale.Z == 0 || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new([], 0);
        }
        if ((contextFlags & 0x30000000) != 0)
        {
            throw new NotSupportedException("Native88AF90 requires the selected56C600 translation or88B200 mutable UV owner.");
        }
        short frame = node.Word40;
        if (node.Word40 != node.Word42 && node.Scalar38 != 0)
        {
            if (node.Scalar38 != 4096)
            {
                throw new NotSupportedException("Native88B320 requires its reached two-frame GTE interpolation owner.");
            }
            frame = node.Word42;
        }
        NativeBattleCuraMeshFrame selected = NativeBattleCuraSceneMesh.Select(scene, unchecked((short)node.GeometryIndex), frame);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.CuraSceneCamera88AF90(parentCamera, node.Vector16, node.Vector8, scale);
        uint flags = (contextFlags & 0xDFFF) | 0x2000;
        int fade = node.Scalar36;
        if ((contextFlags & 0x40000000) != 0)
        {
            short globalFade = unchecked((short)(sceneFade ?? throw new NotSupportedException("Native88AF90 requires the retained2700360 scene-fade low WORD.")));
            fade = unchecked(fade + unchecked(globalFade * (4096 - fade)) / 4096);
        }
        if (fade != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { MeshFlags = flags, MeshFade = fade, PilotIdentity = pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + node.NodeIndex) } : null };
        return NativeBattleModulePackets.EmitMesh572200WithVertices(draw, selected.Resource, projection, matrix, selected.Vertices, 17, 2, node.Color32);
    }
}

public static partial class NativeBattlePoseMatrices
{
    /// <summary>88AF90 composes the unscaled local matrix first, then applies56BEF0 to the resulting camera basis.</summary>
    public static NativeBattleActionMatrix CuraSceneCamera88AF90(NativeBattleActionMatrix parentCamera, NativeBattleVector rotation, NativeBattleVector position, NativeBattleVector scale)
    {
        parentCamera.Validate();
        Matrix local = Rotation(rotation) with { X = position.X, Y = position.Y, Z = position.Z };
        Matrix result = Compose(new(parentCamera.Rotation.ToArray(), parentCamera.X, parentCamera.Y, parentCamera.Z), local);
        if (scale.X != 4096 || scale.Y != 4096 || scale.Z != 4096)
        {
            result = Scale(result, [scale.X, scale.Y, scale.Z]);
        }
        return new(result.Rotation.ToImmutableArray(), result.X, result.Y, result.Z, 4096);
    }
}
