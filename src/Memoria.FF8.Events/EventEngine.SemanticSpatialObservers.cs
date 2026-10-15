using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>A completed-visit policy boundary, not native completion: unchanged geometry need not satisfy an autonomous spatial observer.</summary>
    private bool IsSemanticSpatialObserver(Fiber fiber, ReplayScriptGoal goal)
    {
        if (!IsSemanticSpatialReplay || program is null || fiber.Initialization)
        {
            return false;
        }
        JsmOperation goalOperation = program.Operations[goal.Operation];
        int goalWidth = goalOperation.Opcode switch
        {
            Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        if (goalWidth == 0 || goalOperation.Parameter < 1024 || goalOperation.Parameter > 1280 - goalWidth)
        {
            return false;
        }
        for (int end = fiber.Script.Start; end < fiber.Script.End; end++)
        {
            JsmOperation back = program.Operations[end];
            if (back.Opcode is not (Jsm.Opcode.JMP or Jsm.Opcode.JPF))
            {
                continue;
            }
            long startValue = (long)end + back.Parameter;
            if (startValue < fiber.Script.Start || startValue >= end || fiber.Position < startValue || fiber.Position > end)
            {
                continue;
            }
            int start = (int)startValue;
            bool query = false;
            bool allowed = true;
            for (int index = start; index <= end; index++)
            {
                JsmOperation operation = program.Operations[index];
                if (operation.Opcode is Jsm.Opcode.GETINFO or Jsm.Opcode.PGETINFO or Jsm.Opcode.ISTOUCH)
                {
                    query = true;
                    continue;
                }
                if (operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
                {
                    long target = (long)index + operation.Parameter;
                    if (target < start || target >= fiber.Script.End)
                    {
                        allowed = false;
                        break;
                    }
                    continue;
                }
                int storeWidth = operation.Opcode switch
                {
                    Jsm.Opcode.POPM_B => 1,
                    Jsm.Opcode.POPM_W => 2,
                    Jsm.Opcode.POPM_L => 4,
                    _ => 0
                };
                if (storeWidth > 0)
                {
                    //Only the actual field scratch bank cleared by each loader is local.
                    if (operation.Parameter < 1024 || operation.Parameter > 1280 - storeWidth)
                    {
                        allowed = false;
                        break;
                    }
                    continue;
                }
                if (operation.Opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.CAL or Jsm.Opcode.RANIMELOOP or Jsm.Opcode.MSPEED or Jsm.Opcode.MLIMIT || IsSemanticSpatialOpcode(operation.Opcode))
                {
                    continue;
                }
                allowed = false;
                break;
            }
            if (allowed && query)
            {
                return true;
            }
        }
        return false;
    }
}
