using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    internal FieldHostRequest? PendingMinigame
    {
        get
        {
            if (Process.GlobalCommand != Ff7GlobalCommand.Minigame)
            {
                return null;
            }
            if (Process.GlobalRequestId is not long id || !receipts.TryGetValue(id, out Ff7HostReceipt? receipt) || receipt.Request.Command is not FieldMinigameCommand || receipt.Reply.Completed)
            {
                throw new InvalidOperationException("The FFVII minigame departure lost its unfinished field request.");
            }
            return receipt.Request;
        }
    }

    private FieldHostReply RequestMinigame(FieldHostRequest request)
    {
        if (Process.GlobalCommand == Ff7GlobalCommand.None)
        {
            Process = Process with { GlobalCommand = Ff7GlobalCommand.Minigame, GlobalRequestId = request.Id };
        }
        return new(false);
    }
}
