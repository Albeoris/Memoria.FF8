using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeGastricJuice128(NativeBattleGraphicsState graphics) => RunNativeGastricJuice128(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeGastricJuice128(NativeBattleGraphicsState graphics) => RunNativeGastricJuice128(graphics, false);

    private NativeBattleGraphicsState RunNativeGastricJuice128(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Gastric Juice requires its selected original effect.");
        NativeBattleGastricJuice128Resources resources = effect.Binding.GastricJuice128Resources ?? throw new NotSupportedException("Gastric Juice requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Gastric Juice requires its actual process memory.");
        NativeBattleGastricJuice128Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Gastric Juice requires its CRT stream."), memory.GastricJuice128 ?? throw new NotSupportedException("Gastric Juice requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Gastric Juice requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Gastric Juice requires its original interpolation constants.");
        NativeBattleGastricJuice128Inputs inputs = new(nativeActionPackets, colors, actor => graphics.Models[actor], actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gastric Juice requires its current skeleton.")).BoneCount, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Gastric Juice requires its current projection."), Anchor, Bone, Execute);
        frame = initialize ? NativeBattleGastricJuice128Controller.Initialize(frame, resources, inputs) : NativeBattleGastricJuice128Controller.Advance(frame, resources, inputs);
        if (!initialize && frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Gastric Juice requires its current camera.");

        void Publish(NativeBattleGastricJuice128Frame current)
        {
            NativeEffectMemory = current.Memory with { GastricJuice128 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gastric Juice lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleGastricJuice128Frame Import(NativeBattleGastricJuice128Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Gastric Juice external service lost its process memory.");
            return current with { Memory = latest, State = latest.GastricJuice128 ?? throw new InvalidDataException("Gastric Juice external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Gastric Juice external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Gastric Juice external service lost its CRT stream.") };
        }

        NativeBattleGastricJuice128Frame Deliver(NativeBattleGastricJuice128Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Gastric Juice requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleGastricJuice128Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleGastricJuice128Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleGastricJuice128AnchorStep Anchor(NativeBattleGastricJuice128Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gastric Juice requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Gastric Juice requires the actual actor heading.")).Angle36 });
        }

        NativeBattleGastricJuice128PointStep Bone(NativeBattleGastricJuice128Frame current, int actor, short selector, short distance)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gastric Juice requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, checked((byte)selector), distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gastric Juice bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleGastricJuice128Frame Execute(NativeBattleGastricJuice128Frame current, NativeBattleGastricJuice128Operation operation)
        {
            switch (operation)
            {
                case NativeBattleGastricJuice128Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleGastricJuice128ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleGastricJuice128SnapshotBody snapshot:
                    NativeDeformedBodyBridge captured = SnapshotNativeDeformedBody(current.Memory, current.Effect, current.Random, current.Staging!, graphics, snapshot.Actor, snapshot.CacheAddress, NativeBattleGastricJuice128Profile.BodyCacheBytes);
                    current = current with { Memory = captured.Memory, Random = captured.Random, Staging = captured.Staging };
                    graphics = captured.Graphics;
                    return current;
                case NativeBattleGastricJuice128DrawBody132 body:
                    uint cacheAddress = checked(current.State.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal) + NativeBattleGastricJuice128Profile.BodyCacheOffset);
                    NativeDeformedBodyBridge deformed = DrawNativeDeformedBody(current.Memory, current.Effect, current.Random, current.Staging!, graphics, body.Actor, body.Fraction, cacheAddress, NativeBattleGastricJuice128Profile.BodyCacheBytes);
                    current = current with { Memory = deformed.Memory, Random = deformed.Random, Staging = deformed.Staging };
                    graphics = deformed.Graphics;
                    return current;
                case NativeBattleGastricJuice128AdvanceAct act:
                    return AdvanceAct(current, act.TaskAddress);
                case NativeBattleGastricJuice128DeliverRibbonBatch ribbon:
                    return Deliver(current, ribbon.Batch);
                case NativeBattleGastricJuice128DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.Phase, sprite.DepthShift);
                case NativeBattleGastricJuice128Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Acid operation has no proved implementation.");
            }
        }

        NativeBattleGastricJuice128Frame AdvanceAct(NativeBattleGastricJuice128Frame current, uint taskAddress)
        {
            NativeBattleModulePoolTask task = NativeBattleGastricJuice128Controller.Read(current, taskAddress);
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, task.ReadBytes(0x16, 8)).Write(8, BitConverter.GetBytes(current.State.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal) + NativeBattleGastricJuice128Profile.MorphOffset));
            NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(0x20, 0x248).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.MainAct, 0, (next, node) =>
            {
                NativeBattleEffectArena normalized = current.State.ReadView(NativeBattleGastricJuice128Profile.CurrentMatrixAddress, 32);
                (byte[] bytes, bool[] known) = normalized.ReadSnapshot(0, 32);
                if (!NativeBattleBillboardActContext12.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging!, 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Gastric Juice requires its current primitive process.");
                NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Gastric Juice requires its current projection controls.");
                uint morph = checked(current.State.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal) + NativeBattleGastricJuice128Profile.MorphOffset);
                NativeBattleBillboardActContext12Step drawn = NativeBattleBillboardActContext12.Draw(resources.MainAct, node, context, Camera().CapturePackedView1D97778(), new(bytes.ToImmutableArray(), known.ToImmutableArray()), temporary, current.Memory.Arena!, new(current.Memory.GeometryRegisters!, current.Memory.ScriptedColorRegisters!), process.ModelRendererRegisters!, Camera().Projection!, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, colors, morph, NativeBattleGastricJuice128Profile.MorphBytes);
                current = current with { Memory = current.Memory with { Arena = drawn.Arena, GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88) };
                current = Deliver(current, drawn.Batch);
                current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, 88) };
                return next;
            });
            return NativeBattleGastricJuice128Controller.Write(current, taskAddress, NativeBattleGastricJuice128Controller.Read(current, taskAddress).WriteBytes(0x20, advanced.Workspace.Data.AsSpan()));
        }

        NativeBattleGastricJuice128Frame DrawSprite(NativeBattleGastricJuice128Frame current, uint address, uint sourceAddress, ushort phase, byte depthShift)
        {
            NativeBattleModuleResource resource = resources.Sprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gastric Juice sprite drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Gastric Juice sprite arguments differ from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Gastric Juice sprite dispatcher selected a different source."), Camera().Projection ?? throw new NotSupportedException("Gastric Juice sprite drawing requires its current projection."), Camera().Roll, 17, depthShift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
