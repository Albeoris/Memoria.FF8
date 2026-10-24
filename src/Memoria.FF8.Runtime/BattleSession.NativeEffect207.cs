using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect207(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect207 requires its selected original effect.");
        NativeBattleEffect207Resources resources = effect.Binding.Effect207Resources ?? throw new NotSupportedException("Effect207 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect207 requires its current process memory.");
        NativeBattleEffect207Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect207 requires its current CRT stream."), memory.Effect207 ?? throw new NotSupportedException("Effect207 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect207 requires its current addressed memory.") };
        NativeBattleEffect207Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect207 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => CaptureNativeEffect207Model(graphics.Models[actor]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect207 requires its current projection."), _ => Camera().Roll, Anchor571400, Execute);
        frame = initialize ? NativeBattleEffect207Controller.Initialize(frame, resources, inputs) : NativeBattleEffect207Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect207 requires its current camera.");

        void Publish(NativeBattleEffect207Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect207 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect207 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect207Frame Import(NativeBattleEffect207Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect207 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect207 ?? throw new InvalidDataException("Effect207 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect207 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect207 service lost its addressed memory.") };
        }

        NativeBattleEffect207PointStep Anchor571400(NativeBattleEffect207Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect207 requires its current actor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect207 requires its current actor skeleton.");
            NativeBattleVector first = Observe(0xF0);
            NativeBattleVector second = Observe(0xF1);
            return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)((first.X + second.X) / 2)))).Write(2, BitConverter.GetBytes(unchecked((short)((first.Y + second.Y) / 2)))).Write(4, BitConverter.GetBytes(unchecked((short)((first.Z + second.Z) / 2)))).Write(6, BitConverter.GetBytes(transform.Angle36)));

            NativeBattleVector Observe(byte selector)
            {
                NativeFileStaging = current.Staging;
                NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0));
                current = current with { Memory = observed, Staging = NativeFileStaging };
                return new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
            }
        }

        NativeBattleEffect207Frame Execute(NativeBattleEffect207Frame current, NativeBattleEffect207Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect207UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect207Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect207 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect207RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect207Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect207 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect207 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect207Clear45BEE0 clear:
                    return current with { Memory = NativeBattleGravija214Environment.Clear45BEE0(current.Memory, new(checked((short)clear.X), checked((short)clear.Y), checked((short)clear.Width), checked((short)clear.Height)), clear.Red, clear.Green, clear.Blue) };
                case NativeBattleEffect207SetOrigin45B5E0 origin:
                    return current with { Memory = NativeBattleGravija214Environment.SetOrigin45B5E0(current.Memory, origin.X, origin.Y) };
                case NativeBattleEffect207Sound501330 sound:
                    uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4));
                    Publish(current);
                    PublishNativeModuleSound(new(descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect207AllContacts506BA0 contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    ImmutableArray<NativeBattleContactPacket> selected = Enumerable.Range(contacts.FirstContactIndex, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray();
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = selected });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect207AppendCommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect207Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect207DrawBatch draw:
                    if (draw.CursorGlobal != NativeBattleEffect207Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect207 selected an unsupported private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect207 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect207 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect207 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static NativeBattleEffectArena CaptureNativeEffect207Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = NativeBattleRaldo19ModelMemory.Capture(model);
        if (model.Transform is { } transform)
        {
            bytes = bytes.Write(0x26, BitConverter.GetBytes(transform.Scale38));
        }
        if (model.GeometryBounds52 is { } bounds)
        {
            bytes = bytes.Write(0x36, BitConverter.GetBytes(bounds.Minimum.Y));
        }
        return bytes;
    }

}
