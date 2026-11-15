using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint WaterTemporaryAddress = 0x1A77238;
    private const uint WaterParametersAddress = 0x1D99A78;
    private const uint WaterHeadersAddress = 0x1D280C4;

    private NativeBattleGraphicsState EnterNativeWaterA9B200(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleWater222Resources resources = effect.Binding.Water222Resources ?? throw new NotSupportedException("Native Water requires its authenticated resources.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Water requires its action parameters.");
        NativeBattleWaterInitializationResource first = WaterResource(effect, resources, 0);
        NativeBattleWaterInitializationResource second = WaterResource(effect, resources, 1);
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(parameters.Header);
        ImmutableArray<byte> contacts = WaterContacts(header);
        NativeBattleWaterInitializationInputs inputs = new(first, second, NativeBattleWater222Controller.CreateTemporaryFrame(WaterTemporaryAddress), WaterParametersAddress, WaterHeadersAddress + (uint)(20 * parameters.Header), 0x186C84C, parameters.Source, parameters.Header, contacts[0], contacts, header.ReadContactCount(), header.ReadRepeatModeMinusOne(), graphics.Models);
        NativeBattleWater222Frame initialized = NativeBattleWater222Controller.InitializeA9B200(new(RequireWaterMemory(), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Water requires its retained CRT state.")), resources, inputs);
        NativeEffectMemory = initialized.Memory;
        nativeCrtRandom = initialized.Random;
        return graphics with { OriginalEffect = initialized.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeWaterA9B870(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleWater222Resources resources = effect.Binding.Water222Resources ?? throw new NotSupportedException("Native Water requires its original resources.");
        NativeBattleEffectParameters parameters = effect.Parameters!;
        NativeBattleScriptedGraphicsMemory registers = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native Water requires its current graphics registers.");
        NativeBattleCameraProjection projection = ReadProjection();
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Water requires its retained current view.");
        short focal = NativeCamera!.FocalLength ?? throw new NotSupportedException("Native Water requires its global focal WORD.");
        NativeBattlePrimitiveProcess process = RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native Water requires its COMMON packet bank.");
        uint common = checked((uint)(process.ByteCursor ?? throw new NotSupportedException("Native Water COMMON cursor is unknown.")));
        NativeBattleWaterCallbackInput input = new(resources, checked((ushort)graphics.Flags), MatrixBytes(view), 0, common, focal);
        NativeBattleWaterPresentationServices presentation = new((_, pointer) => graphics.ActorModels.Get(PhysicalActor(pointer, graphics.Models.Length)).Flags0, (current, pointer, flags) =>
        {
            int index = PhysicalActor(pointer, graphics.Models.Length);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, graphics.ActorModels.Get(index) with { Flags0 = flags }) };
            return current;
        }, (current, address, argument, pan) =>
        {
            uint sound = BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(address, 4), 4));
            PublishNativeModuleSound(new(sound, unchecked((uint)(int)argument), null) { Pan = pan, RawPan = pan });
            return current;
        });
        NativeBattleWaterContextServices context = new((_, pointer) =>
        {
            int? linked = graphics.ActorModels.Get(PhysicalActor(pointer, graphics.Models.Length)).ReadLinkedModelIndex140();
            return linked is int index ? ActorPointer(index) : 0;
        }, (_, pointer) => ActorPointer(NativeBattleModelRings.Walk(graphics.Models, PhysicalActor(pointer, graphics.Models.Length)).Min()), (_, address, length) => ReadAction(address, length), (current, address) =>
        {
            PublishMemory(current);
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(ContactIndex(address))] });
            graphics = graphics with { PendingContacts = pending };
            return ReadMemory(current);
        }, current =>
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Water requires its camera owner.")).Apply50A730();
            return current;
        }, (current, index, depth, color) =>
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Water requires its four stage groups.");
            NativeStage = stage with { Groups = stage.Groups.SetItem(index, stage.Groups[index] with { ColorDepth = depth, ColorFlags = color }) };
            return current;
        }) { ActionPackets = nativeActionPackets.PresentationRoster is not null ? nativeActionPackets : null };
        NativeBattleWaterInstructionServices instructions = new(ResolveProgram, presentation, context, (_, address) => ParameterByte(address), (_, pointer) => graphics.ActorModels.Get(PhysicalActor(pointer, graphics.Models.Length)), ActorAnchor, (_, _, address) => address == WaterParametersAddress ? WaterHeadersAddress + (uint)(20 * parameters.Header) : throw new NotSupportedException("Native Water parameter base does not identify its current action."), (_, _, address, _) => CaptureInputs(address), _ => RequireTextures(), (current, textures) =>
        {
            NativeEffectMemory = RequireWaterMemory() with { Textures = textures };
            return current;
        }, (current, request) =>
        {
            PublishMemory(current);
            NativeBattleWaterIoQueued queued = NativeBattleWaterIoService.CopyAndQueueFile(RequireWaterMemory(), NativeFileIo ?? throw new NotSupportedException("Native Water requires its current IO scheduler."), request, resources.Io ?? throw new NotSupportedException("Native Water requires its original external files."));
            NativeEffectMemory = queued.Memory;
            NativeFileIo = queued.FileIo;
            return ReadMemory(current);
        }, current => checked(current.TemporaryFrame.Address + 0x100), CreateNativeActorAnchorServices()) { MagicTextureBufferAddress = RequireWaterMemory().MagicTextureBufferAddress1D99A88 };
        NativeBattleWaterActorPoseServices actors = new((current, actor) =>
        {
            if (presentationForeignActors?.TryGetValue(actor.Index, out NativeBattleForeignActor? foreign) == true)
            {
                return new(current, [], []) { Pose = foreign.Pose ?? throw new NotSupportedException("The host actor cannot supply its current posed skeleton segments.") };
            }
            NativeBattlePoseSource source = PoseSource(actor.Index);
            NativeBattleGraphicsModel published = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor.Index, published) };
            return new(current, source.Skeleton, NativeBattleBodyBoneMatrices.Require(published, source));
        }, (current, actor, matrices) =>
        {
            if (presentationForeignActors?.ContainsKey(actor.Index) == true)
            {
                return current;
            }
            NativeBattleBodyBoneMatrices owner = graphics.ActorModels.Get(actor.Index).CurrentBodyBoneMatrices ?? throw new NotSupportedException("Native Water actor composition requires its actual body publication.");
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor.Index, actor with { CurrentBodyBoneMatrices = owner with { Matrices = matrices } }) };
            return current;
        }, CreateNativeActorAnchorServices());
        NativeBattleWaterDrawInputs drawInput = new(resources, registers.Zsf3 ?? throw new NotSupportedException("Native Water requires its initialized current ZSF3."), registers.Dqa, registers.Dqb, 0);
        NativeBattleWaterDrawServices drawing = new(_ => ReadProjection(), PublishFocal, ShapeExtent, _ => RequireRegisters().Depths, (current, depths) =>
        {
            NativeEffectMemory = RequireWaterMemory() with { ScriptedGraphics = RequireRegisters() with { Depths = depths } };
            return current;
        }, SubmitPackets, CurrentFiles, (current, write) =>
        {
            int file = ArenaOffset(CoreDword(current, write.Upload.SourceIndex == 0 ? 0x2798A68u : 0x2798A6Cu), 0);
            NativeEffectMemory = RequireWaterMemory() with { Textures = RequireTextures().EnqueueArenaRectangle505DF0(write.Upload.Rectangle, current.Arena, checked(file + write.Upload.Offset)) };
            return current;
        }, _ => graphics.Models, (current, models) =>
        {
            graphics = graphics with { ActorModels = models.Select((model, index) => model with { CurrentBodyBoneMatrices = graphics.ActorModels.Get(index).CurrentBodyBoneMatrices }).ToImmutableArray() };
            return current;
        }, actors, ReadMetadata, OtBucket)
        {
            SubmitMesh = SubmitMesh,
            ReadRegisters = _ => new(RequireWaterMemory().GeometryRegisters ?? throw new NotSupportedException("Native Water rectangles require current geometry registers."), RequireWaterMemory().ScriptedColorRegisters ?? throw new NotSupportedException("Native Water rectangles require current arithmetic registers.")),
            PublishRegisters = (current, step) =>
            {
                NativeEffectMemory = RequireWaterMemory() with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
                return current;
            }
        };
        NativeBattleWaterCallbackServices callbacks = new((current, pc) => NativeBattleWaterInstructionDispatcher.ReadInstruction(current, pc, instructions), (current, opcode) => NativeBattleWaterInstructionDispatcher.Execute(current, opcode, resources, instructions), (current, _) => NativeBattleWaterInstructionDispatcher.UpdateCurrent(current, resources), (current, kind) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resources);
            return NativeBattleWaterDrawDispatcher.Draw(current, kind, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), drawInput, drawing);
        }, (current, address, count) =>
        {
            if (address != 0x44 || count != 0x1000)
            {
                throw new NotSupportedException("Native Water clear requires its selected ordering-table range.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Water requires its current ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = primitive.Pending.ClearRange45D550(checked((int)(address / 4)), count) } };
            return current;
        }, PublishFocal) { ReadCurrentFocal = _ => NativeCamera?.FocalLength ?? throw new NotSupportedException("Native Water callback requires its current scalar camera focal.") };
        NativeBattleWater222Frame advanced = NativeBattleWater222Controller.AdvanceA9B870(new(RequireWaterMemory(), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Water CRT state is unknown.")), input, callbacks, WaterTemporaryAddress, RequireWaterMemory);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleCameraProjection ReadProjection() => NativeCamera?.Projection ?? throw new NotSupportedException("Native Water requires its published projection.");
        NativeBattleScriptedGraphicsMemory RequireRegisters() => RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native Water graphics registers are unknown.");
        NativeBattleEffectTextureState RequireTextures() => RequireWaterMemory().Textures ?? throw new NotSupportedException("Native Water requires its texture queue.");
        NativeBattlePoseSource PoseSource(int index) => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Water requires its actual actor rig.");
        NativeBattleWaterExecutionFrame PublishFocal(NativeBattleWaterExecutionFrame current, short value)
        {
            NativeCamera = NativeCamera! with { Projection = ReadProjection() with { FocalLength = unchecked((ushort)value) } };
            return current;
        }
        void PublishMemory(NativeBattleWaterExecutionFrame current) => NativeEffectMemory = RequireWaterMemory() with { ScriptedEffects = current.Core, Arena = current.Arena };
        NativeBattleWaterExecutionFrame ReadMemory(NativeBattleWaterExecutionFrame current) => current with { Core = RequireWaterMemory().ScriptedEffects!, Arena = RequireWaterMemory().Arena! };
        NativeBattleWaterActorAnchorSource ActorAnchor(NativeBattleWaterExecutionFrame current, uint pointer)
        {
            int index = PhysicalActor(pointer, graphics.Models.Length);
            if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
                return new([], [], NativeBattleForeignPlacement.World(model.Transform!, model.Rotation12 ?? 0, model.Rotation14 ?? foreign.Facing, model.Rotation16 ?? 0)) { Pose = foreign.Pose ?? throw new NotSupportedException("The host actor cannot supply posed semantic anchors.") };
            }
            NativeBattlePoseSource source = PoseSource(index);
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(index);
            return new(source.Skeleton, NativeBattleBodyBoneMatrices.Require(actor, source), actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Water actor world matrix is unknown."));
        }
        NativeBattleWaterResetCaptureInputs CaptureInputs(uint address)
        {
            int index = HeaderIndex(address);
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[index];
            NativeBattleActionHeaderSlot first = nativeActionPackets.ReadHeader(parameters.Header);
            return new(parameters.Source, index, WaterContacts(first)[0], WaterContacts(header), header.ReadContactCount(), graphics.Models);
        }
        byte ParameterByte(uint address) => NativeBattleCinematicParameterBytes.Read(parameters, checked((int)(address - WaterParametersAddress)), 1)[0];

        byte[] ReadAction(uint address, int length)
        {
            if (TryReadOriginalHeaderBytes(address, length, out byte[] header))
            {
                return header;
            }
            (BattleContactOccurrence occurrence, int offset) = NativeBattleContactAddress.ResolveOriginalField(address, length);
            return ReadOriginalContactBytes(occurrence, offset, length);
        }
        NativeBattleWaterScriptProgram ResolveProgram(NativeBattleWaterExecutionFrame current, uint address)
        {
            for (int file = 0; file < 2; file++)
            {
                uint start = CoreDword(current, file == 0 ? 0x2798A68u : 0x2798A6Cu);
                int length = resources.Files[file].Data.Length;
                if (address >= start && address - start < length)
                {
                    return new(ArenaOffset(start, length), length, start);
                }
            }
            throw new NotSupportedException("Native Water instruction leaves its current original resource owners.");
        }
        int ShapeExtent(NativeBattleWaterExecutionFrame current, uint address)
        {
            uint file = CoreDword(current, 0x2798A68);
            uint directory = unchecked(file + BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(file + 12, 4), 4)));
            if (BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(directory, 4), 4)) != 14)
            {
                throw new InvalidDataException("Native Water shape directory differs from its selected profile.");
            }
            foreach (int id in new[] { 2, 4, 13 })
            {
                uint relative = BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(directory + (uint)(4 + id * 4), 4), 4));
                if (unchecked(directory + relative) == address)
                {
                    return NativeBattleWaterMeshes.SelectedShapeLength(id);
                }
            }
            throw new NotSupportedException("Native Water current shape leaves its selected mesh resources.");
        }
        ImmutableArray<NativeBattleEffectHeapAllocation> CurrentFiles(NativeBattleWaterExecutionFrame current)
        {
            return resources.Files.Select((file, index) => file with { Data = current.Arena.Read(ArenaOffset(CoreDword(current, index == 0 ? 0x2798A68u : 0x2798A6Cu), file.Data.Length), file.Data.Length).ToImmutableArray() }).ToImmutableArray();
        }
        byte[] ReadMetadata(NativeBattleWaterExecutionFrame current, uint address, int length)
        {
            if (address >= 0x186C86C && address - 0x186C86C <= resources.Profile.Tables.Data.Length - length)
            {
                return resources.Profile.Tables.Data.AsSpan((int)(address - 0x186C86C), length).ToArray();
            }
            return current.Arena.Read(ArenaOffset(address, length), length);
        }
        NativeBattleWaterMeshSubmission SubmitMesh(NativeBattleWaterMeshProjected projected, int extent)
        {
            uint cursor = projected.Frame.ReadTemporaryDword(0x6C);
            short count = BinaryPrimitives.ReadInt16LittleEndian(projected.Frame.Arena.Read(ArenaOffset(cursor + 2, 2), 2));
            if (count == -1)
            {
                return NativeBattleWaterMeshPreparation.Submit(projected, extent, OtBucket);
            }
            NativeBattleScriptedLightingMemory retainedLighting = RequireWaterMemory().ScriptedLighting ?? throw new NotSupportedException("Native Water requires its retained shared lighting register owner.");
            NativeBattleScriptedColorRegisters colorRegisters = RequireWaterMemory().ScriptedColorRegisters ?? throw new NotSupportedException("Native Water requires its retained shared normal-color registers.");
            retainedLighting.Validate();
            colorRegisters.Validate();
            NativeBattleScriptedMaterialStep setup = NativeBattleScriptedMaterial.Prepare(projected.Frame, colorRegisters);
            colorRegisters = setup.Registers;
            NativeBattleWaterMeshSubmission result = NativeBattleWaterMeshPreparation.Submit(projected with { Frame = setup.Frame }, extent, OtBucket, material =>
            {
                ImmutableArray<byte> normal = colorRegisters.NormalInput ?? throw new NotSupportedException("Native Water material requires its published packed normal input.");
                colorRegisters = NativeBattleScriptedNormalColor.Transform4601B0(setup.Lighting, NativeBattleScriptedMeshPreparation.Vector(normal.AsSpan()), material, colorRegisters);
                return colorRegisters.Rgb[2] ?? throw new NotSupportedException("Native Water material did not publish RGB2.");
            });
            NativeEffectMemory = RequireWaterMemory() with { ScriptedLighting = setup.Lighting, ScriptedColorRegisters = colorRegisters };
            return result;
        }
        NativeBattleWaterExecutionFrame SubmitPackets(NativeBattleWaterExecutionFrame current, uint start, NativeBattleGfGeometryBatch batch)
        {
            PublishMemory(current);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Water requires its current ordering table.");
            if ((ReadSlot(current, 0x4C, 1)[0] & 1) != 0)
            {
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(RequireWaterMemory(), ArenaOffset(start, 0), primitive.Pending, batch);
                NativeEffectMemory = written.Memory;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            else
            {
                NativeBattlePrimitiveProcess commonProcess = RequireWaterMemory().PrimitiveProcess!;
                if (start != commonProcess.ByteCursor)
                {
                    throw new InvalidDataException("Native Water COMMON cursor disagrees with its physical bank owner.");
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(commonProcess, primitive.Pending, batch);
                NativeEffectMemory = RequireWaterMemory() with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            return ReadMemory(current);
        }
    }

    private NativeBattleEffectMemory RequireWaterMemory() => NativeEffectMemory ?? throw new NotSupportedException("Native Water effect memory is unknown.");
    private static NativeBattleWaterInitializationResource WaterResource(NativeBattleOriginalEffectState effect, NativeBattleWater222Resources resources, int index)
    {
        uint global = index == 0 ? 0x2796BDCu : 0x2796BD8u;
        NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.Single(value => value.GlobalAddress == global);
        return new(pointer.ArenaOffset, resources.Files[index].Data.Length, NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset);
    }
    private ImmutableArray<byte> WaterContacts(NativeBattleActionHeaderSlot header)
    {
        int start = header.FirstContactIndex ?? throw new NotSupportedException("Native Water header has no actual contact owner.");
        return Enumerable.Range(0, header.ReadContactCount()).Select(index => nativeActionPackets.ReadContact(start + index).Primary.Recipient).ToImmutableArray();
    }
    private static int PhysicalActor(uint pointer, int actorCount)
    {
        return NativeBattleGraphicsModelAddress.TryDecode(pointer, out int actorIndex, actorCount) ? actorIndex : throw new NotSupportedException("Native Water actor pointer leaves its physical model bank.");
    }
    private static uint ActorPointer(int index) => NativeBattleGraphicsModelAddress.Encode(index);
    private int ContactIndex(uint address)
    {
        return NativeBattleContactAddress.TryDecode(address, out int index, nativeActionPackets.StorageCapacity) ? index : throw new NotSupportedException("Native Water contact pointer leaves its physical packet bank.");
    }
    private static int HeaderIndex(uint address)
    {
        uint relative = unchecked(address - WaterHeadersAddress);
        return relative % 20 == 0 && relative / 20 < NativeBattleActionPacketsState.HeaderCapacity ? (int)(relative / 20) : throw new NotSupportedException("Native Water header pointer leaves its physical action bank.");
    }
    private static ImmutableArray<byte> MatrixBytes(NativeBattleActionMatrix matrix)
    {
        byte[] bytes = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(lane * 2), matrix.Rotation[lane]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        return bytes.ToImmutableArray();
    }
    private static int OtBucket(uint address) => address % 4 == 0 && address / 4 < NativeBattlePrimitiveOrdering.BattleTableLength ? (int)(address / 4) : throw new NotSupportedException("Native Water OT address leaves its owned table.");
}
