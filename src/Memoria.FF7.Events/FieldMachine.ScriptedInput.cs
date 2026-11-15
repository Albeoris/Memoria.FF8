namespace Memoria.FF7.Events;

public enum FieldScriptInputKind
{
    Held,
    Pressed,
    Released
}

public sealed record FieldScriptInputDemand(string Identity, string ProgramIdentity, int Entity, int Priority, int ProgramCounter, FieldScriptInputKind Kind, ushort Mask, bool Mapped, bool AwaitingInput);

public sealed partial class FieldMachine
{
    private sealed class InputInspectionBoundaryException : Exception
    {
    }

    /// <summary>Finds current native button waits without dispatching hosts, requesting scripts or advancing the live VM.</summary>
    public IReadOnlyList<FieldScriptInputDemand> ReadCurrentInputDemands(int instructionBudget = 64)
    {
        if (instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        }
        if (!Initialized)
        {
            return [];
        }
        FieldVmSnapshot source = Capture() with { CycleCursor = null };
        List<FieldScriptInputDemand> demands = [];
        for (int entity = 0; entity < entities.Length; entity++)
        {
            FieldMachine detached = Restore(program, source, new InspectionHost());
            detached.inspection = new() { StopAtHost = true };
            HashSet<(int Pc, int Priority)> visited = [];
            for (int step = 0; step < instructionBudget; step++)
            {
                Entity owner = detached.entities[entity];
                if (!visited.Add((owner.Pc, owner.Priority)))
                {
                    break;
                }
                try
                {
                    byte opcode = program.Read(owner.Pc);
                    if (opcode is >= 0x30 and <= 0x32)
                    {
                        int pc = owner.Pc;
                        ushort mask = (ushort)(program.Read(pc + 1) | program.Read(pc + 2) << 8);
                        bool mapped = (program.Read(pc + 2) & 2) != 0;
                        int unmatched = unchecked((ushort)(pc + 3 + program.Read(pc + 3)));
                        bool awaiting = mask != 0 && unmatched != unchecked((ushort)(pc + 4)) && LoopsBackToInput(detached, entity, unmatched, pc, instructionBudget);
                        string identity = $"{program.Identity}:{entity}:{owner.Priority}:{pc:X4}";
                        demands.Add(new(identity, program.Identity, entity, owner.Priority, pc, (FieldScriptInputKind)(opcode - 0x30), mask, mapped, awaiting));
                        break;
                    }
                    if (IsInputInspectionBoundary(opcode))
                    {
                        break;
                    }
                    detached.Execute(entity);
                }
                catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
                {
                    break;
                }
            }
        }
        return demands;
    }

    private bool LoopsBackToInput(FieldMachine source, int entity, int unmatched, int target, int instructionBudget)
    {
        FieldVmSnapshot original = source.Capture() with { CycleCursor = null };
        FieldMachine detached = Restore(program, original, new InspectionHost());
        detached.inspection = new() { StopAtHost = true };
        detached.entities[entity].Pc = unmatched;
        HashSet<int> visited = [];
        for (int step = 0; step < instructionBudget; step++)
        {
            int pc = detached.entities[entity].Pc;
            if (pc == target)
            {
                return original.PersistentVariables.AsSpan().SequenceEqual(detached.Variables.CopyPersistent()) && original.MapVariables.AsSpan().SequenceEqual(detached.Variables.CopyMap());
            }
            if (!visited.Add(pc))
            {
                return false;
            }
            try
            {
                byte opcode = program.Read(pc);
                if (opcode is >= 0x30 and <= 0x32 || IsInputInspectionBoundary(opcode))
                {
                    return false;
                }
                detached.Execute(entity);
            }
            catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                return false;
            }
        }
        return false;
    }

    private static bool IsInputInspectionBoundary(byte opcode)
    {
        return opcode is <= 0x06 or 0x24 or 0x5F;
    }
}
