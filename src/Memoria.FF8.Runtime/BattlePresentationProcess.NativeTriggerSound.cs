namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState PublishNativeTriggerSound(NativeBattleGraphicsState graphics)
    {
        if (graphics.PendingTriggerSoundActor is not { } owner)
        {
            return graphics;
        }
        byte identity = graphics.ActorModels.Get(owner).ModelId4 ?? throw new NotSupportedException("Native501640 requires the retained AB actor's loaded model identity.");
        if (identity < 16)
        {
            identity = ReadOriginalPresentationModelResource(owner)?.CombatStyleModelId ?? identity;
        }
        NativeBattleVector position = ReadNativeGraphicsBone(graphics, owner, 241, 0);
        byte pan = (NativeCamera?.Projection ?? throw new NotSupportedException("Native501640 requires its actual5011B0 projection.")).ReadPan(position);
        NativeBattleModuleSound sound = NativeBattleTriggerAudio.Resolve501640(identity, position, pan, this.resources.NativeGraphics?.Rules.ActorSounds1120);
        PublishNativeModuleSound(sound, RequireCanonicalGraphicsActorId(owner));
        return graphics with { PendingTriggerSoundActor = null };
    }
}
