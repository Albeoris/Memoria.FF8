namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeModuleEffects(NativeBattleGraphicsState graphics)
    {
        NativeBattleModuleStep step = graphics.ModuleEffects.Advance(new(graphics.Flags) { Resources = definition.NativeModuleResources, CrtRandom = nativeCrtRandom, ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index), ReadModelBone = (index, bone, fraction) => ReadNativeGraphicsBone(graphics, index, bone, fraction), ReadModelRotation14 = index => graphics.Models[index].Rotation14 ?? throw new NotSupportedException("Native module effect requires the loaded model rotationWORD14.") });
        nativeCrtRandom = step.CrtRandomAfter;
        foreach (NativeBattleModuleDraw draw in step.Draws)
        {
            if (draw.Kind == NativeBattleModuleDrawKind.Sound)
            {
                NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native module sound lacks its original spatial anchor.");
                PublishNativeModuleSound(draw.Sound, draw.SoundFlags, anchor);
            }
        }
        return graphics with { ModuleEffects = step.State, ModuleDraws = step.Draws };
    }

    private void PublishNativeModuleSound(uint sound, ushort flags, NativeBattleModuleAnchor anchor, string? actorId = null)
    {
        //501160 clears the3D-mode switch;5017A0 then uses5011B0's current camera projection before46B2A0.
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5017A0 sound requires its actual5011B0 camera projection.");
        byte pan = projection.ReadPan(new(anchor.X, anchor.Y, anchor.Z));
        events.Add(new BattleEvent(Frame, "NativeModuleSound", actorId, null, 0, string.Empty) { NativeModuleSound = new(sound, flags, anchor) { Pan = pan } });
    }
}
