using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterInstructionServices(
    Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterScriptProgram> ResolveProgram,
    NativeBattleWaterPresentationServices Presentation,
    NativeBattleWaterContextServices Context,
    Func<NativeBattleWaterExecutionFrame, uint, byte> ReadParameterByte,
    Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel> ReadActor,
    Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterActorAnchorSource> ReadActorAnchor,
    Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, uint> ReadHeaderBase,
    Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, byte, NativeBattleWaterResetCaptureInputs> ReadCaptureInputs,
    Func<NativeBattleWaterExecutionFrame, NativeBattleEffectTextureState> ReadTextures,
    Func<NativeBattleWaterExecutionFrame, NativeBattleEffectTextureState, NativeBattleWaterExecutionFrame> PublishTextures,
    Func<NativeBattleWaterExecutionFrame, NativeBattleWaterFileReadRequest, NativeBattleWaterExecutionFrame> SubmitFileRead,
    Func<NativeBattleWaterExecutionFrame, uint> ReadIncomingEcx)
{
    public uint? MagicTextureBufferAddress { get; init; }
}

/// <summary>Finite instruction orchestration over the current resource arena and explicit engine services.</summary>
public static class NativeBattleWaterInstructionDispatcher
{
    public static ushort ReadInstruction(NativeBattleWaterExecutionFrame frame, uint pc, NativeBattleWaterInstructionServices services)
    {
        NativeBattleWaterScriptProgram program = services.ResolveProgram(frame, pc);
        return BinaryPrimitives.ReadUInt16LittleEndian(ReadProgram(frame, program, pc, 2));
    }

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, ushort opcode, NativeBattleWater222Resources resources, NativeBattleWaterInstructionServices services)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != opcode)
        {
            throw new InvalidDataException("Native instruction dispatch requires its current fetched opcode.");
        }
        switch (opcode)
        {
            case 0:
                return NativeBattleWaterStopCommand.StopRemoveAA5120(frame);
            case 1:
                NativeBattleWaterHeaderNextResult next = NativeBattleWaterHeaderNextCommand.Execute(frame, current => services.ResolveProgram(current, CoreDword(current, 0x2797450)), current => Resource1(current, resources), services.ReadHeaderBase, services.ReadCaptureInputs);
                return next.Frame;
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
                NativeBattleWaterSlotBank controlBank = CurrentBank(frame, resources);
                uint header = opcode == 5 ? CoreDword(frame, 0x279744C) : 0;
                ushort headerFlags = opcode == 5 ? CoreWord(frame, checked(header + 2)) : (ushort)0;
                NativeBattleWaterScriptControlResult control = NativeBattleWaterScriptControl.Execute(frame.Arena, controlBank, CurrentIndex(frame, controlBank), Program(), Pc(), frame.ReadTemporaryByte(0x143), headerFlags);
                frame = frame with { Arena = control.Arena };
                frame = WriteCoreDword(frame, 0x2797450, control.NextInstructionPointer);
                if (opcode == 5)
                {
                    frame = WriteCoreWord(frame, checked(header + 2), control.HeaderFlagsWord2);
                }
                return ApplyWait(frame, control.YieldWait);
            case 6:
            case 8:
            case 0x29:
                NativeBattleWaterSlotBank ioBank = CurrentBank(frame, resources);
                NativeBattleEffectTextureState? textures = opcode == 0x29 && frame.Core.Read(0x2798219, 1)[0] == 0 ? services.ReadTextures(frame) : null;
                NativeBattleWaterIoCommandResult io = NativeBattleWaterIoCommands.Execute(frame.Core, frame.Arena, ioBank, CurrentIndex(frame, ioBank), Program(), Pc(), CoreDword(frame, 0x27973EC), textures);
                frame = frame with { Core = io.Core };
                if (io.Textures is not null)
                {
                    frame = services.PublishTextures(frame, io.Textures);
                }
                if (io.Request is not null)
                {
                    frame = services.SubmitFileRead(frame, io.Request);
                }
                frame = WriteCoreDword(frame, 0x2797450, io.NextInstructionPointer);
                return ApplyWait(frame, io.YieldWait);
            case 0xA:
            case 0xB:
            case 0xC:
            case 0xD:
            case 0xE:
            case 0xF:
            case 0x11:
            case 0x12:
            case 0x15:
            case 0x16:
                return ExecuteVector(frame, Program(), resources);
            case 0x20:
            case 0x22:
            case 0x39:
            case 0xCF:
                return NativeBattleWaterContextCommands.Execute(frame, Program(), services.Context);
            case 0x21:
            case 0x7D:
                return NativeBattleWaterWorkCommands.Execute(frame, Program(), services.MagicTextureBufferAddress);
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
                NativeBattleWaterSlotBank fieldBank = CurrentBank(frame, resources);
                return NativeBattleWaterFieldControl.Execute(frame, Program(), fieldBank, CurrentIndex(frame, fieldBank));
            case 0x25:
            case 0x2F:
            case 0x3C:
            case 0x56:
                NativeBattleWaterSlotBank rectangleBank = CurrentBank(frame, resources);
                NativeBattleEffectHeapAllocation map = new("shape-resource-map", resources.Profile.Tables.Data.AsSpan().Slice(0x60, 0x10).ToArray().ToImmutableArray());
                return NativeBattleWaterRectangleCommands.Execute(frame, Program(), rectangleBank, CurrentIndex(frame, rectangleBank), resources, map);
            case 0x26:
            case 0x32:
            case 0x8B:
                return NativeBattleWaterChildCommands.Execute(frame, Program(), CurrentBank(frame, resources), opcode == 0x32 ? services.ReadIncomingEcx(frame) : 0);
            case 0x27:
                return ExecuteTexturePair(frame, Program(), resources, services);
            case 0x2B:
            case 0x43:
            case 0x44:
            case 0x4D:
            case 0x45:
            case 0x5A:
            case 0x5E:
            case 0xD0:
                NativeBattleWaterSlotBank presentationBank = CurrentBank(frame, resources);
                return NativeBattleWaterPresentationCommands.Execute(frame, Program(), presentationBank, CurrentIndex(frame, presentationBank), services.Presentation);
            case 0x52:
                return NativeBattleWaterTextureMetadata.Execute52AA3A60(frame, resources.Profile);
            case 0x55:
            case 0x61:
            case 0x62:
            case 0x75:
            case 0x76:
            case 0x87:
            case 0x4C:
            case 0xD2:
            case 0xD3:
                NativeBattleWaterScalarReferenceServices scalarServices = new((current, selector) => ResolveCurrentSlot(current, resources, selector), services.ReadParameterByte, services.ReadActor, services.ReadActorAnchor, current => UpdateCurrent(current, resources), current => CacheCurrent(current, resources));
                return NativeBattleWaterScalarReferenceCommands.Execute(frame, Program(), scalarServices);
            case 0x58:
            case 0x59:
                return NativeBattleWaterParticleCommands.Execute(frame, Program());
            case 0x60:
                return NativeBattleWaterActorAnchor.Execute60AA2190(frame, Program(), CurrentBank(frame, resources), services.ReadActorAnchor);
            case 0x63:
            case 0x6A:
            case 0xC4:
            case 0x67:
            case 0xD4:
            case 0x104:
            case 0x105:
            case 0x116:
                return NativeBattleWaterMatrixCommands.Execute(frame, Program(), CurrentBank(frame, resources));
            default:
                throw new NotSupportedException("Native Water instruction is outside its finite selected command set.");
        }

        uint Pc() => CoreDword(frame, 0x2797450);
        NativeBattleWaterScriptProgram Program() => services.ResolveProgram(frame, Pc());
    }

    public static NativeBattleWaterSlotBank CurrentBank(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources)
    {
        NativeBattleWaterInitializationResource file = Resource1(frame, resources);
        return CurrentBank(frame, file.Length);
    }

    public static NativeBattleWaterSlotBank CurrentBank(NativeBattleWaterExecutionFrame frame, int resource1Length)
    {
        if (resource1Length <= 0)
        {
            throw new InvalidDataException("Native scripted slots require their current resource extent.");
        }
        int resourceOffset = ArenaOffset(CoreDword(frame, 0x2798A6C), resource1Length);
        uint manager = CoreDword(frame, 0x27973EC);
        int slotBase = ArenaOffset(CoreDword(frame, checked(manager + 0x90)), 0);
        int capacity = CoreWord(frame, checked(CoreDword(frame, 0x279744C) + 0x38));
        return new(resourceOffset, resource1Length, capacity, slotBase);
    }

    public static int CurrentIndex(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank)
    {
        int relative = ArenaOffset(CoreDword(frame, 0x27973E8), 0x100) - bank.SlotBaseOffset;
        if (relative < 0 || relative % 0x100 != 0)
        {
            throw new InvalidDataException("Native instruction current slot leaves its freshly derived cached bank.");
        }
        int index = relative / 0x100;
        _ = bank.SlotOffset(frame.Arena, index);
        return index;
    }

    public static uint ResolveCurrentSlot(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, short selector)
    {
        NativeBattleWaterSlotBank bank = CurrentBank(frame, resources);
        int found = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, CurrentIndex(frame, bank), ActiveOrder(frame), selector);
        return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, found));
    }

    public static NativeBattleWaterExecutionFrame UpdateCurrent(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources)
    {
        return UpdateCurrent(frame, resources.Files[1].Data.Length);
    }

    public static NativeBattleWaterExecutionFrame UpdateCurrent(NativeBattleWaterExecutionFrame frame, int resource1Length)
    {
        NativeBattleWaterSlotBank bank = CurrentBank(frame, resource1Length);
        ImmutableArray<byte> active = ReadSlot(frame, 0x18, 1)[0] is 3 or 7 ? ActiveOrder(frame) : [];
        return frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, bank, CurrentIndex(frame, bank), active) };
    }

    public static NativeBattleWaterExecutionFrame CacheCurrent(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources)
    {
        return CacheCurrent(frame, resources.Files[1].Data.Length);
    }

    public static NativeBattleWaterExecutionFrame CacheCurrent(NativeBattleWaterExecutionFrame frame, int resource1Length)
    {
        NativeBattleWaterSlotBank bank = CurrentBank(frame, resource1Length);
        return frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, CurrentIndex(frame, bank)) };
    }

    private static NativeBattleWaterExecutionFrame ExecuteVector(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWater222Resources resources)
    {
        return ExecuteVector(frame, program, resources.Files[1].Data.Length);
    }

    public static NativeBattleWaterExecutionFrame ExecuteVector(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, int resource1Length, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame>? updateCurrent = null)
    {
        uint pc = CoreDword(frame, 0x2797450);
        uint relative = unchecked(pc - program.Address);
        if (relative > int.MaxValue)
        {
            throw new InvalidDataException("Native vector instruction leaves its actual program view.");
        }
        int slot = SlotOffset(frame, 0, 0x100);
        NativeBattleWaterVectorStep vector = NativeBattleWaterVectorCommands.ExecuteAA1810((offset, count) => frame.Arena.Read(slot + offset, count), (offset, bytes) => frame = frame with { Arena = frame.Arena.Write(slot + offset, bytes) }, offset => BinaryPrimitives.ReadUInt16LittleEndian(ReadProgram(frame, program, unchecked(program.Address + (uint)offset), 2)), (int)relative, frame.Random);
        frame = frame with { Random = vector.Random };
        frame = WriteCoreDword(frame, 0x2797450, unchecked(program.Address + (uint)vector.NextOffset));
        if (vector.PostAction == NativeBattleWaterVectorPostAction.RefreshPosition)
        {
            frame = CacheCurrent(updateCurrent is null ? UpdateCurrent(frame, resource1Length) : updateCurrent(frame), resource1Length);
        }
        else if (vector.PostAction == NativeBattleWaterVectorPostAction.RefreshAcceleration)
        {
            NativeBattleWaterSlotBank bank = CurrentBank(frame, resource1Length);
            frame = frame with { Arena = NativeBattleWaterSlots.RefreshAccelerationAA27F0(frame.Arena, bank, CurrentIndex(frame, bank)) };
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame ExecuteTexturePair(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWater222Resources resources, NativeBattleWaterInstructionServices services)
    {
        if (frame.Core.Read(0x2798219, 1)[0] != 0)
        {
            return frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8));
        }
        uint pc = CoreDword(frame, 0x2797450);
        short texture = BinaryPrimitives.ReadInt16LittleEndian(ReadProgram(frame, program, unchecked(pc + 2), 2));
        uint manager = CoreDword(frame, 0x27973EC);
        byte count = frame.Core.Read(checked(manager + 0xD2), 1)[0];
        ImmutableArray<NativeBattleEffectHeapAllocation> currentFiles = Enumerable.Range(0, 2).Select(index =>
        {
            int length = resources.Files[index].Data.Length;
            int offset = ArenaOffset(CoreDword(frame, 0x2798A68 + (uint)(index * 4)), length);
            return new NativeBattleEffectHeapAllocation("current-texture-source", frame.Arena.Read(offset, length).ToImmutableArray());
        }).ToImmutableArray();
        NativeBattleWater222TextureStep step = NativeBattleWater222TexturePair.Transfer27(resources.Profile, currentFiles, texture, 0, count, pc, SlotWord(frame, 0xC8), Submit);
        frame = WriteCoreDword(frame, 0x2797450, step.InstructionPointer);
        return ApplyWait(frame, step.Wait);

        void Submit(NativeBattleWater222UploadPlan plan)
        {
            count = unchecked((byte)(count + 1));
            frame = frame with { Core = frame.Core.Write(checked(manager + 0xD2), new byte[] { count }) };
            uint source = CoreDword(frame, 0x2798A68 + (uint)(plan.SourceIndex * 4));
            int offset = ArenaOffset(unchecked(source + (uint)plan.Offset), 0);
            NativeBattleEffectTextureState queued = services.ReadTextures(frame).EnqueueArenaRectangle505DF0(plan.Rectangle, frame.Arena, offset);
            frame = services.PublishTextures(frame, queued);
        }
    }

    private static NativeBattleWaterExecutionFrame ApplyWait(NativeBattleWaterExecutionFrame frame, ushort? wait) => wait is ushort value ? frame.WriteTemporaryWord(0x13E, value) : frame;

    private static NativeBattleWaterInitializationResource Resource1(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources)
    {
        if (resources.Files.IsDefault || resources.Files.Length != 2)
        {
            throw new InvalidDataException("Native instruction slot-bank view requires its actual second resource extent.");
        }
        uint address = CoreDword(frame, 0x2798A6C);
        int length = resources.Files[1].Data.Length;
        return new(ArenaOffset(address, length), length, address);
    }

    private static ImmutableArray<byte> ActiveOrder(NativeBattleWaterExecutionFrame frame)
    {
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int index = 0; index < order.Length; index++)
        {
            order[index] = frame.Core.Read(0x2797454 + (uint)index, 1)[0];
            if (order[index] == 0xFF)
            {
                return order.ToImmutableArray();
            }
        }
        throw new NotSupportedException("Native instruction reference lookup requires its bounded terminated active order.");
    }

    private static byte[] ReadProgram(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, uint address, int count)
    {
        uint relative = unchecked(address - program.Address);
        if (program.ArenaOffset < 0 || program.Length < count || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - count))
        {
            throw new InvalidDataException("Native instruction fetch leaves its current bounded program view.");
        }
        return frame.Arena.Read(checked(program.ArenaOffset + (int)relative), count);
    }
}
