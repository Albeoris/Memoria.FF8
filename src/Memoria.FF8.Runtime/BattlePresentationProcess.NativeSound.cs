namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    public NativeGlobalSoundProcess? NativeGlobalSoundProcess { get; private set; }

    private void PublishNativeModuleSound(NativeBattleModuleSound sound, string? actorId = null, NativeBattleSequenceSound? sequence = null)
    {
        NativeBattleModelLifecycleServices? services = HasBattleEntry ? RequireHostedAudioFrameServices() : null;
        if (services is null)
        {
            presentationSounds.Add(sound);
            return;
        }
        //46B2A0 writes the global original-ID slot before4699A0 can reject output.
        //The declared bufferless process owns allocation; host device availability is not simulation state.
        (NativeGlobalSoundProcess process, NativeBattleModuleSound committed) = sound.Commit46B2A0(NativeGlobalSoundProcess);
        NativeGlobalSoundProcess = process;
        services.EmitSound(committed, actorId, sequence);
        presentationSounds.Add(committed);
    }

    private void PublishNativeStopAllSounds(string message)
    {
        hostedFrameServices?.EmitStopAllSounds(message);
    }

    private NativeBattleModelLifecycleServices RequireHostedAudioFrameServices()
    {
        return hostedFrameServices ?? throw new InvalidOperationException("Hosted native audio requires an active presentation frame.");
    }

    private byte ReadNativeActionSoundModelId(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        byte identity = graphics.ActorModels.Get(owner).ModelId4 ?? throw new NotSupportedException("Native actor audio requires the actual loaded model BYTE4 identity.");
        NativeBattleGraphicsResource? resource = identity < 16 ? ReadStandingPresentationModelResource(graphics, owner) : null;
        return resource?.SequenceSoundModelId ?? resource?.CombatStyleModelId ?? identity;
    }

    private void PublishNativeSequenceSound(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsModel model, string actorId, NativeBattleSequenceCue cue)
    {
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, model) };
        NativeBattleSequenceSound source = new(checked((byte)cue.First), cue.Kind == NativeBattleSequenceCueKind.ActorSound, checked((byte)cue.Second), checked((ushort)cue.Third)) { OverrideOnContact4 = cue.OverrideSoundOnContact4 };
        NativeBattleModuleSound? sound = NativeBattleSequenceAudio.Resolve505AB0(source, owner, () => graphics.ActionGraphics?.FirstTargetBinding?.Resolve(graphics.ActorModels, graphics.NativeBindings),
            index => ReadNativeActionSoundModelId(graphics, index),
            index => ReadNativeGraphicsBone(graphics, index, 241, 0),
            point => (NativeCamera?.Projection ?? throw new NotSupportedException("Native505AB0 requires its actual5011B0 projection.")).ReadPan(point),
            this.resources.NativeGraphics?.Rules.ActorSounds1120, source.OverrideOnContact4 ? nativeActionPackets.ReadActiveContact().Primary.HitFlags : null);
        if (sound is not null)
        {
            PublishNativeModuleSound(sound, actorId, source);
        }
    }
}
