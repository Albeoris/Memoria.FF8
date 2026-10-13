using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedWorldControlAliases()
    {
        if (services.WorldControls is null)
        {
            return null;
        }
        return variables.Keys.Any(address => address is >= 102 and <= 105 or >= 206 and <= 209) ? "Native world controls overlap a legacy variable with unknown byte width; original aliases require byte-addressed memory evidence" : null;
    }

    private string? UnsupportedWorldControlAccess(JsmOperation operation)
    {
        if (services.WorldControls is null)
        {
            return null;
        }
        if (operation.Opcode is Jsm.Opcode.PHSPOWER or Jsm.Opcode.MAPJUMPO or Jsm.Opcode.MAPJUMP or Jsm.Opcode.MAPJUMP3)
        {
            return UnsupportedWorldControlAliases();
        }
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        long last = (long)operation.Parameter + width - 1;
        if (width == 0 || !(operation.Parameter <= 105 && last >= 104 || operation.Parameter <= 209 && last >= 209))
        {
            return null;
        }
        if (operation.Parameter == 209 && width == 1)
        {
            return UnsupportedWorldControlAliases();
        }
        return "Native control access needs unknown byte104/105 bits or an unproved overlapping word/DWORD; original PC and operands retained";
    }

    private int ExecutePartyPower(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            throw Fault(fiber, "PHSPOWER requires its original operand.");
        }
        if (services.WorldControls is NativeWorldControlState controls)
        {
            SetNativeWorldControls(fiber, controls.WithPartyPower(fiber.Stack.Peek()));
        }
        Scene.PartyPower = Pop(fiber, 1)[0];
        return Scene.PartyPower;
    }

    private void SetNativeWorldControls(Fiber fiber, NativeWorldControlState controls)
    {
        try
        {
            services.SetWorldControls(controls);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string opcode = program!.Operations[fiber.Position].Opcode.ToString();
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native world-control service rejected the update; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode, message);
            throw new EventScriptException(message, exception);
        }
    }
}
