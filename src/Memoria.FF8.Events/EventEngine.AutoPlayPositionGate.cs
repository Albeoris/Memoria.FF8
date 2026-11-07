using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public readonly record struct NativePlayerPositionGate(int Register, int ExclusiveUpperBound)
{
    public bool Admits(float x, float y)
    {
        return (int)(Register == 0 ? x : y) < ExclusiveUpperBound;
    }
}

public sealed partial class EventEngine
{
    public NativePlayerPositionGate? DiscoverAutoPlayPositionGate()
    {
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || !CanAcceptMovementInput || PlayerActor is not { } player || SelectScheduledFiber(player.ObjectId) is not { Initialization: false, Started: true } owner || owner.Script != program.Objects[player.ObjectId].Scripts.ElementAtOrDefault(1))
        {
            return null;
        }
        FieldFixedPosition? position = ReadNativeScriptPosition(player);
        if (position is null)
        {
            return null;
        }

        // A native default can poll its own position without admitting an interaction.
        // Recognize the strict comparison only when its failed branch returns to the poll.
        for (int offset = 0; offset <= 4; offset++)
        {
            int poll = owner.Position - offset;
            if (poll < 0 || !Enumerable.Range(poll, 6).All(owner.Script.ContainsOperation))
            {
                continue;
            }
            IReadOnlyList<JsmOperation> operations = program.Operations;
            if (operations[poll].Opcode != Jsm.Opcode.GETINFO
                || operations[poll + 1] is not { Opcode: Jsm.Opcode.PSHI_L, Parameter: 0 or 1 } register
                || operations[poll + 2] is not { Opcode: Jsm.Opcode.PSHN_L } limit
                || operations[poll + 3] is not { Opcode: Jsm.Opcode.CAL, Parameter: 9 }
                || operations[poll + 4] is not { Opcode: Jsm.Opcode.JPF } branch
                || operations[poll + 5].Opcode != Jsm.Opcode.UCOFF)
            {
                continue;
            }
            int retry = JsmProgram.JumpTarget(poll + 4, branch.Parameter);
            if (!owner.Script.ContainsOperation(retry) || operations[retry] is not { Opcode: Jsm.Opcode.JMP } back)
            {
                continue;
            }
            int loop = JsmProgram.JumpTarget(retry, back.Parameter);
            if (loop > poll || poll - loop > 4 || !Enumerable.Range(loop, poll - loop).All(i => owner.Script.ContainsOperation(i) && operations[i].Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHN_L or Jsm.Opcode.CAL or Jsm.Opcode.JPF))
            {
                continue;
            }
            NativePlayerPositionGate gate = new(register.Parameter, limit.Parameter);
            if (!gate.Admits(position.X / 4096, position.Y / 4096))
            {
                return gate;
            }
        }
        return null;
    }
}
