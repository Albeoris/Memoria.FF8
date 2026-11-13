namespace Memoria.FF8.GameUi;

internal sealed class BattleCursorMemory<TActor, TCommand, TSelection> where TActor : notnull where TCommand : notnull
{
    private readonly Dictionary<TActor, TCommand> commands = [];
    private readonly Dictionary<(TActor Actor, TCommand Command), TSelection> selections = [];

    public bool TryGetCommand(TActor actor, out TCommand command) => commands.TryGetValue(actor, out command!);
    public TSelection? Selection(TActor actor, TCommand command) => selections.GetValueOrDefault((actor, command));

    public void Remember(TActor actor, TCommand command, TSelection selection)
    {
        commands[actor] = command;
        selections[(actor, command)] = selection;
    }

    public void Clear()
    {
        commands.Clear();
        selections.Clear();
    }
}
