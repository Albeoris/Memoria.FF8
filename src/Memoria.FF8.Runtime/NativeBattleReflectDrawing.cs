using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleReflectDrawing
{
    public static NativeBattleGfGeometryBatch Hexagon(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleReflectResources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int age = NativeBattleReflectController.Word(task, 12);
        short angle = NativeBattleReflectController.Word(task, 16);
        short tilt = NativeBattleReflectController.Word(task, 14);
        NativeBattleVector center = new(NativeBattleReflectController.Word(parent, 84), NativeBattleReflectController.Word(parent, 86), NativeBattleReflectController.Word(parent, 88));
        NativeBattleVector position = NativeBattleCarbuncle278RecipientChildren.Polar6836F0(center, angle, resources.PolarTable);
        NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(view, position);
        int progress = age > 10 ? (age - 10) * 4096 / 6 : 0;
        short scaleX = unchecked((short)(4096 - progress));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix z = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix diagonal = new([scaleX, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix turned = NativeBattleGfMatrices.Multiply56C090(view, NativeBattleGfMatrices.Multiply56C090(z, diagonal));
        short tiltSine = unchecked((short)NativeBattleCameraMath.Sine(tilt));
        short tiltCosine = unchecked((short)NativeBattleCameraMath.Cosine(tilt));
        NativeBattleActionMatrix x = new([4096, 0, 0, 0, tiltCosine, unchecked((short)-tiltSine), 0, tiltSine, tiltCosine], 0, 0, 0, 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(turned, x) with { X = point.X, Y = point.Y, Z = point.Z };
        return NativeBattleModulePackets.EmitBatch572200(resources.Hexagon.Bytes, 0, default, matrix, projection, new(0xF3, 0, 2048 + (progress >> 1), null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity);
    }

    public static NativeBattleGfGeometryBatch Ring(NativeBattleModulePoolTask task, NativeBattleReflectResources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        short height = NativeBattleReflectController.Word(task, 14);
        short scale = NativeBattleReflectController.Word(task, 16);
        NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(view, new(0, 0, height));
        NativeBattleActionMatrix diagonal = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], 0, 0, 0, 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(diagonal, view) with { X = point.X, Y = point.Y, Z = point.Z };
        int fade = NativeBattleCameraMath.Sine((NativeBattleReflectController.Word(task, 12) << 10) / 20);
        return NativeBattleModulePackets.EmitBatch572200(resources.Ring.Bytes, 0, default, matrix, projection, new(0xF3, 0, fade, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity);
    }
}
