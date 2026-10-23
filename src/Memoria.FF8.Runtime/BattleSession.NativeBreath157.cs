using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBreath157(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Breath requires its selected original effect.");
        NativeBattleBreath157Resources resources = effect.Binding.Breath157Resources ?? throw new NotSupportedException("Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Breath requires its actual process memory.");
        NativeBattleBreath157Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Breath requires its actual CRT stream."), memory.Breath157 ?? throw new NotSupportedException("Breath requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Breath requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Breath requires its interpolation constants.");
        NativeBattleBreath157Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Breath requires its current projection."), (_, actor) => graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Breath requires its actual retained model world matrix."), Bone, Anchor, Execute);
        frame = initialize ? NativeBattleBreath157Controller.Initialize(frame, resources, inputs) : NativeBattleBreath157Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Breath requires its actual camera.");

        void Publish(NativeBattleBreath157Frame current)
        {
            NativeEffectMemory = current.Memory with { Breath157 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Breath lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBreath157Frame Import(NativeBattleBreath157Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Breath external service lost its process memory.");
            return current with { Memory = latest, State = latest.Breath157 ?? throw new InvalidDataException("Breath external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Breath external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Breath external service lost its CRT stream.") };
        }

        NativeBattleBreath157PointStep Bone(NativeBattleBreath157Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Breath requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Breath bone query requires its actual temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleBreath157PointStep Anchor(NativeBattleBreath157Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Breath requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            short heading = (model.Transform ?? throw new NotSupportedException("Breath requires its actual actor heading.")).Angle36;
            return new(current, Point(new(anchor.X, anchor.Y, anchor.Z)).Write(6, BitConverter.GetBytes(heading)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleBreath157Frame Deliver(NativeBattleBreath157Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Breath requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleBreath157Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBreath157Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleBreath157Frame Execute(NativeBattleBreath157Frame current, NativeBattleBreath157Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBreath157Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleBreath157ContactFirst contact:
                    Publish(current);
                    int first = nativeActionPackets.Headers[contact.HeaderIndex].FirstContactIndex ?? throw new NotSupportedException("Breath requires its actual first captured contact.");
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleBreath157ReleaseCommon release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.SourceAddress, checked(release.TaskAddress + 14), (address, value) => current = current with { Effect = NativeBattleBreath157Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleBreath157DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress);
                case NativeBattleBreath157DrawMesh88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress);
                default:
                    throw new NotSupportedException("Breath emitted an unsupported native operation.");
            }
        }

        NativeBattleBreath157Frame DrawSprite(NativeBattleBreath157Frame current, uint address, uint sourceAddress)
        {
            NativeBattleModuleResource resource = resources.Sprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Breath sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Breath sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Breath sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Breath sprite requires its projection."), Camera().Roll);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) }, drawn.Batch);
        }

        NativeBattleBreath157Frame DrawMesh(NativeBattleBreath157Frame current, uint address, uint sourceAddress)
        {
            NativeBattleStaticMeshResource mesh = resources.Mesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Breath mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath mesh requires its current geometry registers.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Breath mesh requires its current primitive process.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Breath mesh requires its current projection controls.");
            short? padding = geometry.Rotation20[18] is byte low && geometry.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null;
            NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(mesh.Bytes, mesh.Address, NativeBattleGeometry.CaptureMatrix(geometry), Camera().Projection ?? throw new NotSupportedException("Breath mesh requires its projection."), temporary, new(geometry, current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Breath mesh requires its current arithmetic registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Breath mesh requires its current far-color controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors, matrixPadding18: padding, zsf3: controls.Zsf3);
            current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
