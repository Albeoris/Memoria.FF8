using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeExamTimerState? ExamTimer => services.ExamTimer;

    private string? UnsupportedTimerAccess(Fiber fiber, JsmOperation operation)
    {
        bool timerCommand = operation.Opcode is Jsm.Opcode.SETTIMER or Jsm.Opcode.DISPTIMER or Jsm.Opcode.GETTIMER or Jsm.Opcode.KILLTIMER;
        if (operation.Opcode == Jsm.Opcode.MOVIE && services.ExamTimer is not null && services.NativeMovieClockFlag is null)
        {
            return "A tracked native process clock requires the accepted movie start/close lifecycle flag before movie departure";
        }
        if (!timerCommand && services.ExamTimer is not { TimerMemoryActivated: true })
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
            if (services.ExamTimer is null)
            {
                return "Native exam timer has no preserved process clock or remaining DWORD provenance";
            }
            if (operation.Opcode is Jsm.Opcode.DISPTIMER or Jsm.Opcode.KILLTIMER && (services.WorldControls is null || !services.CanStoreNativeEventOwners))
            {
                return "Native timer display requires its DWORD104 owner and atomic flag/timer publication";
            }
            if (EventMemory.Overlaps(103, 5) || EventMemory.Overlaps(182, 2))
            {
                return "Native exam timer flags overlap legacy memory aliases with unknown width; original PC and operands retained";
            }
            if (operation.Opcode == Jsm.Opcode.DISPTIMER && EventMemory.Overlaps(205, 2))
            {
                return "DISPTIMER coordinates overlap legacy memory aliases with unknown width; original PC and operands retained";
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
        long last = (long)operation.Parameter + width - 1;
        if (width != 0 && (operation.Parameter <= 183 && last >= 182 || operation.Parameter <= 206 && last >= 205))
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
            Jsm.Opcode.SETTIMER => current!.SetRemaining(operands[0]),
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
            if (opcode is Jsm.Opcode.DISPTIMER or Jsm.Opcode.KILLTIMER)
            {
                NativeWorldControlState controls = services.WorldControls!;
                uint flags = opcode == Jsm.Opcode.DISPTIMER ? controls.EventFlags104 | 0x40u : controls.EventFlags104 & ~0x40u;
                services.SetNativeEventOwners(controls with { EventFlags104 = flags }, timer: next);
            }
            else
            {
                services.SetExamTimer(next);
            }
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
        if (services.ExamTimer is not { TimerMemoryActivated: true } timer || services.WorldControls is { } controls && (controls.EventFlags104 & 0x40) == 0 || timer.Event183Mask1 == true || timer.Alternatives.All(item => item.Remaining != 0))
        {
            return;
        }
        // 477069 evaluates expiry after 529FF0, so SET/KILL in this VM tick can suppress it.
        if (timer.Alternatives.Any(item => item.Remaining != 0))
        {
            throw new NotSupportedException("Native timer expiry is ambiguous between zero and nonzero remaining DWORDs; no field transition published.");
        }
        if (services.WorldControls is null || timer.Event183Mask1 is null)
        {
            throw new NotSupportedException("Native timer expiry requires proved DWORD104 mask40 and byte183 mask1; no field transition published.");
        }
        throw new NotSupportedException("Native timer expired: field75 kind1 triangle7FFF requires preserved original X/Y/direction packet globals; no fabricated arrival published.");
    }
}
