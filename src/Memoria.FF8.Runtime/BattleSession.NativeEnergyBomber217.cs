using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEnergyBomber217(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Energy Bomber requires its selected original effect.");
        NativeBattleEnergyBomber217Resources resources = effect.Binding.EnergyBomber217Resources ?? throw new NotSupportedException("Energy Bomber requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Energy Bomber requires its actual process memory.");
        NativeBattleEnergyBomber217Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Energy Bomber requires its CRT stream."), memory.EnergyBomber217 ?? throw new NotSupportedException("Energy Bomber requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Energy Bomber requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Energy Bomber requires its original interpolation constants.");
        NativeBattleEnergyBomber217Inputs inputs = new(nativeActionPackets, Anchor, Bone, AdvanceAct, Blend, Execute);
        frame = initialize ? NativeBattleEnergyBomber217Controller.Initialize(frame, resources, inputs) : NativeBattleEnergyBomber217Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Energy Bomber requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Energy Bomber requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Energy Bomber requires its current actor skeleton.");

        void Publish(NativeBattleEnergyBomber217Frame current)
        {
            NativeEffectMemory = current.Memory with { EnergyBomber217 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Energy Bomber lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEnergyBomber217Frame ObserveBone(NativeBattleEnergyBomber217Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleEnergyBomber217PointStep Bone(NativeBattleEnergyBomber217Frame current, int actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleEnergyBomber217AnchorStep Anchor(NativeBattleEnergyBomber217Frame current, int actor)
        {
            if ((graphics.Models[actor].Flags0 & 2) != 0)
            {
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    current = ObserveBone(current, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0));
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("Energy Bomber requires its actual anchor WORD owner.")).Angle36 });
        }

        NativeBattleEnergyBomber217PointStep Blend(NativeBattleEnergyBomber217Frame current, NativeBattleVector first, NativeBattleVector second, short firstFactor, short secondFactor)
        {
            NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(current.Memory, first, second, firstFactor, secondFactor, colors);
            return new(current with { Memory = blended.Memory }, blended.Position);
        }

        NativeBattleEnergyBomber217ActStep AdvanceAct(NativeBattleEnergyBomber217Frame current, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = new(NativeBattleEnergyBomber217Controller.Read(current, address).ReadBytes(offset, size).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleAnchorDepthAct16.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("EnergyBomber217 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleEnergyBomber217Profile.WorkingGlobal) + NativeBattleEnergyBomber217Profile.MorphOffset);
                NativeBattleAnchorDepthAct16Step drawn = NativeBattleAnchorDepthAct16.Draw(current.Memory, source, node, context, Camera().CapturePackedView1D97778(), temporary, Camera().Projection ?? throw new NotSupportedException("EnergyBomber217 requires its current projection."), colors, morph, NativeBattleEnergyBomber217Profile.MorphBytes);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("EnergyBomber217 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleEnergyBomber217Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEnergyBomber217Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleEnergyBomber217Controller.Write(current, address, NativeBattleEnergyBomber217Controller.Read(current, address).WriteBytes(offset, advanced.Workspace.Data.AsSpan()));
            return new(current, advanced.Remaining);
        }

        NativeBattleEnergyBomber217Frame Deliver(NativeBattleEnergyBomber217Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Energy Bomber requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleEnergyBomber217Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEnergyBomber217Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEnergyBomber217Frame Execute(NativeBattleEnergyBomber217Frame current, NativeBattleEnergyBomber217Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEnergyBomber217Stage stage:
                    NativeBattleStageState previous = NativeStage ?? throw new NotSupportedException("Energy Bomber requires its current stage groups.");
                    NativeStage = previous with { Groups = previous.Groups.Select(group => group with { ColorDepth = stage.Depth, ColorFlags = stage.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEnergyBomber217DrawTrail120 trail:
                    NativeBattleEnergyBomber217TrailStep drawnTrail = NativeBattleEnergyBomber217Trail.Draw(current.Memory, current.Staging ?? throw new NotSupportedException("Energy Bomber requires its actual trail temporary memory."), trail.Temporary120, trail.Position, resources.TrailMesh, Camera().CapturePackedView1D97778(), Projection(), colors, trail.Cue);
                    return Deliver(current with { Memory = drawnTrail.Memory, Staging = drawnTrail.Staging }, drawnTrail.Batch);
                case NativeBattleEnergyBomber217Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Energy Bomber sound lost its process memory.") };
                case NativeBattleEnergyBomber217Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(contact.Index)) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Energy Bomber contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleEnergyBomber217DrawSprite180 sprite:
                    NativeBattleModulePoolTask task = NativeBattleEnergyBomber217Controller.Read(current, sprite.TaskAddress);
                    NativeBattleVector position = new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)));
                    short phase = unchecked((short)task.ReadWord(18));
                    NativeBattleEffectMemory prepared = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, Camera().CapturePackedView1D97778(), position, phase, 2048, -512);
                    NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(current.Staging!, sprite.Temporary180, resources.Sprite.Address, 0).Write(sprite.Temporary180 + 4, BitConverter.GetBytes(phase));
                    NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, sprite.Temporary180, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
                    NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(prepared, temporary, address => address == resources.Sprite.Address ? resources.Sprite : throw new NotSupportedException("Energy Bomber selected an unsupported sprite source."), Projection(), Camera().Roll);
                    return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, sprite.Temporary180, drawn.Temporary180, 180) }, drawn.Batch);
                default:
                    throw new NotSupportedException("Energy Bomber operation has no proved implementation.");
            }
        }
    }
}
