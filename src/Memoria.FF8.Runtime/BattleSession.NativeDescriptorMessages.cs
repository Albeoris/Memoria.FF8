using static Memoria.FF8.Runtime.BattlePresentationProcess;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeDescriptorMessage(BattleEvent message, byte tag = 86, byte priority = 128)
    {
        if (preparingExpandedHostedAction)
        {
            StageExpandedHostedControl(new HostedDispatchControl.Message(message, tag, priority));
        }
        else if (Presentation is { } process)
        {
            process.AppendScriptMessage(message, tag, priority);
            StopTurnAdvance();
        }
        else
        {
            events.Add(message with { Frame = Frame });
        }
    }
}
