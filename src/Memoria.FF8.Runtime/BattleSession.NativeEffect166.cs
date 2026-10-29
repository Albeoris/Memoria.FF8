using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect166(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect166 requires its selected original effect.");
        NativeBattleEffect166Resources resources = effect.Binding.Effect166Resources ?? throw new NotSupportedException("Effect166 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect166 requires its current process memory.");
        NativeBattleEffect166Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect166 requires its current CRT stream."), memory.Effect166 ?? throw new NotSupportedException("Effect166 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect166 requires its current addressed memory.") };
        NativeBattleEffect166Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect166 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], ReadBone, (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect166 requires its current model world matrix."), null), (_, actor) => graphics.Models[actor].Sequence ?? throw new NotSupportedException("Effect166 requires its current sequence header BYTE0."), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect166 requires its current projection."), Execute)
        {
            CapturePresentation = true,
            ReadModelArena9C = (_, actor) => NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]),
            ReadCameraRoll = _ => Camera().Roll,
            ReadAnchor571400 = Anchor
        };
        frame = initialize ? NativeBattleEffect166Controller.Initialize(frame, resources, inputs) : NativeBattleEffect166Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect166 requires its current camera.");

        void Publish(NativeBattleEffect166Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect166 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect166 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect166Frame Import(NativeBattleEffect166Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect166 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect166 ?? throw new InvalidDataException("Effect166 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect166 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect166 service lost its addressed memory.") };
        }

        NativeBattlePackedMatrix32 ReadBone(NativeBattleEffect166Frame current, int actor, int selector)
        {
            NativeFileStaging = current.Staging;
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect166 requires its current caster skeleton.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
            if ((uint)selector >= (uint)bones.Length)
            {
                throw new InvalidDataException("Effect166 selected a bone outside its current physical skeleton.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(bones[selector], null);
        }

        NativeBattleEffect166PointStep Anchor(NativeBattleEffect166Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            if ((graphics.Models[actor].Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect166 requires its current anchor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor point = ReadNativeModelAnchor(graphics, actor);
            return new(current, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect166Frame Execute(NativeBattleEffect166Frame current, NativeBattleEffect166Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect166UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect166Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect166 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect166RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect166Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect166 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect166 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect166TargetCenter5020A0 target:
                    NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (target.Mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect166 target center requires its actual selected physical positions.")).ToArray());
                    NativeBattleEffectArena vector = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(center.X)).Write(2, BitConverter.GetBytes(center.Y)).Write(4, BitConverter.GetBytes(center.Z));
                    return NativeBattleEffect166Controller.WriteBytes(current, target.DestinationAddress, vector, 6);
                case NativeBattleEffect166Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Rise), checked((short)flash.Hold), checked((short)flash.Fall), flash.Color) };
                    return current;
                case NativeBattleEffect166Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect166 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Intensity), ColorFlags = intensity.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect166Sound501330 sound:
                    int soundIndex = sound.SourceAddress switch { 0xD7803C => 0, 0xD78040 => 1, _ => -1 };
                    if (soundIndex < 0)
                    {
                        throw new NotSupportedException("Effect166 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sounds[soundIndex], sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect166WriteModelBytes model:
                    graphics = graphics with { Models = graphics.Models.SetItem(model.Actor, NativeBattleRaldo19ModelMemory.Apply(graphics.Models[model.Actor], model.Offset, model.Data, 0, model.Length)) };
                    return current;
                case NativeBattleEffect166Sequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return current;
                case NativeBattleEffect166Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C];
                    int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Effect166 requires its actual current header contact base.");
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(checked(firstContact + contact.Index))] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect166CommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect166Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect166Capture5700 capture:
                    (graphics, current) = CaptureNativeEffect166Stage(graphics, current, capture.Preparation, resources, inputs, capture.PilotIdentity);
                    return current;
                case NativeBattleEffect166DrawBatch draw:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect166 requires its current ordering table.");
                    if (draw.CursorGlobal != NativeBattleEffect166Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect166 selected an unsupported packet cursor.");
                    }
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect166 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect166 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }
}
