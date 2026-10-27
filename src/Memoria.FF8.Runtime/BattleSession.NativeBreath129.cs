using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBreath129(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Breath requires its selected original effect.");
        NativeBattleBreath129Resources resources = effect.Binding.Breath129Resources ?? throw new NotSupportedException("Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Breath requires its actual process memory.");
        NativeBattleBreath129Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Breath requires its actual CRT stream."), memory.Breath129 ?? throw new NotSupportedException("Breath requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Breath requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Breath requires its interpolation constants.");
        NativeBattleBreath129Inputs? inputs = null;
        inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Breath requires its current projection."), (_, address) => graphics.Models[Actor(address)], (_, address) => (graphics.Models[Actor(address)].Transform ?? throw new NotSupportedException("Breath requires its actor heading.")).Angle36, Bone, Anchor, ReadBody, PublishBody, AdvanceAct, Execute);
        frame = initialize ? NativeBattleBreath129Controller.Initialize(frame, resources, inputs) : NativeBattleBreath129Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Breath requires its actual camera.");

        void Publish(NativeBattleBreath129Frame current)
        {
            NativeEffectMemory = current.Memory with { Breath129 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Breath lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBreath129Frame Import(NativeBattleBreath129Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Breath external service lost its process memory.");
            return current with { Memory = latest, State = latest.Breath129 ?? throw new InvalidDataException("Breath external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Breath external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Breath external service lost its CRT stream.") };
        }

        NativeBattleBreath129PointStep Bone(NativeBattleBreath129Frame current, uint address, int selector, short distance)
        {
            int actor = Actor(address);
            return BoneActor(current, actor, selector, distance);
        }

        NativeBattleBreath129PointStep BoneActor(NativeBattleBreath129Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Breath requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Breath bone query requires its actual temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleBreath129PointStep Anchor(NativeBattleBreath129Frame current, uint address)
        {
            int actor = Actor(address);
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

        NativeBattleBreath129Frame Deliver(NativeBattleBreath129Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Breath requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleBreath129Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBreath129Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleBreath129Frame Execute(NativeBattleBreath129Frame current, NativeBattleBreath129Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBreath129Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleBreath129ContactIndex contact:
                    Publish(current);
                    int headerIndex = (current.Effect.Parameters ?? throw new NotSupportedException("Breath requires its captured parameters.")).HeaderIndex7C;
                    int first = nativeActionPackets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Breath requires its captured contacts.");
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first + contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleBreath129ComposeBody5095B0 compose:
                    int actor = Actor(compose.ActorAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Breath requires its current world matrix."))) };
                    return current;
                case NativeBattleBreath129Stage5713E0 stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Breath requires its retained stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Accumulator), ColorFlags = unchecked((uint)stage.Mode) }).ToImmutableArray() };
                    return current;
                case NativeBattleBreath129DeliverBatch batch:
                    return Deliver(current, batch.Batch);
                case NativeBattleBreath129AppendPacket packet:
                    return AppendPacket(current, packet);
                case NativeBattleBreath129DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress);
                case NativeBattleBreath129DrawDefaultMesh88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress);
                default:
                    throw new NotSupportedException("Breath emitted an unsupported native operation.");
            }
        }

        NativeBattleBreath129Frame DrawSprite(NativeBattleBreath129Frame current, uint address, uint sourceAddress)
        {
            NativeBattleModuleResource resource = resources.Sprite;
            if (sourceAddress != resource.Address)
            {
                throw new InvalidDataException("Breath sprite changed its original source.");
            }
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Breath sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Breath sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Breath sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Breath sprite requires its projection."), Camera().Roll);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) }, drawn.Batch);
        }

        NativeBattleBreath129Frame DrawMesh(NativeBattleBreath129Frame current, uint address, uint sourceAddress)
        {
            NativeBattleGfAnimationSource act = new[] { resources.FirstAct, resources.SecondAct, resources.ThirdAct, resources.FourthAct }.Single(source => sourceAddress >= source.Address && sourceAddress < source.Address + source.Data.Length);
            System.Collections.Immutable.ImmutableArray<byte> meshBytes = act.Data.AsSpan().Slice(checked((int)(sourceAddress - act.Address))).ToArray().ToImmutableArray();
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Breath mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath mesh requires its current geometry registers.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Breath mesh requires its current primitive process.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Breath mesh requires its current projection controls.");
            short? padding = geometry.Rotation20[18] is byte low && geometry.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null;
            NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(meshBytes, sourceAddress, NativeBattleGeometry.CaptureMatrix(geometry), Camera().Projection ?? throw new NotSupportedException("Breath mesh requires its projection."), temporary, new(geometry, current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Breath mesh requires its current arithmetic registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Breath mesh requires its current far-color controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors, matrixPadding18: padding, zsf3: controls.Zsf3, selectedVerticesAddress: BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(4, 4)), readSelectedVertex: ReadVertex);
            current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(meshBytes.AsSpan().Slice(4));
                if (offset < 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Breath ACT face leaves its actual selected vertex bank.");
                }
                uint selected = checked(BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(4, 4)) + (uint)offset);
                NativeBattleEffectArena vertex = selected >= act.Address && (ulong)selected + 8 <= (ulong)act.Address + (uint)act.Data.Length ? NativeBattleEffectArena.Clear().Write(0, resources.Read(selected, 8).AsSpan()) : NativeBattleBreath129Controller.ReadBytes(current, selected, 8);
                byte[] xyz = vertex.Read(0, 6);
                (byte[] w, bool[] known) = vertex.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        static int Actor(uint address)
        {
            uint relative = unchecked(address - 0x1D972C0);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Breath actor pointer leaves its model bank.");
            }
            return (int)(relative / 156);
        }

        NativeBattleBreath129BodyStep ReadBody(NativeBattleBreath129Frame current, uint row)
        {
            var result = ReadNativeBreath129Body(current, graphics, row, false);
            graphics = result.Graphics;
            return new(result.Frame, result.Source);
        }

        NativeBattleBreath129BodyStep PublishBody(NativeBattleBreath129Frame current, uint row)
        {
            var result = ReadNativeBreath129Body(current, graphics, row, true);
            graphics = result.Graphics;
            return new(result.Frame, result.Source);
        }

        NativeBattleBreath129ActStep AdvanceAct(NativeBattleBreath129Frame current, uint address, NativeBattleGfAnimationSource source, NativeBattleBreath129ActContext context)
        {
            int size = source.Address switch
            {
                NativeBattleBreath129Profile.FirstActAddress => 0x108,
                NativeBattleBreath129Profile.SecondActAddress => 0x110,
                NativeBattleBreath129Profile.ThirdActAddress => 0x68,
                NativeBattleBreath129Profile.FourthActAddress => 0x188,
                _ => throw new NotSupportedException("Breath selected an unsupported ACT workspace.")
            };
            NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(current.Memory, current.Effect.Pools, address, size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                current = NativeBattleBreath129Act.Draw6B57B0(current, node, source, context, inputs ?? throw new InvalidDataException("Breath lost its initialized services."));
                return next;
            });
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(current.Memory, current.Effect.Pools, address, advanced.Workspace);
            return new(current, advanced.Remaining);
        }

        NativeBattleBreath129Frame AppendPacket(NativeBattleBreath129Frame current, NativeBattleBreath129AppendPacket packet)
        {
            if (packet.Allocation < packet.Written || packet.Written < 4 || packet.Data.Length != packet.Known.Length || packet.Data.Length > packet.Allocation || packet.WriteMask.Length != packet.Data.Length || (packet.Allocation & 3) != 0 || (packet.Written & 3) != 0)
            {
                throw new InvalidDataException("Breath packet requires its aligned reservation and explicit physical writes.");
            }
            byte[] bytes = new byte[packet.Allocation];
            packet.Data.CopyTo(bytes, 0);
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, packet.Allocation).ToArray();
            packet.WriteMask.CopyTo(mask, 0);
            for (int index = 0; index < packet.Data.Length; index++)
            {
                if (mask[index] == NativeBattleGfPacketByteState.Known && !packet.Known[index])
                {
                    throw new InvalidDataException("Breath packet claims an unresolved write is known.");
                }
            }
            ImmutableArray<uint> words = Enumerable.Range(0, packet.Written / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive? polygon = packet.Insert ? new(packet.Bucket, words, packet.VertexPayload4) : null;
            NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), polygon)], packet.Insert ? packet.Allocation : 0);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Breath requires its pending ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(packet.Address - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch, packet.Flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory };
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
