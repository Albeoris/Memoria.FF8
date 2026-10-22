using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMeltEye158(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Melt-Eye requires its selected original effect.");
        NativeBattleMeltEye158Resources resources = effect.Binding.MeltEye158Resources ?? throw new NotSupportedException("Melt-Eye requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Melt-Eye requires its actual process memory.");
        NativeBattleMeltEye158Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Melt-Eye requires its CRT stream."), memory.MeltEye158 ?? throw new NotSupportedException("Melt-Eye requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Melt-Eye requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Melt-Eye requires its original interpolation constants.");
        NativeBattleMeltEye158Inputs inputs = new(graphics.Flags, nativeActionPackets, Bone, TargetCenter, World, _ => Camera().CapturePackedView1D97778(), Execute);
        frame = initialize ? NativeBattleMeltEye158Controller.Initialize(frame, resources, inputs) : NativeBattleMeltEye158Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Melt-Eye requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Melt-Eye requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Melt-Eye requires its current actor skeleton.");

        void Publish(NativeBattleMeltEye158Frame current)
        {
            NativeEffectMemory = current.Memory with { MeltEye158 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Melt-Eye lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleMeltEye158Frame ObserveBone(NativeBattleMeltEye158Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleMeltEye158PointStep Bone(NativeBattleMeltEye158Frame current, int actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleMeltEye158PointStep TargetCenter(NativeBattleMeltEye158Frame current, ushort mask)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Melt-Eye requires each selected physical target position.")).ToArray());
            return new(current, center);
        }

        NativeBattlePackedMatrix32 World(NativeBattleMeltEye158Frame current, int actor)
        {
            return NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Melt-Eye requires its actual retained caster world matrix."), null);
        }

        NativeBattleMeltEye158Frame Deliver(NativeBattleMeltEye158Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Melt-Eye requires its current ordering table.");
            NativeBattleMeltEye158PacketWrite written = NativeBattleMeltEye158PacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleMeltEye158Frame Execute(NativeBattleMeltEye158Frame current, NativeBattleMeltEye158Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMeltEye158Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Melt-Eye sound lost its process memory.") };
                case NativeBattleMeltEye158BulkContact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contact.First, contact.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Melt-Eye contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleMeltEye158DrawStatic88 mesh:
                    NativeBattleFileStaging meshStaging = current.Staging ?? throw new NotSupportedException("Melt-Eye requires its actual mesh temporary memory.");
                    NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(meshStaging, mesh.Temporary88, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                    NativeBattleStaticMeshContext88Step drawnMesh = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, resources.Mesh(mesh.MeshAddress), Projection(), context, colors, depthShift: mesh.DepthShift);
                    return Deliver(current with { Memory = drawnMesh.Memory, Staging = NativeBattleTemporaryMemory.PublishView(meshStaging, mesh.Temporary88, drawnMesh.Context, 88) }, drawnMesh.Batch);
                case NativeBattleMeltEye158DrawSprite180 sprite:
                    NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Melt-Eye requires its actual sprite temporary memory.");
                    NativeBattleModuleResource source = resources.Sprite(sprite.SpriteAddress);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(sprite.Temporary180, 4)) != source.Address)
                    {
                        throw new InvalidDataException("Melt-Eye sprite source differs from its actual retained temporary header.");
                    }
                    NativeBattleEffectMemory prepared = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, Camera().CapturePackedView1D97778(), sprite.Position, sprite.W, sprite.Scale, sprite.Depth);
                    staging = staging.Write(sprite.Temporary180 + 4, BitConverter.GetBytes(sprite.Phase));
                    NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, sprite.Temporary180, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
                    NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(prepared, temporary, resources.Sprite, Projection(), Camera().Roll);
                    return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, sprite.Temporary180, drawn.Temporary180, 180) }, drawn.Batch);
                default:
                    throw new NotSupportedException("Melt-Eye operation has no proved implementation.");
            }
        }
    }
}
