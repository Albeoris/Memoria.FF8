using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect173(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect173 requires its selected original effect.");
        NativeBattleEffect173Resources resources = effect.Binding.Effect173Resources ?? throw new NotSupportedException("Effect173 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect173 requires its current process memory.");
        NativeBattleEffect173Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect173 requires its current CRT stream."), memory.Effect173 ?? throw new NotSupportedException("Effect173 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect173 requires its current addressed memory.") };
        NativeBattleEffect173Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect173 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect173 requires its current world matrix."), null), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect173 requires its current projection."), _ => Camera().Roll, Point502170, Execute);
        frame = initialize ? NativeBattleEffect173Controller.Initialize(frame, resources, inputs) : NativeBattleEffect173Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect173 requires its current camera.");

        void Publish(NativeBattleEffect173Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect173 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect173 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect173Frame Import(NativeBattleEffect173Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect173 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect173 ?? throw new InvalidDataException("Effect173 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect173 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect173 service lost its addressed memory.") };
        }

        NativeBattleEffect173PointStep Point502170(NativeBattleEffect173Frame current, int actor, byte selector, short fraction, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect173 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, fraction);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect173Frame Execute(NativeBattleEffect173Frame current, NativeBattleEffect173Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect173UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect173Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect173 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect173RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect173Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect173 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect173 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect173TargetCenter5020A0 target:
                    NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (target.Selector & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect173 target center requires its actual selected physical positions.")).ToArray());
                    NativeBattleEffectArena vector = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(center.X)).Write(2, BitConverter.GetBytes(center.Y)).Write(4, BitConverter.GetBytes(center.Z));
                    return NativeBattleEffect173Controller.WriteBytes(current, target.Destination, vector, 6);
                case NativeBattleEffect173Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect173 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Amount), ColorFlags = intensity.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect173Sound501330 sound:
                    uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4));
                    Publish(current);
                    PublishNativeModuleSound(new(descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect173AllContacts506BA0 contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    ImmutableArray<NativeBattleContactPacket> selected = Enumerable.Range(contacts.FirstContactIndex, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray();
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = selected });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect173AppendCommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect173Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect173DrawBatch draw:
                    if (draw.CursorGlobal != NativeBattleEffect173Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect173 selected an unsupported private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect173 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect173 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect173 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }
}
