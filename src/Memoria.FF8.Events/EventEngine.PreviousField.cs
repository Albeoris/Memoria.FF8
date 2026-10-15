using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidatePreviousFieldLoad(int? previousFieldId)
    {
        if (previousFieldId is null)
        {
            return;
        }
        if (previousFieldId is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(previousFieldId), "Native previous field must fit its unsigned WORD.");
        }
        foreach ((int address, NativeMemoryValue value) in EventMemory.Cells)
        {
            if (address < 86 && (long)address + value.Width > 84)
            {
                throw new NotSupportedException($"Native previous-field load intersects ambiguous alias {address}={value}, current WORD84={Scene.PreviousFieldWord84}; the current field is preserved.");
            }
        }
    }

    private void InitializePreviousField(int? previousFieldId)
    {
        if (previousFieldId is int known)
        {
            SetPreviousFieldWord((ushort)known);
        }
    }

    private void SetPreviousFieldWord(ushort value)
    {
        Scene.PreviousFieldWord84 = value;
    }

    private bool TryReadPreviousFieldWord(JsmOperation operation, out int value)
    {
        value = Scene.PreviousFieldWord84.GetValueOrDefault();
        return Scene.PreviousFieldWord84 is not null && operation.Parameter == 84 && operation.Opcode is Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W;
    }

    private string? UnsupportedPreviousFieldAccess(JsmOperation operation)
    {
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        if (width == 0 || operation.Parameter > 85 || (long)operation.Parameter + width - 1 < 84)
        {
            return null;
        }
        bool exact = operation.Parameter == 84 && operation.Opcode is Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W;
        if (Scene.PreviousFieldWord84 is null && !(operation.Opcode == Jsm.Opcode.POPM_W && operation.Parameter == 84))
        {
            // Unallocated diagnostic memory remains generic; field identity
            // does not establish previous-field provenance.
            return null;
        }
        if (!exact)
        {
            return "Native previous-field WORD84 requires an exact WORD access; overlapping widths preserve PC and operands";
        }
        foreach ((int address, NativeMemoryValue value) in EventMemory.Cells)
        {
            if (address < 86 && (long)address + value.Width > 84)
            {
                return "Native previous-field WORD84 intersects an ambiguous alias; PC and operands preserved";
            }
        }
        return null;
    }

    private void ValidatePreviousFieldCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.PreviousFieldWord84 is not null && saved.EventMemory.Cells.Any(pair => pair.Key < 86 && (long)pair.Key + pair.Value.Width > 84))
        {
            throw new InvalidDataException("Saved previous-field WORD84 disagrees with its exact native memory authority.");
        }
    }
}
