using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeLadderAnimationState([property: JsonRequired] byte Start594, [property: JsonRequired] byte Loop595, [property: JsonRequired] byte End596);

public sealed partial class EventEngine
{
    private int[] ExecuteLadderAnimation(Fiber fiber, ActorState actor, int parameter)
    {
        // Native51DA00 stores BYTE594 from the inline operand, then pops low
        // BYTEs of the physical DWORD stack into595 and596. Return2 completes
        // the opcode immediately and permits the next instruction this frame.
        int[] args = Pop(fiber, 2);
        actor.NativeLadderAnimation = new NativeLadderAnimationState(unchecked((byte)parameter), unchecked((byte)args[1]), unchecked((byte)args[0]));
        return args;
    }
}
