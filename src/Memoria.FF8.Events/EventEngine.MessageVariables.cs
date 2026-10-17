using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeMessageVariables? MessageVariables => services.MessageVariables;

    // Presentation caches need the canonical opening identity and its live variable template.
    public IReadOnlyCollection<DialogueState> DialogueTemplates => dialogues.Values.Where(dialogue => dialogue.NativeWindow is not { Progress: 0, Delta: < 0 }).ToArray();

    private DialogueState ExpandMessageVariables(DialogueState dialogue)
    {
        return dialogue with { Text = NativeMessageVariables.Expand(dialogue.Text, services.MessageVariables) };
    }

    private string? UnsupportedMessageVariable(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.MESVAR)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || !services.CanStoreMessageVariables)
        {
            return "MESVAR requires the verified native handler and canonical process message-variable service";
        }
        if (fiber.Stack.Count < 2 || fiber.Stack.Capture().Take(2).Any(value => !value.IsExact))
        {
            return "MESVAR requires two exact native DWORD operands; typed uncertainty is retained without choosing a value";
        }
        return null;
    }

    private int[] SetMessageVariable(Fiber fiber)
    {
        int[] arguments = Pop(fiber, 2);
        NativeMessageVariables bank = services.MessageVariables ?? NativeMessageVariables.Unknown();
        services.SetMessageVariables(bank.WithWrite(arguments[0] & 7, arguments[1]));
        RefreshCurrentDialogue();
        return arguments;
    }
}
