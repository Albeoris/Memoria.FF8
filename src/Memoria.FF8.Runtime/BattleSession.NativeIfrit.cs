using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeCinematic(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native cinematic entry requires its selected effect.");
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect);
        NativeBattleScriptedInitializationDirectories directories = effect.Binding.ScriptedInitializationDirectories ?? throw new NotSupportedException("Native cinematic requires its original initialization and owner directories.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native cinematic requires its actual action parameters.");
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
        if (assets.Length != 2)
        {
            throw new NotSupportedException("Native cinematic requires both complete executed initializer resources.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        ImmutableArray<byte> contacts = WaterContacts(header);
        if (contacts.IsEmpty)
        {
            throw new NotSupportedException("Native cinematic entry requires its first actual contact actor.");
        }
        NativeBattleWaterInitializationInputs inputs = new(Resource(0, profile.File0Publication), Resource(1, profile.File1Publication), NativeBattleWater222Controller.CreateTemporaryFrame(WaterTemporaryAddress), WaterParametersAddress, WaterHeadersAddress + checked((uint)(20 * parameters.HeaderIndex7C)), profile.InitializationAddress, parameters.Actor78, parameters.HeaderIndex7C, contacts[0], contacts, header.ReadByte(0x10), header.ReadByte(0x11), graphics.Models);
        NativeBattleCinematicEntryStep initialized = NativeBattleCinematicEntry.Initialize(profile, RequireWaterMemory(), effect, directories, inputs);
        NativeEffectMemory = initialized.Memory;
        return graphics with { OriginalEffect = initialized.Effect };

        NativeBattleWaterInitializationResource Resource(int index, uint publication)
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == publication) ?? throw new NotSupportedException("Native cinematic requires its actual initializer resource publication.");
            return new(pointer.ArenaOffset, assets[index].Data.Length, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        }
    }

    private NativeBattleGraphicsState AdvanceNativeCinematic(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleCinematicResources resources = effect.Binding.CinematicResources ?? throw new NotSupportedException("Native cinematic requires its authenticated cinematic tables and resources.");
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect);
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
        if (assets.Length != 2)
        {
            throw new NotSupportedException("Native cinematic callback requires both current resource extents.");
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native cinematic requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native cinematic requires its current projection.");
        NativeBattlePrimitiveProcess process = RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic requires its actual COMMON process.");
        NativeBattleScriptedCallbackInput input = new(effect.Binding.ScriptedInitializationDirectories ?? throw new NotSupportedException("Native cinematic requires its original directories."), assets[1].Data.Length, checked((ushort)graphics.Flags), MatrixBytes(projection.View ?? throw new NotSupportedException("Native cinematic requires its current view matrix.")), 0, checked((uint)(process.ByteCursor ?? throw new NotSupportedException("Native cinematic COMMON cursor is unknown."))), camera.FocalLength ?? throw new NotSupportedException("Native cinematic requires its retained focal WORD."));
        NativeBattleWaterContextServices context = new((_, pointer) =>
        {
            int? linked = graphics.Models[PhysicalActor(pointer)].ReadLinkedModelIndex140();
            return linked is int row ? ActorPointer(row) : 0;
        }, (_, pointer) => ActorPointer(NativeBattleModelRings.Walk(graphics.Models, PhysicalActor(pointer)).Min()), (_, address, length) => ReadAction(address, length), (current, address) =>
        {
            NativeEffectMemory = RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(ContactIndex(address))] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Arena = RequireWaterMemory().Arena!, Core = RequireWaterMemory().ScriptedEffects! };
        }, current =>
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic requires its camera owner.")).Apply50A730();
            return current;
        }, (current, index, depth, color) =>
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native cinematic requires its four stage groups.");
            NativeStage = stage with { Groups = stage.Groups.SetItem(index, stage.Groups[index] with { ColorDepth = depth, ColorFlags = color }) };
            return current;
        });
        NativeBattleScriptedCallbackServices services = new(ReadInstruction, ExecuteInstruction, UpdateSlot, DrawSlots, ClearOrderingTable, PublishFocal) { ReadCurrentFocal = _ => NativeCamera?.FocalLength ?? throw new NotSupportedException("Native cinematic requires its current scratch focal WORD after script execution.") };
        NativeBattleCinematicFrame advanced = NativeBattleCinematicController.Advance(profile, new(RequireWaterMemory(), effect, nativeCrtRandom ?? throw new NotSupportedException("Native cinematic callback requires its retained CRT state.")), input, services, WaterTemporaryAddress, RequireWaterMemory);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        byte[] ReadAction(uint address, int length)
        {
            if (address >= WaterHeadersAddress && address < WaterContactsAddress)
            {
                uint relative = address - WaterHeadersAddress;
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[checked((int)(relative / 20))];
                return Enumerable.Range((int)(relative % 20), length).Select(header.ReadByte).ToArray();
            }
            uint contact = address - WaterContactsAddress;
            byte[] bytes = nativeActionPackets.ReadContact(checked((int)(contact / 24))).Encode();
            return bytes.AsSpan((int)(contact % 24), length).ToArray();
        }

        NativeBattleWaterScriptProgram Program(NativeBattleWaterExecutionFrame current, uint address)
        {
            for (int index = 0; index < assets.Length; index++)
            {
                uint start = CoreDword(current, 0x2798A68 + (uint)(index * 4));
                int length = assets[index].Data.Length;
                if (address >= start && address - start < length)
                {
                    return new(ArenaOffset(start, length), length, start);
                }
            }
            throw new NotSupportedException("Native cinematic instruction leaves its current resource owners.");
        }

        ushort ReadInstruction(NativeBattleWaterExecutionFrame current, uint address)
        {
            NativeBattleWaterScriptProgram program = Program(current, address);
            int offset = checked((int)(address - program.Address));
            if (offset > program.Length - 2)
            {
                throw new InvalidDataException("Native cinematic instruction leaves its complete WORD operand owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(current.Arena.Read(program.ArenaOffset + offset, 2));
        }

        NativeBattleWaterExecutionFrame ExecuteInstruction(NativeBattleWaterExecutionFrame current, ushort opcode)
        {
            try
            {
                return ExecuteInstructionCore(current, opcode);
            }
            catch (NotSupportedException error)
            {
                throw new NotSupportedException($"Native cinematic effect {profile.Effect} instruction {current.ReadTemporaryWord(0x14A):X4} at PC {CoreDword(current, 0x2797450):X8} requires its complete command contract.", error);
            }
        }

        NativeBattleWaterExecutionFrame ExecuteInstructionCore(NativeBattleWaterExecutionFrame current, ushort opcode)
        {
            uint pc = CoreDword(current, 0x2797450);
            uint selectedHandler;
            try
            {
                selectedHandler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "vm_table", opcode, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.InstructionTable + 4u * opcode), 4)));
            }
            catch (NotSupportedException error)
            {
                throw new NotSupportedException($"Native cinematic effect {profile.Effect} instruction {current.ReadTemporaryWord(0x14A):X4} at PC {pc:X8} requires its selected command contract.", error);
            }
            NativeBattleWaterScriptProgram program = Program(current, pc);
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            switch (opcode)
            {
                case 0x10D:
                    return NativeBattleCinematicControlCommands.Wait10D(current, program, ResolveCurrentNode);
                case 0x51:
                    return NativeBattleCinematicControlCommands.Execute51(current, program);
                case 0x41:
                    return NativeBattleCinematicModel.Control41(current, program);
                case 0xD1:
                    return selectedHandler == 0xAFB270 ? NativeBattleCinematicControlCommands.BranchD1(current, program, graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native Brothers stage branch requires its retained resource BYTE98990.")) : current;
                case 0:
                    return NativeBattleWaterStopCommand.StopRemoveAA5120(current);
                case 1:
                    NativeBattleWaterHeaderNextResult next = NativeBattleWaterHeaderNextCommand.Execute(current, frame => Program(frame, CoreDword(frame, 0x2797450)), frame =>
                    {
                        uint address = CoreDword(frame, 0x2798A6C);
                        return new(ArenaOffset(address, assets[1].Data.Length), assets[1].Data.Length, address);
                    }, (_, _, address) =>
                    {
                        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native cinematic reset requires its current action parameters.");
                        return address == WaterParametersAddress ? WaterHeadersAddress + (uint)(20 * parameters.HeaderIndex7C) : throw new NotSupportedException("Native cinematic reset requires its selected parameter base.");
                    }, (_, _, address, _) =>
                    {
                        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native cinematic reset requires its current action parameters.");
                        int headerIndex = HeaderIndex(address);
                        NativeBattleActionHeaderSlot selected = nativeActionPackets.Headers[headerIndex];
                        NativeBattleActionHeaderSlot first = nativeActionPackets.Headers[parameters.HeaderIndex7C];
                        return new(parameters.Actor78, headerIndex, WaterContacts(first)[0], WaterContacts(selected), selected.ReadByte(0x10), graphics.Models);
                    });
                    return next.Frame;
                case 0x1D:
                    return NativeBattleWaterStopCommand.WaitStop1D(current, program, ResolveCurrentNode);
                case 2:
                case 3:
                case 4:
                case 5:
                case 9:
                case 0x1E:
                case 0x1F:
                case 0x33:
                case 0x74:
                case 0x79:
                case 0xBC:
                case 0xE7:
                    uint header = opcode == 5 ? CoreDword(current, 0x279744C) : 0;
                    NativeBattleWaterScriptControlResult control = NativeBattleWaterScriptControl.Execute(current.Arena, bank, index, program, pc, current.ReadTemporaryByte(0x143), opcode == 5 ? CoreWord(current, header + 2) : (ushort)0);
                    current = current with { Arena = control.Arena };
                    current = WriteCoreDword(current, 0x2797450, control.NextInstructionPointer);
                    if (opcode == 5)
                    {
                        current = WriteCoreWord(current, header + 2, control.HeaderFlagsWord2);
                    }
                    return control.YieldWait is ushort wait ? current.WriteTemporaryWord(0x13E, wait) : current;
                case 0x21:
                case 0x7D:
                    return NativeBattleWaterWorkCommands.Execute(current, program, RequireWaterMemory().MagicTextureBufferAddress1D99A88);
                case 0x20:
                    return NativeBattleWaterContextCommands.Execute(current, program, context);
                case 0x22:
                    return NativeBattleIfritContactCommand.Execute(current, context);
                case 0x93:
                case 0xAA:
                    return NativeBattleScriptedLightCommands.Execute(current, program, bank, index);
                case 0x30:
                    PublishNativeModuleSound((RequireWaterMemory().SummonStream ?? throw new NotSupportedException("Native cinematic volume requires its retained summon stream number.")).Play5018C0(128, (uint)(current.ReadTemporaryWord(0x14A) >> 9)));
                    return NativeBattleScriptedCommandReads.Advance(current, 2);
                case 0x3F:
                    return NativeBattleCinematicModel.Register(current, program, ModelServices(0));
                case 0x37:
                    return NativeBattleScriptedMotionReset.Execute(current, bank, UpdateSlot);
                case 0x47:
                    byte[] viewTranslation = ReadSlot(current, 0x94, 6);
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic view translation requires its current camera owner.")) with { ViewTranslationScratch = new(BinaryPrimitives.ReadInt16LittleEndian(viewTranslation), BinaryPrimitives.ReadInt16LittleEndian(viewTranslation.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(viewTranslation.AsSpan(4))) };
                    return NativeBattleScriptedCommandReads.Advance(current, 2);
                case 0x4C:
                    return NativeBattleWaterScalarReferenceCommands.ChangeUpdate(current, program, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 0xD2:
                    return NativeBattleWaterScalarReferenceCommands.NegateMotion(current, program, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]), frame => frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank)) });
                case 0x61:
                case 0x62:
                    return NativeBattleWaterScalarReferenceCommands.CopyMotion(current, program, ResolveCurrentNode, frame => frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank)) }, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 0x4B:
                    return NativeBattleCinematicPaletteCommands.Execute(current, program, resources, bank, index, (frame, rectangle, address) =>
                    {
                        NativeBattleEffectTextureState textures = RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic palette transfer requires its current texture queue.");
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures.EnqueueScriptedRectangle505DF0(rectangle, address) };
                        return frame;
                    });
                case 0x3C:
                case 0x56:
                case 0x4A:
                case 0x2F:
                case 0x25:
                    return NativeBattleWaterRectangleCommands.Execute(current, program, bank, index, (frame, shapeIndex) => NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex).Selected, (frame, shapeIndex) =>
                    {
                        var selected = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex);
                        return (frame.WriteTemporaryDword(0xFC, selected.Directory), selected.Selected);
                    });
                case >= 0xA and <= 0x17:
                    return NativeBattleWaterInstructionDispatcher.ExecuteVector(current, program, assets[1].Data.Length);
                case 7:
                case 0xA0:
                case 0xA1:
                    return NativeBattleScriptedSchedulingCommands.Execute(current, program);
                case 0x26:
                case 0x32:
                case 0x8B:
                    return NativeBattleWaterChildCommands.Execute(current, program, bank, opcode == 0x32 ? checked(current.TemporaryFrame.Address + 0x100) : 0);
                case 0x6D:
                    NativeBattleScriptedProcessFlagsStep flags = NativeBattleScriptedProcessFlags.Execute(current, NativeStage ?? throw new NotSupportedException("Native cinematic flags require their current four stage groups."));
                    NativeStage = flags.State;
                    return flags.Frame;
                case 0x2B:
                case 0x4D:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x5A:
                case 0x5E:
                case 0xD0:
                    return NativeBattleWaterPresentationCommands.Execute(current, program, bank, index, new((_, pointer) => graphics.Models[PhysicalActor(pointer)].Flags0, (frame, pointer, value) =>
                    {
                        int row = PhysicalActor(pointer);
                        graphics = graphics with { Models = graphics.Models.SetItem(row, graphics.Models[row] with { Flags0 = value }) };
                        return frame;
                    }, (frame, address, argument, pan) =>
                    {
                        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
                        PublishNativeModuleSound(new(sound, unchecked((uint)(int)argument), null) { Pan = pan, RawPan = pan });
                        return frame;
                    }));
                case 0x52:
                    return NativeBattleWaterTextureMetadata.Execute52AA3A60(current, resources);
                case 0x13D:
                    return NativeBattleScriptedVisibility.Execute(current, program, new((_, row) => graphics.Models[row].Flags0, (frame, row, value) =>
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(row, graphics.Models[row] with { Flags0 = value }) };
                        return frame;
                    }));
                case 0x31:
                case 0x34:
                    NativeBattleScriptedStreamStep streamed = NativeBattleScriptedStreamCommands.Execute(current, RequireWaterMemory().SummonStream ?? throw new NotSupportedException("Native cinematic requires its retained summon-stream owner."), (_, address, length) => resources.ReadStatic(address, length).ToArray(), _ => throw new NotSupportedException("Native cinematic stream transfer requires its retained external clearing flag."));
                    NativeEffectMemory = RequireWaterMemory() with { SummonStream = streamed.Stream };
                    return streamed.Frame;
                case 0x27:
                case 0x28:
                case 0x29:
                    NativeBattleCinematicTextureCommandStep transferred = NativeBattleCinematicTextureCommands.Execute(current, program, resources, RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic requires its current texture queue."));
                    NativeEffectMemory = RequireWaterMemory() with { Textures = transferred.Textures };
                    return transferred.Frame;
                case 6:
                case 8:
                    NativeBattleCinematicIoStep io = NativeBattleCinematicIoCommands.Execute(current, program, resources, NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect).LoadCallback);
                    if (io.Request is not null)
                    {
                        NativeEffectMemory = RequireWaterMemory() with { Arena = io.Frame.Arena, ScriptedEffects = io.Frame.Core };
                        NativeBattleWaterIoQueued queued = NativeBattleCinematicIoCommands.CopyAndQueue(RequireWaterMemory(), NativeFileIo ?? throw new NotSupportedException("Native cinematic requires its retained file IO scheduler."), io.Request, resources);
                        NativeEffectMemory = queued.Memory;
                        NativeFileIo = queued.FileIo;
                        return io.Frame with { Arena = queued.Memory.Arena!, Core = queued.Memory.ScriptedEffects! };
                    }
                    return io.Frame;
                case 0x39 when current.ReadTemporaryWord(0x14A) >> 12 is 2 or 3:
                    NativeBattleScriptedVibrationStep vibration = NativeBattleScriptedVibrationCommands.Execute(current, program, NativeVibrationProcess ?? throw new NotSupportedException("Native cinematic requires its retained vibration process."), (frame, address) =>
                    {
                        uint file = CoreDword(frame, 0x2798A68);
                        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(file + 0x24, 4), 4));
                        if (address != unchecked(file + relative))
                        {
                            throw new InvalidDataException("Native cinematic vibration resource does not match its current file header.");
                        }
                        return resources.Vibration;
                    });
                    NativeVibrationProcess = vibration.Vibration;
                    return vibration.Frame;
                case 0x39:
                    return NativeBattleScriptedCameraCommands.Execute(current, new((frame, address) =>
                    {
                        NativeBattleWaterScriptProgram file = Program(frame, address);
                        int relative = checked((int)(address - file.Address));
                        NativeBattleEffectHeapAllocation animation = new("cinematic-camera", frame.Arena.Read(file.ArenaOffset + relative, file.Length - relative).ToImmutableArray());
                        NativeBattleEffectTextureState textures = (RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic camera requires its current texture control owner.")).Register5099A0(animation);
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures };
                        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic requires its current camera owner.")).RegisterControlResource(textures.Animation);
                        return frame;
                    }, frame =>
                    {
                        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic requires its current camera owner.")).Apply50A730();
                        return frame;
                    })
                    {
                        ReadCamera = _ => NativeCamera ?? throw new NotSupportedException("Native cinematic requires its current camera globals."),
                        PublishCamera = (frame, cameraState) =>
                        {
                            NativeCamera = cameraState;
                            return frame;
                        },
                        ResolveBoneB65370 = (frame, selector) =>
                        {
                            NativeBattleWaterSlotBank currentBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, assets[1].Data.Length);
                            int found = NativeBattleWaterSlots.LookupB65370(frame.Arena, currentBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, currentBank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
                            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)currentBank.SlotOffset(frame.Arena, found));
                        },
                        ReadGraphicsFlags = _ => graphics.Flags,
                        PublishGraphicsFlags = (frame, value) =>
                        {
                            graphics = graphics with { Flags = value };
                            return frame;
                        }
                    }, program);
                case 0x1B:
                case 0xA2:
                case 0xCD:
                case 0x23:
                case 0x24:
                case 0x2A:
                case 0x3B:
                case 0x3E:
                case 0x40:
                case 0x68:
                case 0xB1:
                    return NativeBattleWaterFieldControl.Execute(current, program, bank, index);
                case 0x58:
                case 0x59:
                    return NativeBattleWaterParticleCommands.Execute(current, program);
                case 0x63:
                case 0x6A:
                case 0xC4:
                case 0x67:
                case 0xD4:
                case 0x104:
                case 0x105:
                case 0x116:
                    return NativeBattleWaterMatrixCommands.Execute(current, program, bank);
                case 0xC3:
                    return NativeBattleScriptedSoundCommands.ExecuteC3(current, (NativeGlobalSoundProcess ?? throw new NotSupportedException("Native cinematic sound control requires its current backend owner.")).Backend);
                case 0xCF:
                    return selectedHandler is 0xAFCBE0 or 0xAF1830 ? NativeBattleWaterContextCommands.ExecuteRing(current, context) : current;
                case 0x96:
                    if (selectedHandler is not (0xB16F60 or 0xAF2760))
                    {
                        return current;
                    }
                    var capture = ExecuteNativeCinematicCapture96(current, program, graphics);
                    graphics = capture.Graphics;
                    return capture.Frame;
                case 0xD5:
                    if (selectedHandler != 0xAFB630)
                    {
                        return current;
                    }
                    ushort encounter = checked((ushort)(definition.EncounterId ?? throw new NotSupportedException("Native Brothers target correction requires its encounter WORD1CFF6E0.")));
                    return NativeBattleBrothersTargetCommand.Execute(current, program, new(encounter, () => RequireWaterMemory().ScriptedTarget2796D40 ?? throw new NotSupportedException("Native Brothers target branch requires its retained2796D40 byte."), value => NativeEffectMemory = RequireWaterMemory() with { ScriptedTarget2796D40 = value }, _ => graphics.Models[3]));
                default:
                    throw new NotSupportedException($"Native cinematic instruction{current.ReadTemporaryWord(0x14A):X4} at{pc:X} requires its selected command contract.");
            }
        }

        NativeBattleWaterExecutionFrame UpdateSlot(NativeBattleWaterExecutionFrame current, byte kind)
        {
            NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "bone_table", kind, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.BoneTable + 4u * kind), 4)));
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            ImmutableArray<byte> active = kind == 3 ? current.Core.Read(0x2797454, 128).ToImmutableArray() : [];
            return current with { Arena = NativeBattleWaterSlots.UpdateSelected(current.Arena, bank, index, active) };
        }

        NativeBattleWaterExecutionFrame DrawSlots(NativeBattleWaterExecutionFrame current, int budget)
        {
            return NativeBattleCinematicDraw.Run(profile, current, PrepareLighting, (frame, kind) =>
            {
                NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "draw_table", kind, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.DrawTable + 4u * kind), 4)));
                if (kind == 4)
                {
                    var overlay = DrawNativeCinematicOverlay(frame, graphics);
                    graphics = overlay.Graphics;
                    return overlay.Frame;
                }
                if (kind == 6)
                {
                    var drawn = DrawNativeCinematicParticles(frame, Program(frame, CoreDword(frame, 0x2798A68)), assets[1].Data.Length, resources, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
                if (kind == 0x15)
                {
                    return NativeBattleCinematicScroll.Draw(frame, resources, (scrollFrame, rectangle, source) =>
                    {
                        NativeBattleEffectTextureState textures = RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic scroll requires its current texture queue.");
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures.EnqueueArenaRectangle505DF0(rectangle, scrollFrame.Arena, ArenaOffset(source, 0)) };
                        return scrollFrame;
                    });
                }
                if (kind is 1 or 2 or 9 or 0x1B)
                {
                    var drawn = DrawNativeCinematicMesh(frame, kind, assets[1].Data.Length, resources, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
                if (kind == 3)
                {
                    uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
                    byte shapeIndex = frame.Arena.Read(ArenaOffset(unchecked(model + 8), 1), 1)[0];
                    uint shape = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex).Selected;
                    var drawn = DrawNativeCinematicModel(frame, assets[1].Data.Length, ModelServices(shape), graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
                return kind == 0x26 ? frame : throw new NotSupportedException($"Native cinematic draw{kind:X2} requires its selected lit geometry contract.");
            }, budget);
        }

        uint ResolveCurrentNode(NativeBattleWaterExecutionFrame frame, short selector)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, assets[1].Data.Length);
            int found = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, found));
        }

        NativeBattleCinematicModelServices ModelServices(uint selectedShape)
        {
            return new((frame, shapeIndex) =>
            {
                selectedShape = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex).Selected;
                return selectedShape;
            }, (frame, animationAddress) =>
            {
                uint extent = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(selectedShape, 4), 4));
                uint relative = unchecked(animationAddress - selectedShape);
                NativeBattleWaterScriptProgram owner = Program(frame, selectedShape);
                if (relative > extent || extent > int.MaxValue || unchecked(selectedShape - owner.Address) > owner.Length || extent > owner.Length - unchecked(selectedShape - owner.Address))
                {
                    throw new NotSupportedException("Native embedded model animation section leaves its current complete source owner.");
                }
                return frame.Arena.Read(ArenaOffset(animationAddress, checked((int)(extent - relative))), checked((int)(extent - relative)));
            });
        }

        NativeBattleWaterExecutionFrame PrepareLighting(NativeBattleWaterExecutionFrame current, sbyte selector)
        {
            NativeBattleScriptedLightingMemory lighting = RequireWaterMemory().ScriptedLighting ?? throw new NotSupportedException("Native cinematic requires its retained light and color register owner.");
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            NativeBattleScriptedLightingStep prepared = NativeBattleScriptedLighting.Prepare(current, lighting, (frame, parent) => NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, parent));
            NativeEffectMemory = RequireWaterMemory() with { ScriptedLighting = prepared.Lighting };
            return prepared.Frame;
        }

        NativeBattleWaterExecutionFrame ClearOrderingTable(NativeBattleWaterExecutionFrame current, uint address, int count)
        {
            if (address != 0x44 || count != 0x1000)
            {
                throw new NotSupportedException("Native cinematic clear requires its selected ordering-table range.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic requires its current ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = primitive.Pending.ClearRange45D550(checked((int)(address / 4)), count) } };
            return current;
        }

        NativeBattleWaterExecutionFrame PublishFocal(NativeBattleWaterExecutionFrame current, short focal)
        {
            NativeCamera = NativeCamera! with { Projection = NativeCamera!.Projection! with { FocalLength = unchecked((ushort)focal) } };
            return current;
        }
    }
}
