using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSaliva138(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Saliva requires its selected original effect.");
        NativeBattleSaliva138Resources resources = effect.Binding.Saliva138Resources ?? throw new NotSupportedException("Saliva requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Saliva requires its actual process memory.");
        NativeBattleSaliva138Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Saliva requires its CRT stream."), memory.Saliva138 ?? throw new NotSupportedException("Saliva requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Saliva requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Saliva requires its original interpolation constants.");
        NativeBattleSaliva138Inputs inputs = new(nativeActionPackets, colors, Bone, Mouth, ModelYaw, _ => Camera().CapturePackedView1D97778(), _ => Projection(), AdvanceAct, Execute);
        frame = initialize ? NativeBattleSaliva138Controller.Initialize(frame, resources, inputs) : NativeBattleSaliva138Controller.Advance(frame, resources, inputs);
        if (!initialize && frame.Effect.ReturnedDescriptor == 0)
        {
            frame = NativeBattleSaliva138Controller.ReleaseReturnedBanks(frame);
        }
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Saliva requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Saliva requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Saliva requires its current actor skeleton.");

        void Publish(NativeBattleSaliva138Frame current)
        {
            NativeEffectMemory = current.Memory with { Saliva138 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Saliva lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleSaliva138Frame ObserveBone(NativeBattleSaliva138Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        int Actor(uint pointer)
        {
            long offset = (long)pointer - 0x1D972C0;
            if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Saliva requires its actual current model pointer.");
            }
            return checked((int)(offset / 156));
        }

        short ModelYaw(uint pointer) => graphics.Models[Actor(pointer)].Rotation14 ?? throw new NotSupportedException("Saliva requires its actual current model rotation WORD14.");

        NativeBattleSaliva138PointStep Bone(NativeBattleSaliva138Frame current, uint pointer, byte selector, short distance)
        {
            int actor = Actor(pointer);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleSaliva138PointStep Mouth(NativeBattleSaliva138Frame current)
        {
            int caster = Actor(current.State.ReadDword(NativeBattleSaliva138Profile.CasterGlobal));
            var prepared = NativeBattleSaliva138Mouth.Prepare(current.Memory, graphics.Models[caster], PoseSource(caster), resources.BoneAnchor);
            return new(current with { Memory = prepared.Memory }, prepared.Position);
        }

        NativeBattleSaliva138ActStep AdvanceAct(NativeBattleSaliva138Frame current, uint address, NativeBattleGfAnimationSource source, int offset, int size, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(NativeBattleSaliva138Controller.Read(current, address), offset, size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleDefaultActContext40.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Saliva138 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleSaliva138Profile.WorkingGlobal) + NativeBattleSaliva138Profile.MorphOffset);
                NativeBattleDefaultActContext40Step drawn = NativeBattleDefaultActContext40.Draw(current.Memory, source, node, context, temporary, Projection(), colors, morph, NativeBattleActContext40Policy.Saliva138);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Saliva138 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleSaliva138Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleSaliva138Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleSaliva138Controller.Write(current, address, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleSaliva138Controller.Read(current, address), offset, advanced.Workspace));
            return new(current, advanced.Remaining);
        }

        NativeBattleSaliva138Frame Deliver(NativeBattleSaliva138Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Saliva requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleSaliva138Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleSaliva138Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffectArena ReadTemporary(NativeBattleSaliva138Frame current, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Saliva requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        NativeBattleSaliva138Frame Execute(NativeBattleSaliva138Frame current, NativeBattleSaliva138Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSaliva138DeliverRibbonBatch ribbon:
                    return Deliver(current, ribbon.Batch);
                case NativeBattleSaliva138DrawCustom92 mesh:
                    NativeBattleEffectArena context = ReadTemporary(current, mesh.TemporaryAddress, 92);
                    NativeBattleStaticMeshResource source = resources.InitialMesh with { Bytes = current.State.ReadMesh() };
                    NativeBattleWindowedMeshStep drawn = NativeBattleWindowedMesh.Draw(current.Memory, source, context, Projection(), NativeBattleWindowedMeshLayout.Saliva138, ReadSelectedVertex);
                    return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, mesh.TemporaryAddress, drawn.Context, 92) }, drawn.Batch);

                    (NativeBattleVector Vertex, short? W) ReadSelectedVertex(uint address)
                    {
                        uint start = checked(current.State.ReadDword(NativeBattleSaliva138Profile.WorkingGlobal) + NativeBattleSaliva138Profile.MorphOffset);
                        long selected = (long)address - start;
                        if (selected < 0 || (selected & 7) != 0 || selected > NativeBattleSaliva138Profile.MorphBytes - 8)
                        {
                            throw new InvalidDataException("Saliva custom mesh leaves its actual selected vertex bank.");
                        }
                        int offset = checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Saliva requires its current selected vertex owner.");
                        byte[] xyz = arena.Read(offset, 6);
                        (byte[] w, bool[] known) = arena.ReadSnapshot(offset + 6, 2);
                        return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
                    }
                case NativeBattleSaliva138Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Saliva sound lost its process memory.") };
                case NativeBattleSaliva138Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(checked((nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Saliva requires its current first contact.")) + contact.Index))) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Saliva contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleSaliva138DrawSprite180 sprite:
                    NativeBattleModuleResource spriteSource = resources.Sprite(sprite.SourceAddress);
                    NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, ReadTemporary(current, sprite.TemporaryAddress, 180), address => address == spriteSource.Address ? spriteSource : throw new NotSupportedException("Saliva selected an unsupported sprite source."), Projection(), Camera().Roll, 17);
                    return Deliver(current with { Memory = spriteDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, sprite.TemporaryAddress, spriteDraw.Temporary180, 180) }, spriteDraw.Batch);
                default:
                    throw new NotSupportedException("Saliva operation has no proved implementation.");
            }
        }
    }
}
