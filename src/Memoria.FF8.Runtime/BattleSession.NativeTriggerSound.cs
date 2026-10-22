namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PublishNativeTriggerSound(NativeBattleGraphicsState graphics)
    {
        if (graphics.PendingTriggerSoundActorIndex is not byte owner)
        {
            return graphics;
        }
        NativeBattleGraphicsModel model = graphics.Models[owner];
        byte identity = model.ModelId4 ?? throw new NotSupportedException("Native501640 requires the retained AB actor's loaded model identity.");
        NativeBattleVector position = ReadNativeGraphicsBone(graphics, owner, 241, 0);
        byte pan = (NativeCamera?.Projection ?? throw new NotSupportedException("Native501640 requires its actual5011B0 projection.")).ReadPan(position);
        NativeBattleModuleSound sound = NativeBattleTriggerAudio.Resolve501640(identity, position, pan, definition.NativeGraphics?.Rules.ActorSounds1120);
        PublishNativeModuleSound(sound, graphics.ActorIds[owner]);
        return graphics with { PendingTriggerSoundActorIndex = null };
    }
}
