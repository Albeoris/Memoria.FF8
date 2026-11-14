using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Advances the unsigned relative action index and refreshes its actual actor context.</summary>
public static class NativeBattleCinematicNextActionAE
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> captureCurrent)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0xAE)
        {
            throw new NotSupportedException("Native cinematic next action requires commandAE.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        byte current = frame.Core.Read(manager + 0xD0, 1)[0];
        byte limit = frame.Core.Read(manager + 0xD1, 1)[0];
        if (current >= limit)
        {
            short relative = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
            return NativeBattleScriptedCommandReads.Advance(frame, unchecked((uint)(int)relative));
        }
        frame = frame with { Core = frame.Core.Write(manager + 0xD0, new[] { unchecked((byte)(current + 1)) }) };
        frame = captureCurrent(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame CaptureCurrent(NativeBattleWaterExecutionFrame frame, int resourceLength, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readActionBytes, ImmutableArray<NativeBattleGraphicsModel> graphics, NativeBattleActionPacketsState? packets = null)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint parameters = CoreDword(frame, manager + 0xC0);
        byte relative = frame.Core.Read(manager + 0xD0, 1)[0];
        uint first = Dword(parameters + 4);
        uint selected = unchecked(first + (uint)(20 * relative));
        frame = WriteCoreDword(frame, manager + 0xCC, selected);
        byte caster = Read(parameters, 1)[0];
        uint firstContacts = Dword(first + 8);
        byte firstActor = Read(firstContacts, 1)[0];
        uint contacts = Dword(selected + 8);
        int count = NativeBattleCinematicContacts.ReadCount(frame, selected, readActionBytes, packets, signedNative: false);
        ImmutableArray<byte>.Builder actors = ImmutableArray.CreateBuilder<byte>(count);
        for (int index = 0; index < count; index++)
        {
            actors.Add(Read(NativeBattleCinematicContacts.Offset(contacts, index, packets), 1)[0]);
        }
        if (CoreDword(frame, 0x279744C) != NativeBattleScriptedEffectMemory.Address || selected < 0x1D280C4 || (selected - 0x1D280C4) % 20 != 0 || (selected - 0x1D280C4) / 20 >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new NotSupportedException("Native cinematic action capture requires its actual shared header and selected physical action row.");
        }
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        int indexInBank = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank);
        NativeBattleWaterHeaderCaptureResult capture = NativeBattleWaterHeaderCapture.CaptureAA4E90(frame.Core.ReadHeader(), frame.Arena, bank, indexInBank, caster, checked((int)((selected - 0x1D280C4) / 20)), firstActor, actors.MoveToImmutable(), count, graphics);
        return frame with { Core = frame.Core.WithHeader(capture.Header), Arena = capture.Arena };

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

        byte[] Read(uint address, int length) => NativeBattleCinematicContacts.ReadBytes(frame, address, length, readActionBytes);
    }
}
