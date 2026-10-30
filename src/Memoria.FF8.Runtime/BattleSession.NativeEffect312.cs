using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect312(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect312 requires its selected original effect.");
        NativeBattleEffect312Resources resources = effect.Binding.Effect312Resources ?? throw new NotSupportedException("Effect312 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect312 requires its current process memory.");
        NativeBattleEffect312Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect312 requires its current CRT stream."), memory.Effect312 ?? throw new NotSupportedException("Effect312 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect312 requires its current addressed memory.") };
        frame = frame with { ReadPhysical = (address, length) => ReadNativeEffect312Physical(graphics, frame, address, length) };
        NativeBattleEffect312Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect312 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (current, address, length) => ReadNativeEffect312Physical(graphics, current, address, length), (_, address) => CaptureNativeEffect301Model(graphics.Models[NativeBattleEffect312Controller.Actor(address)]), (_, address) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[NativeBattleEffect312Controller.Actor(address)].GeometryWorldMatrix ?? throw new NotSupportedException("Effect312 requires its current world matrix."), null), (current, address, bone) => ReadNativeEffect312Bone(graphics, current, address, bone), TargetCenter, (current, address, retained) => ReadNativeEffect312Anchor(graphics, current, address, retained), (_, address) => ReadNativeEffect312SequenceByte(graphics, address), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect312 requires its current projection."), _ => Camera().Roll, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect312Controller.Initialize(frame, resources, inputs) : NativeBattleEffect312Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        static int NativeEffect312ContactIndex(uint address)
        {
            long offset = (long)address - WaterContactsAddress;
            return offset >= 0 && offset % 24 == 0 ? checked((int)(offset / 24)) : throw new InvalidDataException("Effect312 requires its actual physical contact pointer.");
        }

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect312 requires its current camera.");

        void Publish(NativeBattleEffect312Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect312 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect312 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect312Frame Import(NativeBattleEffect312Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect312 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect312 ?? throw new InvalidDataException("Effect312 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect312 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect312 service lost its addressed memory.") };
        }

        NativeBattleEffect312PointStep TargetCenter(NativeBattleEffect312Frame current, ushort mask, NativeBattleEffectArena retained)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect312 requires its selected physical target positions.")).ToArray());
            return new(current, retained.Write(0, BitConverter.GetBytes(center.X)).Write(2, BitConverter.GetBytes(center.Y)).Write(4, BitConverter.GetBytes(center.Z)));
        }

        NativeBattleEffect312Frame Execute(NativeBattleEffect312Frame current, NativeBattleEffect312Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect312UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect312Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect312 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect312RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect312Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect312 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect312 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect312Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Rise), checked((short)flash.Hold), checked((short)flash.Fall), flash.Color, pilotIdentity: flash.PilotIdentity) };
                    return current;
                case NativeBattleEffect312Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect312 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Intensity), ColorFlags = intensity.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect312Sound501330 sound:
                    int soundIndex = sound.SourceAddress switch { 0xCDF64C => 0, 0xCDF650 => 1, _ => -1 };
                    if (soundIndex < 0)
                    {
                        throw new NotSupportedException("Effect312 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect312WriteModelBytes model:
                    graphics = ApplyNativeEffect312ModelBytes(graphics, model.ModelAddress, model.Offset, model.Data, model.Length);
                    return current;
                case NativeBattleEffect312SetModelAnimation505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)NativeBattleEffect312Controller.Actor(sequence.ModelAddress)), checked((byte)sequence.Mode));
                    return current;
                case NativeBattleEffect312Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C];
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(NativeEffect312ContactIndex(contact.TargetArgumentAddress))] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect312CommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect312Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect312CaptureStage59D400 capture:
                    (graphics, current) = CaptureNativeEffect312Stage(graphics, current, NativeBattleEffect312Capture.Prepare(NativeBattleEffect312Controller.ReadBytes(current, capture.TargetAddress, 8)), resources, inputs, capture.PilotIdentity);
                    return current;
                case NativeBattleEffect312DrawBatch draw:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect312 requires its current ordering table.");
                    if (draw.CursorGlobal != NativeBattleEffect312Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect312 selected an unsupported packet cursor.");
                    }
                    NativeBattleEffect312Controller.RequirePacketCapacity(current, draw.Batch, draw.CursorAdvanceAfterBatch);
                    uint packetAddress = current.State.ReadDword(draw.CursorGlobal);
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(packetAddress, 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect312 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect312 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }
}
