using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeFadeNone(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame);

public sealed partial class EventEngine
{
    private string? UnsupportedFadeNoneAccess(JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.FADENONE)
        {
            if (FieldId < 72)
            {
                return "Native FADENONE requires an ordinary field";
            }
            foreach ((int address, int value) in variables)
            {
                if (address >= 213 && address <= 217 && !(address == 216 && Scene.FadeMode216 == value) && !(address < 216 && IsKnownNativeByteAlias(address, value)))
                {
                    return "Native FADENONE intersects an ambiguous bare W216 alias; original PC and state preserved";
                }
            }
            return null;
        }
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        if (Scene.FadeMode216 is null || width == 0 || operation.Parameter > 217 || (long)operation.Parameter + width - 1 < 216)
        {
            return null;
        }
        return operation.Parameter == 216 && operation.Opcode is Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W ? null : "Known native fade mode requires exact WORD216 reads; unknown mixed-width writes preserve PC and operands";
    }

    private void ExecuteFadeNone(Fiber fiber)
    {
        Scene.FadeMode216 = 0;
        variables[216] = 0;
        Scene.FadeOpacity = 0;
        Scene.FadeNone = new NativeFadeNone(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame);
        // Native mode0 suppresses overlay; its counter and rate are retained.
    }

    private void InvalidateFadeModeAuthority()
    {
        if (Scene.FadeMode216 is ushort mode && variables.TryGetValue(216, out int alias) && alias == mode)
        {
            variables.Remove(216);
        }
        Scene.FadeMode216 = null;
    }

    private void ValidateFadeNoneCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.FadeMode216 is ushort mode && (mode != 0 || saved.Scene.FadeOpacity != 0 || saved.Variables.TryGetValue(216, out int alias) && alias != mode))
        {
            throw new InvalidDataException("Saved known native fade mode disagrees with WORD216 or its zero overlay.");
        }
        if (saved.Scene.FadeNone is not NativeFadeNone none)
        {
            return;
        }
        if (none.Version != 1 || none.FieldId < 72 || none.ObjectId < 0 || none.ScriptId < 0 || none.Operation < 0 || none.Frame < 0)
        {
            throw new InvalidDataException("Saved FADENONE lost its original zero-operand provenance.");
        }
        if (none.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == none.ObjectId)?.Scripts.FirstOrDefault(script => script.Id == none.ScriptId);
            if (script is null || none.Operation < script.Start || none.Operation >= script.End || program.Operations[none.Operation].Opcode != Jsm.Opcode.FADENONE)
            {
                throw new InvalidDataException("Saved FADENONE does not match its original instruction.");
            }
        }
    }
}
