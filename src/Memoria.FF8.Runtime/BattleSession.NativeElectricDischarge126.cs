using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeElectricDischarge126(NativeBattleGraphicsState graphics) => RunNativeElectricDischarge126(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeElectricDischarge126(NativeBattleGraphicsState graphics) => RunNativeElectricDischarge126(graphics, false);

    private NativeBattleGraphicsState RunNativeElectricDischarge126(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Electric Discharge requires its selected original effect.");
        NativeBattleElectricDischarge126Resources resources = effect.Binding.ElectricDischarge126Resources ?? throw new NotSupportedException("Electric Discharge requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Electric Discharge requires its actual process memory.");
        NativeBattleElectricDischarge126Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Electric Discharge requires its CRT stream."), memory.ElectricDischarge126 ?? throw new NotSupportedException("Electric Discharge requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Electric Discharge requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Electric Discharge requires its original interpolation constants.");
        NativeBattleElectricDischarge126Inputs inputs = new(nativeActionPackets, colors, actor => graphics.Models[actor], actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Electric Discharge requires its current skeleton.")).BoneCount, CurrentBinding, _ => Camera().CapturePackedView1D97778(), Anchor, Bone, AdvanceAct, Execute);
        frame = initialize ? NativeBattleElectricDischarge126Controller.Initialize(frame, resources, inputs) : NativeBattleElectricDischarge126Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Electric Discharge requires its current camera.");

        void Publish(NativeBattleElectricDischarge126Frame current)
        {
            NativeEffectMemory = current.Memory with { ElectricDischarge126 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Electric Discharge lost its addressed memory.");
            nativeCrtRandom = current.Random;
            graphics = graphics with { OriginalEffect = current.Effect };
        }

        NativeBattleElectricDischarge126Frame Import(NativeBattleElectricDischarge126Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Electric Discharge external service lost its process memory.");
            return current with { Effect = graphics.OriginalEffect ?? throw new InvalidDataException("Electric Discharge lost its current effect tasks."), Memory = latest, State = latest.ElectricDischarge126 ?? throw new InvalidDataException("Electric Discharge external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Electric Discharge external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Electric Discharge external service lost its CRT stream.") };
        }

        NativeBattleElectricDischarge126Frame Deliver(NativeBattleElectricDischarge126Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Electric Discharge requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleElectricDischarge126Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleElectricDischarge126Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleElectricDischarge126AnchorStep Anchor(NativeBattleElectricDischarge126Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Electric Discharge requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Electric Discharge requires its actual actor heading.")).Angle36 });
        }

        NativeBattleElectricDischarge126PointStep Bone(NativeBattleElectricDischarge126Frame current, int actor, short selector, short distance)
        {
            Publish(current);
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Electric Discharge requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, checked((byte)selector), distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Electric Discharge bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleResourceRowReference CurrentBinding(int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (model.ActiveStagedResourceRow100 is uint staged)
            {
                return new(null, staged);
            }
            int row = model.ActiveResourceRow100 ?? throw new NotSupportedException("Electric Discharge requires its actual current model resource row.");
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Electric Discharge requires its actual current CPU resource owner.");
            if ((uint)row >= (uint)cpu.Rows.Length)
            {
                throw new InvalidDataException("Electric Discharge model row leaves its physical CPU table.");
            }
            return new(row, null);
        }

        NativeBattleElectricDischarge126Frame Execute(NativeBattleElectricDischarge126Frame current, NativeBattleElectricDischarge126Operation operation)
        {
            Publish(current);
            switch (operation)
            {
                case NativeBattleElectricDischarge126SpatialSound sound:
                    PublishNativeModuleSound(sound.Value, checked((uint)sound.Volume), sound.Position);
                    return Import(current);
                case NativeBattleElectricDischarge126Flash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.Argument0), unchecked((short)flash.Argument1), unchecked((short)flash.Argument2), unchecked((short)flash.Argument3)) };
                    return current;
                case NativeBattleElectricDischarge126Stage stage:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Electric Discharge requires its retained stage color rows.");
                    NativeStage = stageState with { Groups = stageState.Groups.Select(group => group with { ColorDepth = stage.Depth, ColorFlags = stage.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleElectricDischarge126BlendActorColor blend:
                    NativeBattleGraphicsModel tinted = graphics.Models[blend.Actor];
                    NativeBattleWeightedColorStep color = NativeBattleWeightedColor56CC00.Blend(current.Memory, blend.FirstColor, blend.SecondColor, blend.FirstFactor, blend.SecondFactor, tinted.Color40 ?? throw new NotSupportedException("Electric Discharge requires its current actor color DWORD."), colors);
                    graphics = graphics with { Models = graphics.Models.SetItem(blend.Actor, tinted with { Color40 = color.Color }) };
                    return current with { Memory = color.Memory };
                case NativeBattleElectricDischarge126LoadView:
                    NativeBattlePackedMatrix32 view = Camera().CapturePackedView1D97778();
                    short? padding = view.Known[18] && view.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(view.Bytes.AsSpan().Slice(18)) : null;
                    return current with { Memory = current.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Electric Discharge requires its current geometry registers."), view.ReadMatrix(), true, true, padding) } };
                case NativeBattleElectricDischarge126ComposeCaster compose:
                    (NativeBattleFileStaging composedStaging, NativeBattleGraphicsState composedGraphics) = PrepareNativeLightningBones(current.Staging!, graphics, compose.Actor, true);
                    graphics = composedGraphics;
                    return current with { Staging = composedStaging };
                case NativeBattleElectricDischarge126RestoreCaster restore:
                    (NativeBattleFileStaging restoredStaging, NativeBattleGraphicsState restoredGraphics) = PrepareNativeLightningBones(current.Staging!, graphics, restore.Actor, false);
                    graphics = restoredGraphics;
                    return current with { Staging = restoredStaging };
                case NativeBattleElectricDischarge126ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleElectricDischarge126ProjectWorld project:
                    uint working = current.State.ReadDword(NativeBattleElectricDischarge126Profile.WorkingGlobal);
                    NativeBattleRibbonBank bank = new(checked(working + NativeBattleElectricDischarge126Profile.WorldPointsOffset), checked((int)(working - NativeBattleElectricDischarge126Profile.ArenaAddress) + NativeBattleElectricDischarge126Profile.WorldPointsOffset), NativeBattleElectricDischarge126Profile.PointCount);
                    return current with { Memory = NativeBattleRibbonProjection.ProjectCurrent(current.Memory, project.HeadAddress, bank, Camera().Projection ?? throw new NotSupportedException("Electric Discharge requires its current projection."), current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Electric Discharge requires its current projection controls.")) };
                case NativeBattleElectricDischarge126DrawWorld world:
                    var worldDraw = NativeBattleElectricDischarge126Ribbons.DrawProjected(current, world.HeadAddress, world.Count, world.TextureVOffset, resources, NativeBattleRibbonProjection.CaptureWorldDirectionWord(current.Memory, Camera().Projection ?? throw new NotSupportedException("World ribbon requires its current projection.")));
                    return Deliver(worldDraw.Frame, worldDraw.Batch);
                case NativeBattleElectricDischarge126DrawBoneTrail boneTrail:
                    byte caster = current.Effect.Parameters!.Actor78;
                    if (CurrentBinding(caster) != boneTrail.Binding)
                    {
                        throw new InvalidDataException("Electric Discharge bone trail leaves its current addressed caster binding.");
                    }
                    uint boneBank = checked(current.State.ReadDword(NativeBattleElectricDischarge126Profile.WorkingGlobal) + NativeBattleElectricDischarge126Profile.BonePointsOffset);
                    current = current with { Memory = ProjectNativeLightningBoneTrail(current.Memory, current.Effect, current.Staging!, graphics, caster, boneTrail.HeadAddress, boneBank, NativeBattleElectricDischarge126Profile.PointCount * NativeBattleElectricDischarge126Profile.PointStride) };
                    var boneDraw = NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(current, boneTrail.HeadAddress, boneTrail.Count, resources);
                    return Deliver(boneDraw.Frame, boneDraw.Batch);
                case NativeBattleElectricDischarge126DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.Phase, 2);
                case NativeBattleElectricDischarge126ContactAll contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contacts.First, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Electric Discharge operation has no proved implementation.");
            }
        }

        NativeBattleElectricDischarge126TaskStep AdvanceAct(NativeBattleElectricDischarge126Frame current, uint taskAddress, NativeBattleEffectArena context)
        {
            NativeBattleModulePoolTask task = NativeBattleElectricDischarge126Controller.Read(current, taskAddress);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0xC, 0xC8);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Act, 0, (next, node) =>
            {
                current = NativeBattleElectricDischarge126Drawing.EmitNode(current, resources.Act, context, node, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Electric Discharge requires its projection."), colors, Deliver);
                return next;
            });
            current = NativeBattleElectricDischarge126Controller.Write(current, taskAddress, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleElectricDischarge126Controller.Read(current, taskAddress), 0xC, advanced.Workspace));
            return new(current, advanced.Remaining == 0 ? 2 : 0);
        }

        NativeBattleElectricDischarge126Frame DrawSprite(NativeBattleElectricDischarge126Frame current, uint address, uint sourceAddress, ushort phase, byte depthShift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Electric Discharge sprite drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Electric Discharge sprite arguments differ from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Electric Discharge sprite dispatcher selected a different source."), Camera().Projection ?? throw new NotSupportedException("Electric Discharge sprite drawing requires its current projection."), Camera().Roll, 17, depthShift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
