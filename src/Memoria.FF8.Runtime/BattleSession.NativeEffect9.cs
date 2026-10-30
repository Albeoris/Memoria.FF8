using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeEffect9(NativeBattleGraphicsState graphics) => RunNativeEffect9(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeEffect9(NativeBattleGraphicsState graphics) => RunNativeEffect9(graphics, false);

    private NativeBattleGraphicsState RunNativeEffect9(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect9 requires its selected module.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEffect9Resources resources = effect.Binding.Effect9Resources ?? throw new NotSupportedException("Effect9 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect9 requires its current process memory.");
        NativeBattleEffect9Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect9 requires its current CRT state."), memory.Effect9 ?? throw new NotSupportedException("Effect9 requires its retained module state; explicitly migrate a needed older snapshot.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect9 requires its current physical temporary memory.") };
        if (initialize)
        {
            resources.Validate();
            frame = NativeBattleEffect9Controller.Initialize(memory, effect, frame.Random, nativeActionPackets, resources.Camera, frame.State) with { Staging = frame.Staging };
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleEffect9Inputs Inputs() => new(nativeActionPackets, _ => Camera().CapturePackedView1D97778(), Execute, AdvanceLeaf, resources.Sound);
        frame = NativeBattleEffect9Controller.Advance(frame, Inputs());
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect9 requires its actual camera owner.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect9 requires its current projection.");
        NativeBattlePoseSource Source(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect9 requires its actual selected body rig.");
        NativeBattleSummonColors Colors() => definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect9 requires its current interpolation constants.");

        void Publish(NativeBattleEffect9Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect9 = current.State };
            NativeFileStaging = current.Staging;
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect9Frame Observe(NativeBattleEffect9Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleEffect9NodeAnchor Anchor(NativeBattleEffect9Frame current, int actor)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], Source(actor), 0xF1, 0);
            current = Observe(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleEffect9Frame Append(NativeBattleEffect9Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect9 requires its pending ordering table.");
            if (common)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect9 requires its current common packet allocator.");
                ImmutableArray<NativeBattlePrimitiveReference> references = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch);
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process }, Effect = current.Effect with { PresentationPackets = references } };
            }
            uint cursor = current.State.ReadDword(0x274EFEC);
            NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - 0x20DFAB8)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = packets.Ordering } };
            return current with { Memory = packets.Memory, State = current.State.WriteDword(0x274EFEC, checked(0x20DFAB8u + (uint)packets.Cursor)), Effect = current.Effect with { DrawArenaOffset = packets.Cursor, PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, checked((int)(cursor - 0x20DFAB8)), batch) } };
        }

        NativeBattleEffect9Step AdvanceLeaf(NativeBattleEffect9Frame current, uint address)
        {
            uint callback = NativeBattleEffect9Controller.Read(current, address).Callback;
            switch (callback)
            {
                case 0x8C0BC0:
                case 0x8C3C30:
                    return NativeBattleEffect9Nodes.Advance(current, address, new(resources.ReadSource, Anchor, Camera().CapturePackedView1D97778(), Projection(), Colors(), (value, batch) => Append(value, batch, true)));
                case 0x8C32D0:
                case 0x8C33C0:
                case 0x8C3490:
                    return NativeBattleEffect9Scene.Advance(current, address, Inputs());
                case 0x8C3D40:
                case 0x8C3E00:
                    return NativeBattleEffect9Particles.Advance(current, address, new(nativeActionPackets, resources.BirthSchedule, resources.Particle, _ => Camera().CapturePackedView1D97778(), _ => Projection(), _ => Camera().Roll, (value, batch) => Append(value, batch, true)));
                case 0x8DDC30:
                    NativeBattleFullscreenOverlayStep overlay = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, graphics.PrimitiveFrame!.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Effect9 overlay requires its actual pause word."), resources.Overlay, NativeBattlePilotVisualLifetimes.Identity(9, NativeBattleEffect9Controller.Read(current, address)) with { ResourceGeneration = resources.Overlay.Address });
                    if (!ReferenceEquals(overlay.Frame.Ordering, graphics.PrimitiveFrame.Pending))
                    {
                        current = current with { Effect = current.Effect with { PresentationPackets = current.Effect.PresentationPackets.AddRange(new[] { 0, 24, 48, 72, 96 }.Select(offset => new NativeBattlePrimitiveReference(NativeBattleFullscreenOverlay.PacketBank, 0, offset))) } };
                    }
                    graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = overlay.Frame.Ordering } };
                    return new(current with { Memory = overlay.Frame.Memory, Effect = current.Effect with { Pools = overlay.Frame.Pools } }, overlay.Result);
                default:
                    throw new NotSupportedException($"Effect9 callback{callback:X} has no current implementation.");
            }
        }

        NativeBattleEffect9Frame Execute(NativeBattleEffect9Frame current, NativeBattleEffect9Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect9UpdateCaster caster:
                    NativeBattleModulePoolTask root = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect9Controller.Read(current, caster.Address), current.Effect.Parameters!, graphics.Models, Source, producer => current = Observe(current, producer));
                    return NativeBattleEffect9Controller.Write(current, caster.Address, root);
                case NativeBattleEffect9UpdateTarget target:
                    NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect9Controller.Read(current, target.Address), graphics.Models, Source, producer => current = Observe(current, producer));
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = Source(actor);
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState!, source));
                    return NativeBattleEffect9Controller.Write(current, target.Address, task);
                case NativeBattleEffect9InitializeControl control:
                    NativeBattleGraphicsModel model = graphics.Models[NativeBattleEffect9Controller.Read(current, control.Address).ReadByte(0x2D)];
                    NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Effect9 requires its current target position.")).Position28;
                    uint xy = unchecked((ushort)position.X) | unchecked((uint)(ushort)position.Y << 16);
                    uint zw = unchecked((ushort)position.Z) | unchecked((uint)(ushort)model.Transform.Vertical34 << 16);
                    return current with { State = NativeBattleEffect9Controller.InitializeControl(current.State, xy, zw) };
                case NativeBattleEffect9Contact contact:
                    NativeBattleModulePoolTask contactTask = NativeBattleEffect9Controller.Read(current, contact.Address);
                    NativeBattleEffectParameters parameters = current.Effect.Parameters!;
                    int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)contactTask.ReadByte(0x2A)));
                    int contactIndex = checked((nativeActionPackets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Effect9 contact requires its physical header pointer.")) + unchecked((sbyte)contactTask.ReadByte(0x2B)));
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory!, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect9 lost its current CRT owner."), Staging = NativeFileStaging };
                case NativeBattleEffect9Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory!, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect9 lost its current CRT owner."), Staging = NativeFileStaging };
                case NativeBattleEffect9TextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.Source, checked(load.Address + 0x62), (address, value) => current = current with { Effect = WriteEffect9Completion(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect9StageColor stageColor:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect9 requires its four current stage color rows.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = stageColor.Depth, ColorFlags = stageColor.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect9CaptureBody capture:
                    NativeBattleModulePoolTask body = NativeBattleEffect9Controller.Read(current, capture.Address);
                    int targetActor = body.ReadByte(0x2D);
                    NativeBattleGraphicsModel targetModel = graphics.Models[targetActor];
                    current = NativeBattleEffect9Controller.Write(current, capture.Address, body.WriteDword(0x74, targetModel.Flags0));
                    graphics = graphics with { Models = graphics.Models.SetItem(targetActor, targetModel with { Flags0 = (ushort)(targetModel.Flags0 | 8) }) };
                    NativeBattleEffect9NodeAnchor anchor = Anchor(current, targetActor);
                    byte[] vector = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(vector, anchor.Point.X);
                    BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(2), anchor.Point.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(4), anchor.Point.Z);
                    return anchor.Frame with { State = anchor.Frame.State.Write(0x27571B8, vector) };
                case NativeBattleEffect9SetupBody setup:
                    int setupActor = NativeBattleEffect9Controller.Read(current, setup.Address).ReadByte(0x2D);
                    NativeBattleGraphicsModel setupModel = graphics.Models[setupActor];
                    NativeBattleEffectLocalBounds bounds = setupModel.GeometryBounds52 ?? throw new NotSupportedException("Effect9 requires its actual model bounds.");
                    short currentY = (setupModel.Transform ?? throw new NotSupportedException("Effect9 requires its actual model position.")).Position28.Y;
                    return current with { State = NativeBattleEffect9Scene.InitializeBody(current.State, checked(0x1D972C0u + (uint)(setupActor * 156)), bounds.Minimum.Y, bounds.Maximum.Y, currentY) };
                case NativeBattleEffect9RestoreBody restore:
                    NativeBattleGraphicsModel restored = graphics.Models[restore.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(restore.Actor, restored with { Flags0 = (ushort)(restored.Flags0 ^ ((restored.Flags0 ^ restore.SavedFlags) & 8)) }) };
                    return current;
                case NativeBattleEffect9DrawBody draw:
                    return DrawBody(current, draw.Address);
                default:
                    throw new NotSupportedException("Effect9 operation requires its current native service.");
            }
        }

        NativeBattleEffect9Frame DrawBody(NativeBattleEffect9Frame current, uint address)
        {
            NativeBattleModulePoolTask bodyTask = NativeBattleEffect9Controller.Read(current, address);
            int actor = bodyTask.ReadByte(0x2D);
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect9 body requires its current rig.");
            NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actor], source);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            NativeBattlePilotVisualIdentity visualIdentity = NativeBattlePilotVisualLifetimes.Identity(9, bodyTask) with { ResourceGeneration = model.ModelId4 ?? throw new NotSupportedException("Effect9 body requires its selected resource identity.") };
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect9 body requires its retained world matrix.");
            world = world with { Y = unchecked(world.Y + (short)current.State.ReadWord(0x274F044)) };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(Camera().CapturePackedView1D97778().ReadMatrix(), world);
            current = WriteMatrix(current, 0x274EFF0, world);
            current = WriteMatrix(current, 0x274F010, composed);
            if ((model.Flags0 & 0x20) == 0)
            {
                NativeBattleShadowSource shadow = definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect9 body shadow requires its current source.");
                bool gouraud = definition.NativeGraphics!.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect9 body shadow requires its current device capability.");
                NativeBattleShadowGeometry geometry = NativeBattlePoseMatrices.ShadowGeometry5088A0(model, model.GeometryBounds52!, Projection().View!, shadow, gouraud);
                NativeBattleModuleProjectedVertex[] points = geometry.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(geometry.CameraMatrix, vertex, Projection())).ToArray();
                current = Append(current, NativeBattleShadowPackets.EmitBatch5088A0(points, model.Color44 ?? throw new NotSupportedException("Effect9 body shadow requires its current model color."), gouraud, 4112, 16, geometry.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(geometry.CameraMatrix, vertex), Projection())).ToArray(), visualIdentity with { Primitive = -1 }), false);
            }
            uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect9 body requires its current mesh mask.");
            NativeBattleEffect9BodyDraw body = NativeBattleEffect9Body.Draw(current, NativeBattleCureActorAssembly.SelectGeometry8D7110(model, resource, mask), NativeBattleBodyBoneMatrices.Require(model, source), composed, Projection(), unchecked((short)current.State.ReadWord(0x274F048)), unchecked((short)current.State.ReadWord(0x274F04A)), unchecked((short)current.State.ReadWord(0x274F046)), mask, visualIdentity);
            current = Append(body.Frame, body.Batch, false);
            model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            return current;
        }
    }

    private static NativeBattleEffect9Frame WriteMatrix(NativeBattleEffect9Frame frame, uint address, NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, packed.Bytes.AsSpan(0, 18)).Write(20, packed.Bytes.AsSpan(20, 12));
        return frame with { State = frame.State with { Globals = NativeBattleEffectNodeContextMemory.Copy(view, 0, frame.State.Globals, checked((int)(address - NativeBattleEffect9State.BaseAddress)), 32) } };
    }

    private static NativeBattleOriginalEffectState WriteEffect9Completion(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - NativeBattleEffect9State.Pools[0].Bank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            throw new InvalidDataException("Effect9 completion requires its actual root byte.");
        }
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        uint taskAddress = address - 0x62;
        return effect with { Pools = hierarchy.Write(taskAddress, hierarchy.Read(taskAddress).WriteByte(0x62, value)).Pools };
    }

    private bool TryWriteNativeEffect9Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleEffect9State.Pools[0].Bank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleEffect9Controller.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteEffect9Completion(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleEffect9Controller.Entry } current ? current : throw new NotSupportedException("Effect9 completion lost its actual root.");
            updated = graphics with { OriginalEffect = WriteEffect9Completion(effect, address, value) };
        }
        return true;
    }
}
