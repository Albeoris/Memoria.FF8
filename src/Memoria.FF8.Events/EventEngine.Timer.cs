using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeExamTimerState? ExamTimer => services.ExamTimer;

    private string? UnsupportedTimerAccess(Fiber fiber, JsmOperation operation)
    {
        bool timerCommand = operation.Opcode is Jsm.Opcode.SETTIMER or Jsm.Opcode.DISPTIMER or Jsm.Opcode.GETTIMER or Jsm.Opcode.KILLTIMER;
        if (!timerCommand && services.ExamTimer is null)
        {
            return null;
        }
        if (timerCommand)
        {
            if (FieldId < 72)
            {
                return "Native exam timer commands require an ordinary field";
            }
            int operands = operation.Opcode == Jsm.Opcode.SETTIMER ? 1 : operation.Opcode == Jsm.Opcode.DISPTIMER ? 2 : 0;
            if (fiber.Stack.Count < operands)
            {
                return "Native exam timer command requires its original operands";
            }
            if (operation.Opcode != Jsm.Opcode.SETTIMER && services.ExamTimer is null)
            {
                return "Native exam timer remaining DWORD has no reached SETTIMER provenance";
            }
            if (variables.Keys.Any(address => address is >= 101 and <= 107 or >= 179 and <= 183))
            {
                return "Native exam timer flags overlap legacy memory aliases with unknown width; original PC and operands retained";
            }
            if (operation.Opcode == Jsm.Opcode.DISPTIMER && variables.Keys.Any(address => address is >= 202 and <= 206))
            {
                return "DISPTIMER coordinates overlap legacy memory aliases with unknown width; original PC and operands retained";
            }
            return null;
        }
        if (operation.Opcode == Jsm.Opcode.MOVIE && services.NativeMovieClockFlag is null)
        {
            return "A tracked native timer requires the accepted movie start/close lifecycle flag before movie departure";
        }
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        long last = (long)operation.Parameter + width - 1;
        if (width != 0 && (operation.Parameter <= 104 && last >= 104 || operation.Parameter <= 183 && last >= 182 || operation.Parameter <= 206 && last >= 205))
        {
            return "Native timer memory access requires unproved surrounding flag bits; typed timer masks cannot supply an entire byte/word/DWORD";
        }
        return null;
    }

    private int[] ExecuteTimer(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeExamTimerState? current = services.ExamTimer;
        if (opcode == Jsm.Opcode.GETTIMER)
        {
            fiber.Results[0] = NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, current!));
            return [];
        }
        int[] operands = opcode == Jsm.Opcode.DISPTIMER ? fiber.Stack.Take(2).Reverse().ToArray() : opcode == Jsm.Opcode.SETTIMER ? [fiber.Stack.Peek()] : [];
        NativeExamTimerState next = opcode switch
        {
            Jsm.Opcode.SETTIMER => current?.SetRemaining(operands[0]) ?? NativeExamTimerState.FromReachedSet(operands[0]),
            Jsm.Opcode.DISPTIMER => current!.Display(unchecked((byte)operands[0]), unchecked((byte)operands[1])),
            Jsm.Opcode.KILLTIMER => current!.KillDisplay(),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
        if (opcode == Jsm.Opcode.SETTIMER)
        {
            next = ProjectBattleModeTimer(next);
        }
        try
        {
            services.SetExamTimer(next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native exam timer service rejected the update; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode.ToString(), message);
            throw new EventScriptException(message, exception);
        }
        RecordBattleModeTimerWrite(fiber, opcode);
        return Pop(fiber, operands.Length);
    }

    private void ValidateNativeTimerExpiryAfterVm()
    {
        if (services.ExamTimer is not NativeExamTimerState timer || timer.Event104Mask40 == false || timer.Event183Mask1 == true || timer.Alternatives.All(item => item.Remaining != 0))
        {
            return;
        }
        // 477069 evaluates expiry after 529FF0, so SET/KILL in this VM tick can suppress it.
        if (timer.Alternatives.Any(item => item.Remaining != 0))
        {
            throw new NotSupportedException("Native timer expiry is ambiguous between zero and nonzero remaining DWORDs; no field transition published.");
        }
        if (timer.Event104Mask40 is null || timer.Event183Mask1 is null)
        {
            throw new NotSupportedException("Native timer expiry requires proved DWORD104 mask40 and byte183 mask1; no field transition published.");
        }
        throw new NotSupportedException("Native timer expired: field75 kind1 triangle7FFF requires preserved original X/Y/direction packet globals; no fabricated arrival published.");
    }
}
