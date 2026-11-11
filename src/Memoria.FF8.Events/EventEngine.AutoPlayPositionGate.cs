using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public enum NativePositionComparison
{
    LessThan,
    GreaterThan,
    Equal
}

public readonly record struct NativePositionConstraint(int Register, NativePositionComparison Comparison, int Value)
{
    public bool Admits(float x, float y, float z, float margin = 0)
    {
        int value = (int)(Register switch { 0 => x, 1 => y, 2 => z, _ => throw new InvalidOperationException("A native position constraint requires an XYZ register.") });
        return Comparison switch
        {
            NativePositionComparison.LessThan => value < Value - margin,
            NativePositionComparison.GreaterThan => value > Value + margin,
            NativePositionComparison.Equal => value == Value,
            _ => false
        };
    }
}

public readonly record struct NativePlayerPositionGate(int OwnerObjectId, IReadOnlyList<NativePositionConstraint> Constraints)
{
    public NativePlayerPositionGate(int register, int exclusiveUpperBound) : this(-1, new[] { new NativePositionConstraint(register, NativePositionComparison.LessThan, exclusiveUpperBound) })
    {
    }

    public int Register => Constraints[0].Register;
    public int ExclusiveUpperBound => Constraints[0].Value;

    public bool Admits(float x, float y)
    {
        return Admits(x, y, 0);
    }

    public bool Admits(float x, float y, float z, float margin = 0)
    {
        return Constraints.Count > 0 && Constraints.All(constraint => constraint.Admits(x, y, z, margin));
    }
}

public sealed partial class EventEngine
{
    public NativePlayerPositionGate? DiscoverAutoPlayPositionGate()
    {
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || !CanAcceptMovementInput || PlayerActor is not { } player || player.NativeScriptMovement is { Active: true } || player.NativeJump is { Active: true } || player.NativeLadder is { Active: true } || player.NativeLadderMode3 is { Active: true })
        {
            return null;
        }
        IEnumerable<ActorState> owners = new[] { player }.Concat(Enumerable.Range(0, 3).Select(slot => NativeFieldPartyBinding.ResolveActor(Scene, actors, slot)).OfType<ActorState>().Where(actor => actor != player && actor.Active && actor.NativePartyFlags is { DetachedFollow64: false }));
        foreach (ActorState actor in owners.Distinct())
        {
            if (SelectScheduledFiber(actor.ObjectId) is not { Initialization: false, Started: true } owner || owner.Script != program.Objects[actor.ObjectId].Scripts.ElementAtOrDefault(1) || ReadNativeScriptPosition(actor) is not { } position)
            {
                continue;
            }
            foreach (int poll in owner.Script.ReachableOperations.Where(index => index >= owner.Position - 24 && index <= owner.Position + 4 && program.Operations[index].Opcode == Jsm.Opcode.GETINFO).Order())
            {
                if (TryReadAutoPlayPositionGate(program, owner.Script, owner.Position, poll, actor.ObjectId, out NativePlayerPositionGate gate) && !gate.Admits(position.X / 4096, position.Y / 4096, position.Z / 4096))
                {
                    return gate;
                }
            }
        }
        return null;
    }

    internal static bool TryReadAutoPlayPositionGate(JsmProgram program, EventScript script, int currentPosition, int poll, int ownerObjectId, out NativePlayerPositionGate gate)
    {
        gate = default;
        IReadOnlyList<JsmOperation> operations = program.Operations;
        bool Has(int index) => (uint)index < operations.Count && script.ContainsOperation(index);
        if (!Has(poll) || operations[poll].Opcode != Jsm.Opcode.GETINFO)
        {
            return false;
        }
        List<NativePositionConstraint> constraints = [];
        int cursor = poll + 1;
        int retry = -1;
        while (constraints.Count < 3 && Has(cursor) && operations[cursor] is { Opcode: Jsm.Opcode.PSHI_L, Parameter: >= 0 and <= 2 } register)
        {
            if (!Has(cursor + 1) || operations[cursor + 1] is not { Opcode: Jsm.Opcode.PSHN_L } literal || constraints.Any(constraint => constraint.Register == register.Parameter))
            {
                return false;
            }
            int value = literal.Parameter;
            cursor += 2;
            if (Has(cursor) && operations[cursor] is { Opcode: Jsm.Opcode.CAL, Parameter: 5 })
            {
                value = unchecked(-value);
                cursor++;
            }
            if (!Has(cursor + 1) || operations[cursor] is not { Opcode: Jsm.Opcode.CAL, Parameter: 6 or 7 or 9 } comparison || operations[cursor + 1] is not { Opcode: Jsm.Opcode.JPF } branch)
            {
                return false;
            }
            int failed = JsmProgram.JumpTarget(cursor + 1, branch.Parameter);
            if (retry >= 0 && retry != failed)
            {
                return false;
            }
            retry = failed;
            constraints.Add(new(register.Parameter, comparison.Parameter switch { 9 => NativePositionComparison.LessThan, 7 => NativePositionComparison.GreaterThan, _ => NativePositionComparison.Equal }, value));
            cursor += 2;
        }
        if (constraints.Count == 0 || !Has(retry) || operations[retry] is not { Opcode: Jsm.Opcode.JMP } back)
        {
            return false;
        }
        int loop = JsmProgram.JumpTarget(retry, back.Parameter);
        if (loop > poll || poll - loop > 4 || !Enumerable.Range(loop, poll - loop).All(index => Has(index) && operations[index].Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHN_L or Jsm.Opcode.CAL or Jsm.Opcode.JPF) || currentPosition < loop || currentPosition > Math.Max(cursor, retry))
        {
            return false;
        }
        bool immediateControl = Has(cursor) && operations[cursor].Opcode == Jsm.Opcode.UCOFF;
        bool latchedRetry = constraints.Count > 1 && retry == cursor + 2 && Has(cursor + 1) && operations[cursor] is { Opcode: Jsm.Opcode.PSHN_L, Parameter: 1 } && operations[cursor + 1] is { Opcode: Jsm.Opcode.POPM_B };
        if (!immediateControl && !latchedRetry)
        {
            return false;
        }
        gate = new(ownerObjectId, constraints.AsReadOnly());
        return true;
    }
}
