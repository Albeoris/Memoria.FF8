namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeGlobalSoundProcess? NativeGlobalSoundProcess { get; private set; }

    private void PublishNativeModuleSound(NativeBattleModuleSound sound, string? actorId = null, NativeBattleSequenceSound? sequence = null)
    {
        //46B2A0 writes the global original-ID slot before4699A0 can reject output.
        //The declared bufferless process owns allocation; host device availability is not simulation state.
        (NativeGlobalSoundProcess process, NativeBattleModuleSound committed) = sound.Commit46B2A0(NativeGlobalSoundProcess);
        NativeGlobalSoundProcess = process;
        if (SkippingGuardianFormPresentation)
        {
            return;
        }
        events.Add(new BattleEvent(Frame, sequence is null ? "NativeModuleSound" : "NativeSequenceSound", actorId, null, 0, string.Empty) { NativeModuleSound = committed, NativeSound = sequence });
    }

    private void PublishNativeSequenceSound(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, BattleCombatant actor, NativeBattleSequenceCue cue)
    {
        graphics = graphics with { Models = graphics.Models.SetItem(model.Index, model) };
        NativeBattleSequenceSound source = new(checked((byte)cue.First), cue.Kind == NativeBattleSequenceCueKind.ActorSound, checked((byte)cue.Second), checked((ushort)cue.Third)) { OverrideOnContact4 = cue.OverrideSoundOnContact4 };
        NativeBattleModuleSound? sound = NativeBattleSequenceAudio.Resolve505AB0(source, model.Index, graphics.ActionGraphics?.FirstTarget,
            index => graphics.Models[index].ModelId4 ?? throw new NotSupportedException("Native5015A0 requires the actual loaded model BYTE4 identity."),
            index => ReadNativeGraphicsBone(graphics, index, 241, 0),
            point => (NativeCamera?.Projection ?? throw new NotSupportedException("Native505AB0 requires its actual5011B0 projection.")).ReadPan(point),
            definition.NativeGraphics?.Rules.ActorSounds1120, source.OverrideOnContact4 ? nativeActionPackets.ReadActiveContact().Primary.HitFlags3 : null);
        if (sound is not null)
        {
            PublishNativeModuleSound(sound, actor.Id, source);
        }
    }
}
