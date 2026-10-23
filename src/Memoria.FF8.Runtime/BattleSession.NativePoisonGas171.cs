using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativePoisonGas171(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Poison Gas171 requires its selected original effect.");
        NativeBattlePoisonGas171Resources resources = effect.Binding.PoisonGas171Resources ?? throw new NotSupportedException("Poison Gas171 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Poison Gas171 requires its actual process memory.");
        NativeBattlePoisonGas171Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Poison Gas171 requires its actual CRT stream."), memory.PoisonGas171 ?? throw new NotSupportedException("Poison Gas171 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Poison Gas171 requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Poison Gas171 requires its interpolation constants.");
        NativeBattlePoisonGas171Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Poison Gas171 requires its current projection."), _ => Camera().FocalLength ?? throw new NotSupportedException("Poison Gas171 requires its current scalar focal WORD."), (_, actor) => graphics.Models[actor], (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Poison Gas171 requires its current model world matrix."), null), Bone, Execute);
        frame = initialize ? NativeBattlePoisonGas171Controller.Initialize(frame, resources, inputs) : NativeBattlePoisonGas171Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Poison Gas171 requires its actual camera.");

        void Publish(NativeBattlePoisonGas171Frame current)
        {
            NativeEffectMemory = current.Memory with { PoisonGas171 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Poison Gas171 lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattlePoisonGas171Frame Import(NativeBattlePoisonGas171Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Poison Gas171 external service lost its process memory.");
            return current with { Memory = latest, State = latest.PoisonGas171 ?? throw new InvalidDataException("Poison Gas171 external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Poison Gas171 external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Poison Gas171 external service lost its CRT stream.") };
        }

        NativeBattlePoisonGas171PointStep Bone(NativeBattlePoisonGas171Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Poison Gas171 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Poison Gas171 bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattlePoisonGas171Frame Deliver(NativeBattlePoisonGas171Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Poison Gas171 requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattlePoisonGas171Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattlePoisonGas171Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattlePoisonGas171Frame Execute(NativeBattlePoisonGas171Frame current, NativeBattlePoisonGas171Operation operation)
        {
            switch (operation)
            {
                case NativeBattlePoisonGas171Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattlePoisonGas171Contacts506BA0 contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattlePoisonGas171ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = unchecked((ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattlePoisonGas171Stage5713E0 stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Poison Gas171 requires its retained stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Value), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattlePoisonGas171DrawBody176 body:
                    Publish(current);
                    var drawnBody = DrawNativePoisonGas171Body(current, graphics, checked((byte)body.Actor), body.TemporaryAddress);
                    graphics = drawnBody.Graphics;
                    return drawnBody.Frame;
                case NativeBattlePoisonGas171DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)));
                case NativeBattlePoisonGas171DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)));
                default:
                    throw new NotSupportedException("Poison Gas171 emitted an unsupported native operation.");
            }
        }

        NativeBattlePoisonGas171Frame DrawSprite(NativeBattlePoisonGas171Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Poison Gas171 sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Poison Gas171 sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Poison Gas171 sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Poison Gas171 sprite requires its projection."), Camera().Roll, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattlePoisonGas171Frame DrawMesh(NativeBattlePoisonGas171Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Poison Gas171 mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Poison Gas171 requires its current projection."), temporary, colors, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
