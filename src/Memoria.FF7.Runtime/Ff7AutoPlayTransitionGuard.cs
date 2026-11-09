using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed class Ff7AutoPlayTransitionGuard
{
    private readonly AutoPlayTransitionCycleGuard<(string Field, short Story, int Battles, int Witness)> guard = new();
    private long generation = -1;

    public void Reset()
    {
        generation = -1;
        guard.Reset();
    }

    public void Observe(Ff7Session session)
    {
        if (generation == session.FieldGeneration)
        {
            return;
        }
        generation = session.FieldGeneration;
        short story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        int witness = session.Field.Name is "elevtr1" or "nmkin_1" ? session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE1) & 1 : 0;
        if (guard.Observe((session.Field.Name, story, session.Progress.CompletedBattles.Length, witness)))
        {
            throw new InvalidOperationException($"FFVII autoplay repeated the same two-field route three times without a native story handoff: {session.Field.Name}, story {story}.");
        }
    }
}
