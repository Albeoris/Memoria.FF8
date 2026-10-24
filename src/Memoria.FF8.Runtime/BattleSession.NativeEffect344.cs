using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect344(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect344 requires its selected original effect.");
        NativeBattleEffect344Resources resources = effect.Binding.Effect344Resources ?? throw new NotSupportedException("Effect344 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect344 requires its current process memory.");
        NativeBattleEffect344Frame frame = new(memory, effect, memory.Effect344 ?? throw new NotSupportedException("Effect344 requires its retained physical owners.")) { Staging = NativeFileStaging };
        NativeBattleEffect344Inputs inputs = new(_ => graphics.Flags, _ => nativeActionPackets, Execute);
        frame = initialize ? NativeBattleEffect344Controller.Initialize(frame, resources, inputs) : NativeBattleEffect344Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        void Publish(NativeBattleEffect344Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect344 = current.State };
            NativeFileStaging = current.Staging;
        }

        NativeBattleEffect344Frame Import(NativeBattleEffect344Frame current)
        {
            NativeBattleEffectMemory updated = NativeEffectMemory ?? throw new InvalidDataException("Effect344 service lost its current process memory.");
            return current with { Memory = updated, State = updated.Effect344 ?? throw new InvalidDataException("Effect344 service lost its current state."), Staging = NativeFileStaging };
        }

        NativeBattleEffect344Frame Execute(NativeBattleEffect344Frame current, NativeBattleEffect344Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect344MusicFade46BBC0 fade:
                    NativeGlobalSoundProcess sound = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect344 requires its current sound process.");
                    NativeGlobalSoundProcess = sound with { MusicPlayback = (sound.MusicPlayback ?? throw new NotSupportedException("Effect344 requires its current playback owner.")).FadeGain46BBC0(unchecked((uint)fade.Channel), unchecked((byte)fade.Duration), unchecked((ushort)fade.Gain)) };
                    return current;
                case NativeBattleEffect344StageMusic501A20 music:
                    NativeMusicProcess = (NativeMusicProcess ?? throw new NotSupportedException("Effect344 requires its current music process.")).StageLoad501A20(music.SourceAddress, resources.ReadStatic);
                    NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary.AppendMusicLoad501A20(music.CompletionByteAddress, (address, value) => current = current with { State = current.State.Write(address, [value]) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect344RegisterCamera5099A0 camera:
                    if (camera.SourceAddress != CameraAddress)
                    {
                        throw new NotSupportedException("Effect344 selected an unsupported camera source.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect344 requires its current resource scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect344 requires its current camera owner.")).RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect344SetSequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return current;
                case NativeBattleEffect344MusicGain501B10 gain:
                    NativeMusicProcess = (NativeMusicProcess ?? throw new NotSupportedException("Effect344 requires its current music commands.")).QueueGain501B10(gain.Duration, gain.Gain);
                    return current;
                case NativeBattleEffect344ConsumeFirstContact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Effect344 selected an unsupported native service.");
            }
        }
    }

    private bool TryWriteNativeEffect344Completion(uint address, byte value)
    {
        if (address != CompletionGlobal)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect344 completion requires its current process memory.");
        NativeBattleEffect344State state = memory.Effect344 ?? throw new NotSupportedException("Effect344 completion requires its retained byte owner.");
        NativeEffectMemory = memory with { Effect344 = state.Write(address, [value]) };
        return true;
    }
}
