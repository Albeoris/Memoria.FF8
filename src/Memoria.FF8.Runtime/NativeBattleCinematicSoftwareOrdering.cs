using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The private ordering-table entry executes one software-only pass while retaining the shared batch owners.</summary>
public static class NativeBattleCinematicSoftwareOrdering
{
    public static void Execute45CEE0(int proxyCursor, Func<ImmutableArray<NativeBattleLogicalCommand>> readCommands, INativeBattleLogicalExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(readCommands);
        ExecuteAdmitted45CEE0(proxyCursor, () =>
        {
            NativeBattleCommandPages<NativeBattleLogicalCommandReference>.Builder commands = new();
            foreach (NativeBattleLogicalCommand command in readCommands())
            {
                commands.Add(command);
            }
            return commands.Freeze();
        }, context);
    }

    internal static void ExecuteAdmitted45CEE0(int proxyCursor, Func<NativeBattleCommandPages<NativeBattleLogicalCommandReference>> readCommands, INativeBattleLogicalExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (proxyCursor < 0)
        {
            throw new InvalidDataException("Native private ordering requires its actual shared proxy cursor.");
        }
        if (proxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
        {
            return;
        }
        context.BeginOrdering();
        foreach (ref readonly NativeBattleLogicalCommandReference command in readCommands())
        {
            if (context.SoftwareTable.Handler(command.Opcode) != 0)
            {
                context.ExecuteSoftware(command);
            }
        }
        context.FlushSoftwareImage();
    }
}
