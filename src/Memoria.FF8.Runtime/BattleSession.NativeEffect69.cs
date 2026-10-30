using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect69(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect69 requires its selected original effect.");
        NativeBattleEffect69Resources resources = effect.Binding.Effect69Resources ?? throw new NotSupportedException("Effect69 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect69 requires its current process memory.");
        NativeBattleEffect69Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect69 requires its current CRT stream."), memory.Effect69 ?? throw new NotSupportedException("Effect69 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect69 requires its addressed shared memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect69 requires its arithmetic constants.");
        NativeBattleEffect69Inputs inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], (_, actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect69 requires its current skeleton."), _ => graphics.Flags, _ => Camera().Roll, (_, address) => address switch
        {
            0xB8B7D8 => Stage().ModelColors?.Color40 ?? throw new NotSupportedException("Effect69 requires its current model color."),
            0xB8B9A8 => Stage().ModelColors?.Color44 ?? throw new NotSupportedException("Effect69 requires its current model color."),
            _ => throw new NotSupportedException("Effect69 selected an unsupported current color address.")
        }, Execute)
        {
            CapturePresentation = true,
            ReadIoResult = _ => NativeEffectIoResult ?? throw new NotSupportedException("Effect69 requires its current signed file result."),
            ReadScratchAddress = _ => ScratchAddress(),
            ReadStage = ReadStage,
            PrepareStage = PrepareStage
        };
        frame = initialize ? NativeBattleEffect69Controller.Initialize(frame, resources, inputs) : NativeBattleEffect69Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect69 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect69 requires its current projection.");
        NativeBattleStageState Stage() => NativeStage ?? throw new NotSupportedException("Effect69 requires its current stage.");
        int ScratchLength() => checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect69 requires its current projected capacity.")) * 8);
        uint ScratchAddress() => checked(0x196B018u - (uint)ScratchLength());

        void Publish(NativeBattleEffect69Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect69 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect69 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect69Frame Import(NativeBattleEffect69Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect69 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect69 ?? throw new InvalidDataException("Effect69 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect69 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect69 service lost its current addressed memory.") };
        }

        NativeBattleEffect69Frame PrepareStage(NativeBattleEffect69Frame current, int index, NativeBattlePackedMatrix32? matrix)
        {
            NativeBattleStageState stage = Stage();
            NativeBattleStageGroup group = NativeBattleCinematicStageShatter49.RestoreStageGroup508C90(stage.Groups[index]);
            if (matrix is not null)
            {
                group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(group.Matrices, matrix.ReadMatrix(), 0) };
            }
            NativeStage = stage with { Groups = stage.Groups.SetItem(index, group) };
            return current;
        }

        NativeBattleEffect69StageSource ReadStage(NativeBattleEffect69Frame current, int index)
        {
            uint geometry = RequireNativeCurrentStageGeometryAddress(index);
            return new(Stage().Groups[index], RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == index).ToImmutableArray(), ScratchAddress(), ScratchLength()) { GeometryAddress = geometry };
        }

        NativeBattleEffect69Frame Deliver(NativeBattleEffect69Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags = 0)
        {
            batch = NativeBattleEffect69Presentation.FinalizeCapturedBatch(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect69 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect69 requires its ordinary packet process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect69 requires its current arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(0x20DFAB8u + (uint)written.Cursor)) };
        }

        NativeBattleEffect69Frame Execute(NativeBattleEffect69Frame current, NativeBattleEffect69Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect69RegisterCamera camera:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect69 requires its texture scheduler.")).Register5099A0(camera.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect69ModelFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    ushort mask = flags.WordWidth ? flags.AndMask : (ushort)(0xFF00 | (byte)flags.AndMask);
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = (ushort)((flagged.Flags0 & mask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect69ModelPositionZ position:
                    NativeBattleGraphicsModel model = graphics.Models[position.Actor];
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect69 requires its current caster transform.");
                    model = model with { Transform = transform with { Position28 = transform.Position28 with { Z = position.Z } } };
                    if (position.WriteWorldTranslation)
                    {
                        model = model with { GeometryWorldMatrix = (model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect69 requires its current world matrix.")) with { Z = position.Z } };
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, model) };
                    return current;
                case NativeBattleEffect69StageFlags flags:
                    NativeBattleStageState stage = Stage();
                    NativeBattleStageGroup group = stage.Groups[flags.Group];
                    NativeStage = stage with { Groups = stage.Groups.SetItem(flags.Group, group with { Flags = (byte)((group.Flags & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect69ModelColors44 color:
                    graphics = graphics with { Models = graphics.Models.Select(model => model with { Color44 = color.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect69DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                case NativeBattleEffect69DeliverCommonBatch packets:
                    return Deliver(current, packets.Batch, 0x1D8E054);
                case NativeBattleEffect69Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect69StartVibration vibration:
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect69 requires its vibration process.")).Enqueue(vibration.Bank, 1, 128);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect69Controller.Write(current, vibration.TaskAddress, NativeBattleEffect69Controller.Read(current, vibration.TaskAddress).WriteDword(vibration.OutputOffset, unchecked((uint)queued.Handle)));
                case NativeBattleEffect69StopVibration stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect69 requires its vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect69DrawWindow108 window:
                    NativeBattleStaticMeshResource mesh = resources.ReadGeometry(window.GeometryAddress);
                    NativeBattleEffectArena context = NativeBattleEffect69Controller.ReadBytes(current, window.TemporaryAddress, 108);
                    NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(current.Memory, mesh, Projection(), context, colors, 17, 2, 120, offset =>
                    {
                        uint address = checked(window.SelectedVertices + (uint)offset);
                        NativeBattleEffectArena point = address >= ProgrammeAddress && (ulong)address + 8 <= (ulong)ProgrammeAddress + ProgrammeLength ? NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(address, 8).AsSpan()) : NativeBattleEffect69Controller.ReadBytes(current, address, 8);
                        (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                        return (NativeBattleEffect69Controller.Vector(point), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
                    }, pilotIdentity: window.PilotIdentity);
                    current = NativeBattleEffect69Controller.WriteBytes(current with { Memory = drawn.Memory }, window.TemporaryAddress, drawn.Context108, 108);
                    return Deliver(current, drawn.Batch, CursorGlobal);
                case NativeBattleEffect69DrawCaster caster:
                    (NativeBattleGraphicsState bodyGraphics, NativeBattleEffect69Frame bodyFrame) = DrawNativeEffect69Caster(graphics, current, caster, (body, batch) => Deliver(body, batch, 0x1D8E054));
                    graphics = graphics with { Models = bodyGraphics.Models };
                    return bodyFrame;
                case NativeBattleEffect69QueueStage512AA0 queue:
                    Publish(current);
                    QueueNativeStage512AA0(queue.Index, queue.Destination);
                    return Import(current);
                case NativeBattleEffect69ReloadStage512AC0 reload:
                    Publish(current);
                    ReloadNativeStage512AC0(reload.Index, reload.Source);
                    return Import(current);
                default:
                    throw new NotSupportedException("Effect69 emitted an unsupported native operation.");
            }
        }
    }
}
