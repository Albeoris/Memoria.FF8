using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDevour62(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Devour requires its selected original effect.");
        NativeBattleDevour62Resources resources = effect.Binding.Devour62Resources ?? throw new NotSupportedException("Devour requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Devour requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Devour requires its actual CRT stream.");
        NativeBattleDevour62Frame frame = new(memory, effect, random, memory.RequireDevour62());
        if (initialize)
        {
            frame = NativeBattleDevour62Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Devour requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Devour requires its current camera.");
        NativeBattleDevour62Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], PoseSource, Execute);
        frame = NativeBattleDevour62Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Devour requires its actual current model rig.");

        void Publish(NativeBattleDevour62Frame current)
        {
            NativeEffectMemory = current.Memory.WithDevour62(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleDevour62Frame Execute(NativeBattleDevour62Frame current, NativeBattleDevour62Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDevour62Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleDevour62Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Devour contact lost its process memory.") };
                case NativeBattleDevour62UploadTim upload:
                    int source = current.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == upload.GlobalAddress)?.ArenaOffset ?? throw new NotSupportedException("Devour requires its executed TIM source pointer.");
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Devour requires its retained texture owner.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Devour requires its loaded TIM arena."), source);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleDevour62MusicGain music:
                    NativeGlobalSoundProcess soundProcess = NativeGlobalSoundProcess ?? throw new NotSupportedException("Devour requires its retained global sound process.");
                    NativeMusicPlaybackState playback = soundProcess.MusicPlayback ?? throw new NotSupportedException("Devour requires its retained music gain owner.");
                    NativeGlobalSoundProcess = soundProcess with { MusicPlayback = playback.SetGain46BB40(0, music.Gain) };
                    return current;
                case NativeBattleDevour62StageFade fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Devour requires its four actual stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("Devour requires four physical stage descriptors.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Value, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000 : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleDevour62WriteActorFlags flags:
                    if ((uint)flags.Actor >= 7 || graphics.Models.Length != 7)
                    {
                        throw new InvalidDataException("Devour actor writes exceed its seven physical models.");
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = flags.Flags }) };
                    return current;
                case NativeBattleDevour62FadeActors actorFade:
                    if (graphics.Models.Length != 7)
                    {
                        throw new InvalidDataException("Devour fade requires its seven physical models.");
                    }
                    for (int actor = 0; actor < 7; actor++)
                    {
                        NativeBattleGraphicsModel model = graphics.Models[actor];
                        if ((model.Flags0 & 2) != 0)
                        {
                            uint processColor = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Devour requires its retained process actor color.");
                            graphics = graphics with { Models = graphics.Models.SetItem(actor, NativeBattleDevour62ActorFade.Apply(model, actorFade.Value, processColor)) };
                        }
                    }
                    return current;
                case NativeBattleDevour62DrawQuads draw:
                    (graphics, current) = DrawNativeDevour62Quads(graphics, current, draw.Address);
                    return current;
                default:
                    throw new NotSupportedException("Devour requires its original immediate operation owner.");
            }
        }
    }
}
