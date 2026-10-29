using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSandStorm155(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Sand Storm requires its selected original effect.");
        NativeBattleSandStorm155Resources resources = effect.Binding.SandStorm155Resources ?? throw new NotSupportedException("Sand Storm requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Sand Storm requires its actual process memory.");
        NativeBattleSandStorm155Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Sand Storm requires its actual CRT stream."), memory.SandStorm155 ?? throw new NotSupportedException("Sand Storm requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Sand Storm requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Sand Storm requires its interpolation constants.");
        NativeBattleSandStorm155Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Sand Storm requires its current projection."), (_, actor) => graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Sand Storm requires its actual retained model world matrix."), Anchor, TargetCenter, Execute);
        frame = initialize ? NativeBattleSandStorm155Controller.Initialize(frame, resources, inputs) : NativeBattleSandStorm155Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Sand Storm requires its actual camera.");

        void Publish(NativeBattleSandStorm155Frame current)
        {
            NativeEffectMemory = current.Memory with { SandStorm155 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Sand Storm lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleSandStorm155Frame Import(NativeBattleSandStorm155Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Sand Storm external service lost its process memory.");
            return current with { Memory = latest, State = latest.SandStorm155 ?? throw new InvalidDataException("Sand Storm external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Sand Storm external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Sand Storm external service lost its CRT stream.") };
        }

        NativeBattleSandStorm155AnchorStep Anchor(NativeBattleSandStorm155Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Sand Storm requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Sand Storm requires its actual actor heading.")).Angle36 });
        }

        NativeBattleSandStorm155PointStep TargetCenter(NativeBattleSandStorm155Frame current, ushort mask)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Sand Storm requires every selected physical target position.")).ToArray());
            return new(current, center);
        }

        NativeBattleSandStorm155Frame Deliver(NativeBattleSandStorm155Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Sand Storm requires its pending ordering table.");
            int cursor = checked((int)(current.State.ReadDword(NativeBattleSandStorm155Profile.CursorGlobal) - NativeBattleSandStorm155Profile.PacketBase));
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(current.State.Packets, NativeBattleSandStorm155Profile.PacketBankId, cursor, NativeBattleSandStorm155Profile.PacketLength, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = (current.State with { Packets = written.Arena }).WriteDword(NativeBattleSandStorm155Profile.CursorGlobal, checked(NativeBattleSandStorm155Profile.PacketBase + (uint)written.Cursor)) };
        }

        NativeBattleSandStorm155Frame Execute(NativeBattleSandStorm155Frame current, NativeBattleSandStorm155Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSandStorm155Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleSandStorm155ContactsAll contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleSandStorm155DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.PilotIdentity);
                case NativeBattleSandStorm155DrawMesh88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.PilotIdentity);
                default:
                    throw new NotSupportedException("Sand Storm emitted an unsupported native operation.");
            }
        }

        NativeBattleSandStorm155Frame DrawSprite(NativeBattleSandStorm155Frame current, uint address, uint sourceAddress, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleModuleResource resource = sourceAddress == resources.SpriteA.Address ? resources.SpriteA : sourceAddress == resources.SpriteB.Address ? resources.SpriteB : throw new NotSupportedException("Sand Storm selected an unsupported original sprite.");
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Sand Storm sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Sand Storm sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Sand Storm sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Sand Storm sprite requires its projection."), Camera().Roll, pilotIdentity: pilotIdentity);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) }, drawn.Batch);
        }

        NativeBattleSandStorm155Frame DrawMesh(NativeBattleSandStorm155Frame current, uint address, uint sourceAddress, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleStaticMeshResource mesh = resources.Mesh;
            if (sourceAddress != mesh.Address)
            {
                throw new NotSupportedException("Sand Storm selected an unsupported original mesh.");
            }
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Sand Storm mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Sand Storm mesh requires its current geometry registers.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Sand Storm mesh requires its current primitive process.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Sand Storm mesh requires its current projection controls.");
            short? padding = geometry.Rotation20[18] is byte low && geometry.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null;
            NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(mesh.Bytes, mesh.Address, NativeBattleGeometry.CaptureMatrix(geometry), Camera().Projection ?? throw new NotSupportedException("Sand Storm mesh requires its projection."), temporary, new(geometry, current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Sand Storm mesh requires its current arithmetic registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Sand Storm mesh requires its current far-color controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors, matrixPadding18: padding, zsf3: controls.Zsf3, selectedVerticesAddress: NativeBattleSandStorm155Profile.SelectedVerticesAddress, readSelectedVertex: ReadVertex, pilotIdentity: pilotIdentity);
            current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                if (offset < 0 || offset > NativeBattleSandStorm155Profile.VertexCount * 8 - 8)
                {
                    throw new InvalidDataException("Sand Storm face leaves its actual selected vertex bank.");
                }
                NativeBattleEffectArena vertex = current.State.ReadView(checked(NativeBattleSandStorm155Profile.SelectedVerticesAddress + (uint)offset), 8);
                byte[] xyz = vertex.Read(0, 6);
                (byte[] w, bool[] known) = vertex.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
