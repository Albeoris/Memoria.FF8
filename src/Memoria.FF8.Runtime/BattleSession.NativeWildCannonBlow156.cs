using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeWildCannonBlow156(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Wild Cannon Blow requires its selected original effect.");
        NativeBattleWildCannonBlow156Resources resources = effect.Binding.WildCannonBlow156Resources ?? throw new NotSupportedException("Wild Cannon Blow requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Wild Cannon Blow requires its actual process memory.");
        NativeBattleWildCannonBlow156Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Wild Cannon Blow requires its actual CRT stream."), memory.WildCannonBlow156 ?? throw new NotSupportedException("Wild Cannon Blow requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Wild Cannon Blow requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Wild Cannon Blow requires its interpolation constants.");
        NativeBattleWildCannonBlow156Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Wild Cannon Blow requires its current projection."), (_, actor) => graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Wild Cannon Blow requires its actual retained model world matrix."), Bone, TargetCenter, Execute);
        frame = initialize ? NativeBattleWildCannonBlow156Controller.Initialize(frame, resources, inputs) : NativeBattleWildCannonBlow156Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Wild Cannon Blow requires its actual camera.");

        void Publish(NativeBattleWildCannonBlow156Frame current)
        {
            NativeEffectMemory = current.Memory with { WildCannonBlow156 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Wild Cannon Blow lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleWildCannonBlow156Frame Import(NativeBattleWildCannonBlow156Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Wild Cannon Blow external service lost its process memory.");
            return current with { Memory = latest, State = latest.WildCannonBlow156 ?? throw new InvalidDataException("Wild Cannon Blow external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Wild Cannon Blow external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Wild Cannon Blow external service lost its CRT stream.") };
        }

        NativeBattleWildCannonBlow156PointStep Bone(NativeBattleWildCannonBlow156Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Wild Cannon Blow requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Wild Cannon Blow bone query requires its actual temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleWildCannonBlow156PointStep TargetCenter(NativeBattleWildCannonBlow156Frame current, ushort mask)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Wild Cannon Blow requires every selected physical target position.")).ToArray());
            return new(current, Point(center));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleWildCannonBlow156Frame Deliver(NativeBattleWildCannonBlow156Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Wild Cannon Blow requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleWildCannonBlow156Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleWildCannonBlow156Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleWildCannonBlow156Frame Execute(NativeBattleWildCannonBlow156Frame current, NativeBattleWildCannonBlow156Operation operation)
        {
            switch (operation)
            {
                case NativeBattleWildCannonBlow156Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleWildCannonBlow156ContactsAll contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleWildCannonBlow156AdjustShake shake:
                    NativeCamera = Camera().AddViewTranslationY61B8D5(shake.Delta);
                    return current;
                case NativeBattleWildCannonBlow156ReleaseCommon release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.SourceAddress, checked(release.TaskAddress + 14), (address, value) => current = current with { Effect = NativeBattleWildCannonBlow156Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleWildCannonBlow156DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.PilotIdentity);
                case NativeBattleWildCannonBlow156DrawMesh88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.PilotIdentity);
                default:
                    throw new NotSupportedException("Wild Cannon Blow emitted an unsupported native operation.");
            }
        }

        NativeBattleWildCannonBlow156Frame DrawSprite(NativeBattleWildCannonBlow156Frame current, uint address, uint sourceAddress, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleModuleResource resource = resources.Sprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Wild Cannon Blow sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Wild Cannon Blow sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Wild Cannon Blow sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Wild Cannon Blow sprite requires its projection."), Camera().Roll, pilotIdentity: pilotIdentity);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) }, drawn.Batch);
        }

        NativeBattleWildCannonBlow156Frame DrawMesh(NativeBattleWildCannonBlow156Frame current, uint address, uint sourceAddress, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleStaticMeshResource mesh = resources.Mesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its current geometry registers.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its current primitive process.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its current projection controls.");
            short? padding = geometry.Rotation20[18] is byte low && geometry.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null;
            NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(mesh.Bytes, mesh.Address, NativeBattleGeometry.CaptureMatrix(geometry), Camera().Projection ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its projection."), temporary, new(geometry, current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its current arithmetic registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Wild Cannon Blow mesh requires its current far-color controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors, matrixPadding18: padding, zsf3: controls.Zsf3, pilotIdentity: pilotIdentity);
            current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
