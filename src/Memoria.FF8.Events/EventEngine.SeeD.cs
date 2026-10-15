using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSeeD(Fiber fiber, JsmOperation operation)
    {
        bool command = operation.Opcode is Jsm.Opcode.ADDSEEDLEVEL or Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF or Jsm.Opcode.SARALYDISPON or Jsm.Opcode.SARALYDISPOFF;
        int width = NativeMemoryWidth(operation.Opcode);
        bool rankMemory = width != 0 && (operation.Parameter < 12 && (long)operation.Parameter + width > 8 || operation.Parameter < 20 && (long)operation.Parameter + width > 16 || operation.Parameter < 188 && (long)operation.Parameter + width > 184);
        bool flagsMemory = width != 0 && (operation.Parameter < 108 && (long)operation.Parameter + width > 103 || operation.Parameter <= 207 && (long)operation.Parameter + width > 207);
        bool salary = operation.Opcode is Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF or Jsm.Opcode.SARALYDISPON or Jsm.Opcode.SARALYDISPOFF;
        if (!command && !rankMemory && !flagsMemory)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return "Native rank/event flags require the verified original runtime profile";
        }
        if (salary || flagsMemory)
        {
            if (services.WorldControls is not NativeWorldControlState controls)
            {
                return "Native BYTE103/DWORD104 requires its executed New Game52CA90 owner; no flags are inferred";
            }
            controls.Validate();
            string? aliases = UnsupportedWorldControlAliases();
            if (aliases is not null)
            {
                return aliases;
            }
            if (flagsMemory && !(operation.Parameter >= 103 && (long)operation.Parameter + width <= 108 || operation.Parameter == 207 && width == 1))
            {
                return "Native BYTE103/DWORD104 projection cannot consume unknown surrounding bytes";
            }
        }
        if (!salary && !flagsMemory || operation.Opcode is Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF)
        {
            if (services.SeeD is not NativeSeeDState state || !services.CanStoreSeeD)
            {
                return "Native SeeD rank/process UI requires executed initialization and a persistent service";
            }
            state.Validate();
        }
        if (operation.Opcode is Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF && !services.CanStoreNativeEventOwners)
        {
            return "Native salary requires atomic DWORD104/process UI publication";
        }
        if (rankMemory && (EventMemory.Overlaps(8, 4) || EventMemory.Overlaps(16, 4) || EventMemory.Overlaps(184, 4)))
        {
            return "Native SeeD WORD16 overlaps an unproved VM memory alias; the snapshot is missing required native state";
        }
        bool operand = operation.Opcode == Jsm.Opcode.ADDSEEDLEVEL || operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        if (operand && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact))
        {
            return "Native SeeD update requires its exact original DWORD operand before mutation";
        }
        if (rankMemory && !(operation.Parameter >= 8 && (long)operation.Parameter + width <= 12 || operation.Parameter >= 16 && (long)operation.Parameter + width <= 20 || operation.Parameter >= 184 && (long)operation.Parameter + width <= 188))
        {
            return "Native SeeD projection supports counter8..11, rank/kill-baseline16..19 or display/previous-rank184..187; surrounding memory is unproved";
        }
        return null;
    }

    private bool TryReadNativeOwnedEventMemory(JsmOperation operation, out int value)
    {
        value = 0;
        int width = NativeMemoryWidth(operation.Opcode);
        if (services.WorldControls is NativeWorldControlState musicControls && operation.Parameter == 207 && width == 1)
        {
            value = musicControls.MusicKeep207;
            return true;
        }
        if (services.WorldControls is NativeWorldControlState controls && width != 0 && operation.Parameter >= 103 && (long)operation.Parameter + width <= 108)
        {
            value = controls.ReadEventBytes(operation.Parameter, width);
            return true;
        }
        if (services.SeeD is not NativeSeeDState state)
        {
            return false;
        }
        if (width != 0 && (operation.Parameter >= 8 && (long)operation.Parameter + width <= 12 || operation.Parameter >= 16 && (long)operation.Parameter + width <= 20 || operation.Parameter >= 184 && (long)operation.Parameter + width <= 188))
        {
            value = state.ReadEventWords(operation.Parameter, width);
            return true;
        }
        return false;
    }

    private bool TryWriteNativeOwnedEventMemory(Fiber fiber, JsmOperation operation, out int[] operands)
    {
        operands = [];
        bool write = operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        int width = NativeMemoryWidth(operation.Opcode);
        if (write && services.WorldControls is NativeWorldControlState musicControls && operation.Parameter == 207 && width == 1)
        {
            SetNativeWorldControls(fiber, musicControls with { MusicKeep207 = unchecked((byte)fiber.Stack.Peek()) });
            operands = Pop(fiber, 1);
            return true;
        }
        if (write && services.WorldControls is NativeWorldControlState controls && operation.Parameter >= 103 && (long)operation.Parameter + width <= 108)
        {
            SetNativeWorldControls(fiber, controls.WriteEventBytes(operation.Parameter, width, fiber.Stack.Peek()));
            operands = Pop(fiber, 1);
            return true;
        }
        if (services.SeeD is not NativeSeeDState state || !write || !(operation.Parameter >= 8 && (long)operation.Parameter + width <= 12 || operation.Parameter >= 16 && (long)operation.Parameter + width <= 20 || operation.Parameter >= 184 && (long)operation.Parameter + width <= 188))
        {
            return false;
        }
        int raw = fiber.Stack.Peek();
        PublishNativeSeeD(fiber, state.WriteEventWords(operation.Parameter, width, raw));
        operands = Pop(fiber, 1);
        return true;
    }

    private int[] ExecuteAddSeeDLevel(Fiber fiber)
    {
        NativeSeeDState next = services.SeeD!.AddRankPoints(fiber.Stack.Peek());
        PublishNativeSeeD(fiber, next);
        return Pop(fiber, 1);
    }

    private void ExecuteNativeSalary(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeWorldControlState controls = services.WorldControls!;
        if (opcode is Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF)
        {
            bool enabled = opcode == Jsm.Opcode.SARALYON;
            PublishNativeEventOwners(fiber, controls.WithSalaryEnabled(enabled), seeD: services.SeeD!.WithSalaryUiEnabled(enabled));
        }
        else
        {
            SetNativeWorldControls(fiber, controls.WithSalaryDisplayEnabled(opcode == Jsm.Opcode.SARALYDISPON));
        }
    }

    private void PublishNativeSeeD(Fiber fiber, NativeSeeDState state)
    {
        try
        {
            services.SetSeeD(state);
        }
        catch (NotSupportedException exception)
        {
            throw NativeOwnerPublicationFault(fiber, exception);
        }
    }

    private void PublishNativeEventOwners(Fiber fiber, NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null)
    {
        try
        {
            services.SetNativeEventOwners(controls, seeD, timer, characterVitals, economy, globalSoundProcess, salaryPresentation);
        }
        catch (NotSupportedException exception)
        {
            throw NativeOwnerPublicationFault(fiber, exception);
        }
    }

    private EventScriptException NativeOwnerPublicationFault(Fiber fiber, NotSupportedException exception)
    {
        fiber.Position--;
        string opcode = program!.Operations[fiber.Position].Opcode.ToString();
        string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native owner service rejected publication; PC and operands preserved.";
        LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode, message);
        return new EventScriptException(message, exception);
    }
}
