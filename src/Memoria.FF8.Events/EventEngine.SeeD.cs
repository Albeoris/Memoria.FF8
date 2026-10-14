using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSeeD(Fiber fiber, JsmOperation operation)
    {
        bool command = operation.Opcode is Jsm.Opcode.ADDSEEDLEVEL or Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF or Jsm.Opcode.SARALYDISPON or Jsm.Opcode.SARALYDISPOFF;
        int width = NativeMemoryWidth(operation.Opcode);
        bool rankMemory = width != 0 && operation.Parameter < 18 && (long)operation.Parameter + width > 16;
        if (!command && !rankMemory)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || services.SeeD is not NativeSeeDState state || !services.CanStoreSeeD)
        {
            return "Native SeeD rank and salary require the executed New Game52CA90 state and a persistent service; no rank is inferred";
        }
        state.Validate();
        if (variables.Keys.Any(address => address is >= 13 and <= 17) || services.NativeVariableValues?.Any(pair => pair.Key < 18 && (long)pair.Key + pair.Value.Width > 16) == true)
        {
            return "Native SeeD WORD16 overlaps an unproved VM memory alias; recreate the authentic journal";
        }
        bool operand = operation.Opcode == Jsm.Opcode.ADDSEEDLEVEL || operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        if (operand && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact))
        {
            return "Native SeeD update requires its exact original DWORD operand before mutation";
        }
        if (rankMemory && !(width == 1 && operation.Parameter is 16 or 17 || width == 2 && operation.Parameter == 16))
        {
            return "Native SeeD memory projection supports its owned BYTE16/17 or WORD16; surrounding memory is unproved";
        }
        return null;
    }

    private bool TryReadNativeSeeDRank(JsmOperation operation, out int value)
    {
        value = 0;
        if (services.SeeD is not NativeSeeDState state)
        {
            return false;
        }
        if (operation.Opcode is Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W && operation.Parameter == 16)
        {
            value = state.RankPoints;
            return true;
        }
        if (operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B && operation.Parameter is 16 or 17)
        {
            value = operation.Parameter == 16 ? state.RankPoints & 255 : state.RankPoints >> 8;
            return true;
        }
        return false;
    }

    private bool TryWriteNativeSeeDRank(Fiber fiber, JsmOperation operation, out int[] operands)
    {
        operands = [];
        if (services.SeeD is not NativeSeeDState state || operation.Opcode == Jsm.Opcode.POPM_W && operation.Parameter != 16 || operation.Opcode == Jsm.Opcode.POPM_B && operation.Parameter is not (16 or 17) || operation.Opcode is not (Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W))
        {
            return false;
        }
        int raw = fiber.Stack.Peek();
        ushort word = operation.Opcode == Jsm.Opcode.POPM_W ? unchecked((ushort)raw) : operation.Parameter == 16 ? (ushort)((state.RankPoints & 0xFF00) | (byte)raw) : (ushort)((state.RankPoints & 0xFF) | (byte)raw << 8);
        services.SetSeeD(state with { RankPoints = word });
        operands = Pop(fiber, 1);
        return true;
    }

    private int[] ExecuteAddSeeDLevel(Fiber fiber)
    {
        NativeSeeDState next = services.SeeD!.AddRankPoints(fiber.Stack.Peek());
        services.SetSeeD(next);
        return Pop(fiber, 1);
    }

    private void ExecuteNativeSalary(Jsm.Opcode opcode)
    {
        NativeSeeDState state = services.SeeD!;
        NativeSeeDState next = opcode is Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF ? state.WithSalaryEnabled(opcode == Jsm.Opcode.SARALYON) : state.WithSalaryDisplayEnabled(opcode == Jsm.Opcode.SARALYDISPON);
        services.SetSeeD(next);
    }
}
