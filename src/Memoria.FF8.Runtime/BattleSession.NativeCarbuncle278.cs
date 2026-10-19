using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeCarbuncle278(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleCarbuncle278Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Carbuncle requires its retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Carbuncle lost its selected effect."), nativeCrtRandom ?? throw new NotSupportedException("Native Carbuncle requires its actual CRT random owner."));
        if (initialize)
        {
            int encounter = definition.EncounterId ?? throw new NotSupportedException("Native Carbuncle requires its installed encounter WORD1CFF6E0.");
            frame = NativeBattleCarbuncle278Entry.Initialize680C50(frame, nativeActionPackets, graphics.Models, checked((ushort)encounter));
        }
        else
        {
            NativeBattleCarbuncle278Inputs inputs = new(nativeActionPackets, DrawMirror, StartVibration) { DrawCreature = DrawCreature, PresentCreature = PresentCreature, ModelCreature = ModelCreature, DrawSparkle = DrawSparkle, PresentSparkleWindow = PresentSparkleWindow, AdvanceProgramme = AdvanceProgramme, PresentProgramme = PresentProgramme, AdvanceRecipient = AdvanceRecipient, QueueFile = QueueFile, DrawRecipientChild = DrawRecipientChild, DrawRecipientRange = DrawRecipientRange, ApplyRecipientContact = ApplyRecipientContact, PresentReturn = PresentReturn, BeginFinalTexture = BeginFinalTexture, StopVibration = StopVibration, RestoreActors = RestoreActors };
            frame = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (depth, flags) =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained four stage groups.");
                stage.Validate();
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)depth), ColorFlags = unchecked((uint)flags) }).ToImmutableArray() };
            }, inputs);

            NativeBattleCarbuncle278Frame DrawMirror(NativeBattleCarbuncle278Frame current, NativeBattleVector plane, NativeBattleEffectLocalBounds bounds)
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Carbuncle mirror requires its current camera owner.");
                _ = NativeBattleCarbuncle278Mirror.Transform6812E0(plane, bounds, camera.FocalLength ?? throw new NotSupportedException("Native Carbuncle mirror requires its signed focal scratch WORD."), camera.Projection ?? throw new NotSupportedException("Native Carbuncle mirror requires its current screen offsets."));
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle mirror requires its current ordering table.");
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                int cursor = checked((int)(state.PacketCursor81BC - state.ModelBuffer81F8));
                NativeBattleCarbuncle278MirrorWrite written = NativeBattleCarbuncle278Mirror.Append6812E0(current.Memory, primitive.Pending, cursor, graphics.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = written.Memory with { Carbuncle278 = state with { PacketCursor81BC = checked(state.ModelBuffer81F8 + (uint)written.Cursor) } } };
            }

            NativeBattleCarbuncle278Vibration StartVibration(NativeBattleCarbuncle278Frame current)
            {
                NativeVibrationBank bank = current.Effect.Binding.Carbuncle278Resources?.Vibration ?? throw new NotSupportedException("Native Carbuncle requires its original vibration bank.");
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Carbuncle requires its retained vibration process.")).Enqueue(bank, 1, 128);
                NativeVibrationProcess = queued.State;
                return new(current, queued.Handle);
            }

            NativeBattleCarbuncle278Frame DrawCreature(NativeBattleCarbuncle278Frame current, ushort counter)
            {
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                byte stage = graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native Carbuncle mirror requires its current stage resource index.");
                if (stage is 0x2C or 0x2D or 0x2E)
                {
                    current = DrawMirror(current, state.MirrorPlane81F0 ?? throw new NotSupportedException("Native Carbuncle mirror requires its retained plane WORDs."), state.Bounds81E0 ?? throw new NotSupportedException("Native Carbuncle mirror requires its current bounds WORDs."));
                    state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                }
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its current projection.");
                NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its current view matrix.");
                NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, unchecked(state.Base81CC - 200), 4096);
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its current ordering table.");
                NativeBattleStaticMeshResource mesh = current.Effect.Binding.Carbuncle278Resources?.MirrorBoundsMesh ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its original geometry.");
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its actual COMMON packet owner.");
                byte intensity = unchecked((byte)((unchecked((int)(4096 - state.Flash81D4)) << 7) >> 12));
                Func<int, NativeBattleVector>? readVertices = null;
                int ratio = 4096;
                if (counter is >= 16 and < 36 or >= 247)
                {
                    NativeBattleCarbuncle278Resources resources = current.Effect.Binding.Carbuncle278Resources!;
                    if (resources.MirrorAlternateVertices.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Carbuncle mirror morph requires its original alternate vertex source.");
                    }
                    ratio = counter < 36 ? (counter - 16) * 4096 / 20 : Math.Max(0, 4096 - ((counter * 4096 - 0xF6FF9) >> 3));
                    current = current with { Memory = NativeBattleCarbuncle278Morph.Morph683CA0(current.Memory, mesh, resources.MirrorAlternateVertices, ratio) };
                    readVertices = offset =>
                    {
                        byte[] vertex = current.Memory.Arena!.Read(checked(0x2E84 + offset), 6);
                        return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(4)));
                    };
                }
                if (counter is >= 36 and < 247)
                {
                    System.Collections.Immutable.ImmutableArray<byte> vertices = current.Effect.Binding.Carbuncle278Resources!.MirrorAlternateVertices;
                    if (vertices.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Carbuncle mirror requires its original second vertex shape.");
                    }
                    readVertices = offset =>
                    {
                        ReadOnlySpan<byte> vertex = vertices.AsSpan().Slice(offset, 6);
                        return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
                    };
                }
                NativeBattleCommonPacketWrite written = NativeBattleCarbuncle278MirrorMesh.Append6839B0(process, primitive.Pending, mesh, matrix, projection, intensity, readVertices);
                if (counter >= 16)
                {
                    NativeBattleStaticMeshResource secondary = current.Effect.Binding.Carbuncle278Resources?.SecondaryMesh ?? throw new NotSupportedException("Native Carbuncle mirror requires its original secondary mesh.");
                    short scale = unchecked((short)ratio);
                    NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Compose56C2F0(view, local with { Rotation = [scale, 0, 0, 0, scale, 0, 0, 0, scale] });
                    NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(secondary.Bytes, 0, secondary.Bytes.Slice(8, secondary.Bytes.Length - 8), scaled, projection, new(0x2000, null, null, null, null, null, 17, 2));
                    written = NativeBattleCommonPacketArena.Append(written.Process, written.Ordering, batch);
                }
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }

            NativeBattleCarbuncle278ModelStep ModelCreature(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                NativeBattleEffectOwnedModelResource resource = current.Effect.Binding.Carbuncle278Resources?.CreatureModel ?? throw new NotSupportedException("Native Carbuncle requires its original creature model.");
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                if (state.Pause8114 == 0)
                {
                    NativeBattleCarbuncle278CreaturePose advanced = NativeBattleCarbuncle278Model.Advance6FBDB0(task, current.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle requires its current executable skeleton."), resource);
                    task = advanced.Task;
                    current = current with { Memory = current.Memory with { CarbuncleExecutableMemory = advanced.Executable } };
                }
                bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Carbuncle model drawing requires its current hardware capability.");
                NativeBattleCarbuncle278ModelDraw draw = NativeBattleCarbuncle278Model.Draw6A79B0(current.Memory, task, resource, NativeCamera?.Projection ?? throw new NotSupportedException("Native Carbuncle model drawing requires its current projection."), hardware);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle model drawing requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(draw.Memory.PrimitiveProcess!, primitive.Pending, draw.Packets);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return new(current with { Memory = draw.Memory with { PrimitiveProcess = written.Process } }, task);
            }

            NativeBattleCarbuncle278ModelStep AdvanceProgramme(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                NativeBattleCarbuncle278Resources resources = current.Effect.Binding.Carbuncle278Resources ?? throw new NotSupportedException("Native Carbuncle requires its original current resources.");
                ushort counter = task.ReadWord(0xC);
                if (counter <= 145)
                {
                    NativeBattleCarbuncle278ProgrammeDraw first = NativeBattleCarbuncle278Programme.Advance681949(current.Memory, task, resources.CreatureProgramme ?? throw new NotSupportedException("Native Carbuncle requires its first original creature programme."), resources.CreatureModel ?? throw new NotSupportedException("Native Carbuncle requires its original creature model."), CurrentProjection(), CurrentColors());
                    current = AppendProgramme(current, first);
                    task = first.Task;
                }
                if (counter >= 145)
                {
                    NativeBattleCarbuncle278ProgrammeDraw second = NativeBattleCarbuncle278Programme.Advance681949(current.Memory, task, resources.SecondCreatureProgramme ?? throw new NotSupportedException("Native Carbuncle requires its second original creature programme."), resources.CreatureModel ?? throw new NotSupportedException("Native Carbuncle requires its original creature model."), CurrentProjection(), CurrentColors());
                    current = AppendProgramme(current, second);
                    task = second.Task;
                }
                return new(current, task);
            }

            NativeBattleCameraProjection CurrentProjection() => NativeCamera?.Projection ?? throw new NotSupportedException("Native Carbuncle programme requires its current projection.");
            NativeBattleSummonColors CurrentColors() => definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Carbuncle programme requires its authenticated color constants.");

            NativeBattleCarbuncle278Frame AppendProgramme(NativeBattleCarbuncle278Frame current, NativeBattleCarbuncle278ProgrammeDraw drawn)
            {
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(drawn.Memory);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle programme requires its current ordering table.");
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(state.PacketCursor81BC - state.ModelBuffer81F8)), primitive.Pending, drawn.Packets);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = written.Memory with { Carbuncle278 = state with { PacketCursor81BC = checked(state.ModelBuffer81F8 + (uint)written.Cursor) } } };
            }

            int ActorIndex(uint actor)
            {
                uint offset = unchecked(actor - 0x1D972C0u);
                if (offset % 0x9C != 0 || offset / 0x9C >= graphics.Models.Length)
                {
                    throw new InvalidDataException("Native Carbuncle recipient does not address a current physical actor row.");
                }
                return checked((int)(offset / 0x9C));
            }

            NativeBattleCarbuncle278ModelStep AdvanceRecipient(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                if (unchecked((short)task.ReadWord(0xC)) < 21)
                {
                    NativeBattleGfAnimationSource source = current.Effect.Binding.Carbuncle278Resources?.RecipientProgramme ?? throw new NotSupportedException("Native Carbuncle requires its original recipient programme.");
                    NativeBattleCarbuncle278ProgrammeDraw drawn = NativeBattleCarbuncle278Programme.AdvanceRecipient6822F0(current.Memory, task, source, CurrentProjection(), CurrentColors());
                    current = AppendProgramme(current, drawn);
                    task = drawn.Task;
                    current = NativeBattleCarbuncle278Recipients.SpawnSparkle6822F0(current, task.ReadDword(0x10), address => (NativeGraphicsPoseResource(graphics, ActorIndex(address)).PoseSource ?? throw new NotSupportedException("Native Carbuncle recipient sparkles require the current actor skeleton.")).BoneCount, (address, bone) => ReadNativeGraphicsBone(graphics, ActorIndex(address), checked((byte)bone), 0));
                }
                int actor = ActorIndex(task.ReadDword(0x10));
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle recipient cache requires its current actor scale.");
                short yaw = model.Rotation14 ?? throw new NotSupportedException("Native Carbuncle recipient cache requires its current actor yaw.");
                task = NativeBattleCarbuncle278Recipients.Cache6DBCC0(task, yaw, transform.Scale38, CurrentProjection().View ?? throw new NotSupportedException("Native Carbuncle recipient cache requires its current view matrix."));
                return new(current, task);
            }

            NativeBattleCarbuncle278ModelStep DrawRecipientChild(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                NativeBattleCarbuncle278Resources resources = current.Effect.Binding.Carbuncle278Resources ?? throw new NotSupportedException("Native Carbuncle children require their original current resources.");
                NativeBattleModulePoolTask parent = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(task.ReadDword(0x18));
                NativeBattleCarbuncle278ChildDraw draw = task.Callback switch
                {
                    0x683760 => NativeBattleCarbuncle278ChildDrawing.Draw683760(current.Memory, task, parent, resources, CurrentProjection(), CurrentColors()),
                    0x6831D0 => NativeBattleCarbuncle278ChildDrawing.Draw6831D0(current.Memory, task, resources, CurrentProjection(), CurrentColors()),
                    _ => NativeBattleCarbuncle278ChildDrawing.Draw683330(current.Memory, task, parent, resources, CurrentProjection())
                };
                NativeBattleCarbuncle278ProgrammeDraw packets = new(draw.Memory, draw.Task, draw.Packets, 0);
                return new(AppendProgramme(current, packets), draw.Task);
            }

            NativeBattleCarbuncle278ModelStep DrawRecipientRange(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task, uint address)
            {
                NativeBattleCarbuncle278RangePhase phase = NativeBattleCarbuncle278RangeSetup.Read6825A5(current.Memory, unchecked((short)task.ReadWord(0xC)));
                NativeBattleStaticMeshResource mesh = current.Effect.Binding.Carbuncle278Resources?.CreatureBoundsMesh ?? throw new NotSupportedException("Native Carbuncle range requires its original source mesh.");
                NativeBattleCarbuncle278RangeDraw draw = NativeBattleCarbuncle278RangeMesh.Emit682760(mesh, NativeBattleCarbuncle278ChildDrawing.ReadCached(current.Memory, address), CurrentProjection(), phase.Minimum, phase.Maximum, phase.Fade, phase.VOffset, CurrentColors());
                if (draw.FarColorWritten)
                {
                    NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle range requires its current far-color controls.");
                    NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                    current = current with { Memory = current.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(0) } } };
                }
                current = AppendProgramme(current, new(current.Memory, task, draw.Packets, 0));
                int actor = ActorIndex(task.ReadDword(0x10));
                short scale = graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native Carbuncle rings require the current actor scale WORD.");
                current = NativeBattleCarbuncle278RangeSetup.Spawn6826B7(current, address, phase, scale);
                return new(current, task);
            }

            NativeBattleCarbuncle278Frame ApplyRecipientContact(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                int actor = ActorIndex(task.ReadDword(0x10));
                int header = current.Effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("Native Carbuncle contact requires its current header pointer.");
                foreach (NativeBattleContactPacket packet in nativeActionPackets.ReadHeaderContacts506BA0(header))
                {
                    if (packet.Primary.ActorIndex == actor)
                    {
                        var pending = graphics.PendingContacts;
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [packet] });
                        graphics = graphics with { PendingContacts = pending };
                    }
                }
                return current;
            }

            NativeBattleCarbuncle278Frame QueueFile(NativeBattleCarbuncle278Frame current, NativeBattleEffectFileRequest request)
            {
                NativeBattleEffectFileAsset file = current.Effect.Binding.Carbuncle278Resources?.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException("Native Carbuncle requires its requested original archive file.");
                if (file.Data.IsDefaultOrEmpty)
                {
                    throw new NotSupportedException("Native Carbuncle requested archive bytes are unavailable.");
                }
                NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Carbuncle file delivery requires its physical arena.");
                arena = arena.Write(checked((int)(request.DestinationAddress - NativeBattleEffectNodeMemory.ArenaAddress)), file.Data.AsSpan());
                NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Carbuncle file delivery requires its retained IO clock.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
                return current with { Memory = current.Memory with { Arena = arena } };
            }

            NativeBattleCarbuncle278ModelStep PresentProgramme(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                ushort counter = task.ReadWord(0xC);
                PlayCounterSound(current, counter);
                if (counter is 110 or 136)
                {
                    NativeBattleEffectOwnedModelResource model = current.Effect.Binding.Carbuncle278Resources?.CreatureModel ?? throw new NotSupportedException("Native Carbuncle requires its original creature animations.");
                    NativeBattleCarbuncle278CreaturePose selected = NativeBattleCarbuncle278Creature.Select6574D0(task, current.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle requires its current creature skeleton."), model, counter == 110 ? (byte)1 : (byte)2);
                    task = selected.Task;
                    current = current with { Memory = current.Memory with { CarbuncleExecutableMemory = selected.Executable } };
                }
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Carbuncle requires its current programme camera.");
                if (counter < 145)
                {
                    NativeCamera = NativeBattleCarbuncle278Camera.AdvanceProgrammeWindow(camera, counter);
                }
                else
                {
                    current = NativeBattleCarbuncle278Recipients.Spawn681B8C(current, task, actor => ReadNativeModelAnchor(graphics, ActorIndex(actor)));
                    if (counter == 145)
                    {
                        graphics = graphics with { Models = graphics.Models.Select(model => model.Index < 3 ? model with { Flags0 = unchecked((ushort)(model.Flags0 & 0xFFFB)) } : model).ToImmutableArray() };
                    }
                    NativeCamera = NativeBattleCarbuncle278Camera.AdvanceSecondProgrammeWindow(camera, NativeBattleCarbuncle278Entry.RequireState(current.Memory).Centroid81C4, counter);
                }
                return new(current, task);
            }

            NativeBattleCarbuncle278Frame DrawSparkle(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                NativeBattleCarbuncle278Resources resources = current.Effect.Binding.Carbuncle278Resources ?? throw new NotSupportedException("Native Carbuncle sprites require their original current resources.");
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Carbuncle sparkles require their current camera.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Carbuncle sparkles require their current projection.");
                NativeBattleModuleDraw sprite = task.Callback == 0x683DC0 ? NativeBattleCarbuncle278ReturnParticles.Draw683DC0(task, resources.ReturnSprites) : NativeBattleCarbuncle278Sparkles.Draw683E80(task, resources.SparkleSprite ?? throw new NotSupportedException("Native Carbuncle requires its original sparkle sprite."));
                NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(sprite, projection, camera.Roll, 17, 2);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle sparkles require their current ordering table.");
                int cursor = checked((int)(state.PacketCursor81BC - state.ModelBuffer81F8));
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = written.Memory with { Carbuncle278 = state with { PacketCursor81BC = checked(state.ModelBuffer81F8 + (uint)written.Cursor) } } };
            }

            NativeBattleCarbuncle278Frame PresentSparkleWindow(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                ushort counter = task.ReadWord(0xC);
                PlayCounterSound(current, counter);
                current = NativeBattleCarbuncle278Sparkles.Spawn681B3F(current, task);
                NativeBattleVector? target = null;
                if (counter < 92)
                {
                    NativeBattleEffectOwnedModelResource model = current.Effect.Binding.Carbuncle278Resources?.CreatureModel ?? throw new NotSupportedException("Native Carbuncle camera requires its original creature model.");
                    target = NativeBattleCarbuncle278Anchors.ReadVertex682270(task, current.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle camera requires its current creature skeleton."), model, 0, 176);
                }
                NativeCamera = NativeBattleCarbuncle278Camera.AdvanceSparkleWindow(NativeCamera ?? throw new NotSupportedException("Native Carbuncle camera requires its current pose."), counter, target);
                return current;
            }

            void PlayCounterSound(NativeBattleCarbuncle278Frame current, ushort counter)
            {
                if (NativeBattleCarbuncle278Finish.SoundIndex681ACE(counter) is not int index)
                {
                    return;
                }
                ImmutableArray<uint> sounds = current.Effect.Binding.Carbuncle278Resources?.Sounds ?? [];
                if (sounds.IsDefault || index >= sounds.Length)
                {
                    throw new NotSupportedException("Native Carbuncle requires its selected original sound descriptor.");
                }
                PublishNativeModuleSound(new(sounds[index], 0, null) { Pan = 128 });
            }

            NativeBattleCarbuncle278ModelStep PresentReturn(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                ushort counter = task.ReadWord(0xC);
                PlayCounterSound(current, counter);
                if (counter is >= 219 and <= 230)
                {
                    current = NativeBattleCarbuncle278Sparkles.Spawn681B3F(current, task);
                }
                current = NativeBattleCarbuncle278ReturnParticles.Spawn681C31(current, counter);
                NativeBattleVector? anchor = null;
                if (counter < 231)
                {
                    NativeBattleEffectOwnedModelResource model = current.Effect.Binding.Carbuncle278Resources?.CreatureModel ?? throw new NotSupportedException("Native Carbuncle return camera requires its original creature model.");
                    anchor = NativeBattleCarbuncle278Anchors.ReadVertex682270(task, current.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle return camera requires its current creature skeleton."), model, 0, 176);
                }
                if (counter == 215)
                {
                    graphics = graphics with { Models = graphics.Models.Select(model => model.Index < 3 ? model with { Flags0 = (ushort)(model.Flags0 | 4) } : model).ToImmutableArray() };
                    task = task.WriteDword(0x78, 0);
                }
                NativeCamera = NativeBattleCarbuncle278Camera.AdvanceReturnWindow(NativeCamera ?? throw new NotSupportedException("Native Carbuncle return requires its current camera owner."), NativeBattleCarbuncle278Entry.RequireState(current.Memory).Base81CC, counter, anchor);
                return new(current, task);
            }

            NativeBattleCarbuncle278Frame BeginFinalTexture(NativeBattleCarbuncle278Frame current)
            {
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(state.ModelBuffer81F8 + 0x3ECC), 0x25081D8, (address, value) => current = current with { Memory = NativeBattleCarbuncle278Finish.WriteCompletion508660(current.Memory, address, value) });
                graphics = graphics with { Auxiliary = auxiliary };
                return current;
            }

            NativeBattleCarbuncle278Frame StopVibration(NativeBattleCarbuncle278Frame current, int handle)
            {
                NativeVibrationProcessState vibration = NativeVibrationProcess ?? throw new NotSupportedException("Native Carbuncle completion requires its retained vibration process.");
                if (vibration.IsPlaying(handle))
                {
                    NativeVibrationProcess = vibration.Stop4A2940(handle);
                }
                return current;
            }

            NativeBattleCarbuncle278Frame RestoreActors(NativeBattleCarbuncle278Frame current)
            {
                graphics = graphics with { Models = NativeBattleCarbuncle278Finish.Restore681199(graphics.Models, NativeBattleCarbuncle278Entry.RequireState(current.Memory).Lift81D0) };
                return current;
            }

            NativeBattleCarbuncle278Frame PresentCreature(NativeBattleCarbuncle278Frame current, ushort counter)
            {
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                PlayCounterSound(current, counter);
                if (counter == 2)
                {
                    graphics = graphics with { Models = graphics.Models.Select(model => UpdateModel(model)).ToImmutableArray() };
                }
                if (counter <= 36)
                {
                    NativeCamera = NativeBattleCarbuncle278Camera.AdvanceFirstWindow(NativeCamera ?? throw new NotSupportedException("Native Carbuncle requires its current camera globals."), state.Base81CC, counter);
                }
                return current;

                NativeBattleGraphicsModel UpdateModel(NativeBattleGraphicsModel model)
                {
                    if (model.Index < 3)
                    {
                        return model with { Flags0 = (ushort)(model.Flags0 | 4) };
                    }
                    if ((model.Flags0 & 2) == 0)
                    {
                        return model;
                    }
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle lift requires its selected actor's position WORD20.");
                    return model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + state.Lift81D0)) } } };
                }
            }
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private bool TryWriteNativeCarbuncle278Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (address != 0x25081D8)
        {
            return false;
        }
        NativeEffectMemory = NativeBattleCarbuncle278Finish.WriteCompletion508660(NativeEffectMemory ?? throw new NotSupportedException("Native Carbuncle texture completion requires its retained effect memory."), address, value);
        return true;
    }
}
