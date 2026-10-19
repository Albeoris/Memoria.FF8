using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeIfritB25780(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Ifrit entry requires its selected effect.");
        NativeBattleScriptedInitializationDirectories directories = effect.Binding.ScriptedInitializationDirectories ?? throw new NotSupportedException("Native Ifrit requires its original initialization and owner directories.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Ifrit requires its actual action parameters.");
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
        if (assets.Length != 2)
        {
            throw new NotSupportedException("Native Ifrit requires both complete executed initializer resources.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        ImmutableArray<byte> contacts = WaterContacts(header);
        if (contacts.IsEmpty)
        {
            throw new NotSupportedException("Native Ifrit entry requires its first actual contact actor.");
        }
        NativeBattleWaterInitializationInputs inputs = new(Resource(0, 0x2796E4C), Resource(1, 0x2796E48), NativeBattleWater222Controller.CreateTemporaryFrame(WaterTemporaryAddress), WaterParametersAddress, WaterHeadersAddress + checked((uint)(20 * parameters.HeaderIndex7C)), 0x1874894, parameters.Actor78, parameters.HeaderIndex7C, contacts[0], contacts, header.ReadByte(0x10), header.ReadByte(0x11), graphics.Models);
        NativeBattleIfritEntryStep initialized = NativeBattleIfritEntry.InitializeB25780(RequireWaterMemory(), effect, directories, inputs);
        NativeEffectMemory = initialized.Memory;
        return graphics with { OriginalEffect = initialized.Effect };

        NativeBattleWaterInitializationResource Resource(int index, uint publication)
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == publication) ?? throw new NotSupportedException("Native Ifrit requires its actual initializer resource publication.");
            return new(pointer.ArenaOffset, assets[index].Data.Length, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        }
    }

    private NativeBattleGraphicsState AdvanceNativeIfritB25DF0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleCinematicResources resources = effect.Binding.CinematicResources ?? throw new NotSupportedException("Native Ifrit requires its authenticated cinematic tables and resources.");
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
        if (assets.Length != 2)
        {
            throw new NotSupportedException("Native Ifrit callback requires both current resource extents.");
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Ifrit requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Ifrit requires its current projection.");
        NativeBattlePrimitiveProcess process = RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native Ifrit requires its actual COMMON process.");
        NativeBattleScriptedCallbackInput input = new(effect.Binding.ScriptedInitializationDirectories ?? throw new NotSupportedException("Native Ifrit requires its original directories."), assets[1].Data.Length, checked((ushort)graphics.Flags), MatrixBytes(projection.View ?? throw new NotSupportedException("Native Ifrit requires its current view matrix.")), 0, checked((uint)(process.ByteCursor ?? throw new NotSupportedException("Native Ifrit COMMON cursor is unknown."))), camera.FocalLength ?? throw new NotSupportedException("Native Ifrit requires its retained focal WORD."));
        NativeBattleScriptedCallbackServices services = new(ReadInstruction, ExecuteInstruction, UpdateSlot, DrawSlots, ClearOrderingTable, PublishFocal) { ReadCurrentFocal = _ => NativeCamera?.FocalLength ?? throw new NotSupportedException("Native Ifrit requires its current scratch focal WORD after script execution.") };
        NativeBattleIfrit201Frame advanced = NativeBattleIfrit201Controller.AdvanceB25DF0(new(RequireWaterMemory(), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Ifrit requires its retained CRT state.")), input, services, WaterTemporaryAddress, RequireWaterMemory);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

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
            throw new NotSupportedException("Native Ifrit instruction leaves its current resource owners.");
        }

        ushort ReadInstruction(NativeBattleWaterExecutionFrame current, uint address)
        {
            NativeBattleWaterScriptProgram program = Program(current, address);
            int offset = checked((int)(address - program.Address));
            if (offset > program.Length - 2)
            {
                throw new InvalidDataException("Native Ifrit instruction leaves its complete WORD operand owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(current.Arena.Read(program.ArenaOffset + offset, 2));
        }

        NativeBattleWaterExecutionFrame ExecuteInstruction(NativeBattleWaterExecutionFrame current, ushort opcode)
        {
            uint pc = CoreDword(current, 0x2797450);
            NativeBattleWaterScriptProgram program = Program(current, pc);
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            switch (opcode)
            {
                case 0:
                    return NativeBattleWaterStopCommand.StopRemoveAA5120(current);
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
                case >= 0xA and <= 0x17:
                    return NativeBattleWaterInstructionDispatcher.ExecuteVector(current, program, assets[1].Data.Length);
                case 7:
                case 0xA0:
                    return NativeBattleScriptedSchedulingCommands.Execute(current, program);
                case 0x26:
                case 0x32:
                case 0x8B:
                    return NativeBattleWaterChildCommands.Execute(current, program, bank, opcode == 0x32 ? checked(current.TemporaryFrame.Address + 0x100) : 0);
                case 0x6D:
                    NativeBattleScriptedProcessFlagsStep flags = NativeBattleScriptedProcessFlags.Execute(current, RequireWaterMemory().ScriptedProcessFlags ?? throw new NotSupportedException("Native cinematic flags require their retained four-row process owner."));
                    NativeEffectMemory = RequireWaterMemory() with { ScriptedProcessFlags = flags.State };
                    return flags.Frame;
                case 0x2B:
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
                    NativeBattleScriptedStreamStep streamed = NativeBattleScriptedStreamCommands.Execute(current, RequireWaterMemory().SummonStream ?? throw new NotSupportedException("Native Ifrit requires its retained summon-stream owner."), (_, address, length) => resources.ReadStatic(address, length).ToArray(), _ => throw new NotSupportedException("Native cinematic stream transfer requires its retained external clearing flag."));
                    NativeEffectMemory = RequireWaterMemory() with { SummonStream = streamed.Stream };
                    return streamed.Frame;
                case 0x27:
                case 0x28:
                case 0x29:
                    NativeBattleCinematicTextureCommandStep transferred = NativeBattleCinematicTextureCommands.Execute(current, program, resources, RequireWaterMemory().Textures ?? throw new NotSupportedException("Native Ifrit requires its current texture queue."));
                    NativeEffectMemory = RequireWaterMemory() with { Textures = transferred.Textures };
                    return transferred.Frame;
                case 6:
                case 8:
                    NativeBattleCinematicIoStep io = NativeBattleCinematicIoCommands.Execute(current, program, resources);
                    if (io.Request is not null)
                    {
                        NativeEffectMemory = RequireWaterMemory() with { Arena = io.Frame.Arena, ScriptedEffects = io.Frame.Core };
                        NativeBattleWaterIoQueued queued = NativeBattleCinematicIoCommands.CopyAndQueue(RequireWaterMemory(), NativeFileIo ?? throw new NotSupportedException("Native Ifrit requires its retained file IO scheduler."), io.Request, resources);
                        NativeEffectMemory = queued.Memory;
                        NativeFileIo = queued.FileIo;
                        return io.Frame with { Arena = queued.Memory.Arena!, Core = queued.Memory.ScriptedEffects! };
                    }
                    return io.Frame;
                case 0x39 when current.ReadTemporaryWord(0x14A) >> 12 is 2 or 3:
                    NativeBattleScriptedVibrationStep vibration = NativeBattleScriptedVibrationCommands.Execute(current, program, NativeVibrationProcess ?? throw new NotSupportedException("Native Ifrit requires its retained vibration process."), (frame, address) =>
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
                        NativeBattleEffectTextureState textures = (RequireWaterMemory().Textures ?? throw new NotSupportedException("Native Ifrit camera requires its current texture control owner.")).Register5099A0(animation);
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures };
                        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Ifrit requires its current camera owner.")).RegisterControlResource(textures.Animation);
                        return frame;
                    }, frame =>
                    {
                        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Ifrit requires its current camera owner.")).Apply50A730();
                        return frame;
                    })
                    {
                        ReadCamera = _ => NativeCamera ?? throw new NotSupportedException("Native Ifrit requires its current camera globals."),
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
                case 0x67:
                case 0xD4:
                case 0x104:
                case 0x105:
                    return NativeBattleWaterMatrixCommands.Execute(current, program, bank);
                case 0xCF:
                    return current;
                default:
                    throw new NotSupportedException($"Native Ifrit instruction{current.ReadTemporaryWord(0x14A):X4} at{pc:X} requires its selected command contract.");
            }
        }

        NativeBattleWaterExecutionFrame UpdateSlot(NativeBattleWaterExecutionFrame current, byte kind)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            ImmutableArray<byte> active = kind == 3 ? current.Core.Read(0x2797454, 128).ToImmutableArray() : [];
            return current with { Arena = NativeBattleWaterSlots.UpdateSelected(current.Arena, bank, index, active) };
        }

        NativeBattleWaterExecutionFrame DrawSlots(NativeBattleWaterExecutionFrame current, int budget)
        {
            return NativeBattleIfritDraw.RunB2ABE0(current, PrepareLighting, (frame, kind) => kind == 0x26 ? frame : throw new NotSupportedException($"Native Ifrit draw{kind:X2} requires its selected lit geometry contract."), budget);
        }

        NativeBattleWaterExecutionFrame PrepareLighting(NativeBattleWaterExecutionFrame current, sbyte selector)
        {
            NativeBattleScriptedLightingMemory lighting = RequireWaterMemory().ScriptedLighting ?? throw new NotSupportedException("Native Ifrit requires its retained light and color register owner.");
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            NativeBattleScriptedLightingStep prepared = NativeBattleScriptedLighting.Prepare(current, lighting, (frame, parent) => NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, parent));
            NativeEffectMemory = RequireWaterMemory() with { ScriptedLighting = prepared.Lighting };
            return prepared.Frame;
        }

        NativeBattleWaterExecutionFrame ClearOrderingTable(NativeBattleWaterExecutionFrame current, uint address, int count)
        {
            if (address != 0x44 || count != 0x1000)
            {
                throw new NotSupportedException("Native Ifrit clear requires its selected ordering-table range.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Ifrit requires its current ordering table.");
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
