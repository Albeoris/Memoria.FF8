using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed class Ff7AutoPlayTransitionGuard
{
    private readonly AutoPlayTransitionCycleGuard<(string Field, string Progress)> guard = new();
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
        if (Observe(session.Field.Name, session.FieldGeneration, session.CaptureTransitionProgressWitness()))
        {
            short story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
            throw new InvalidOperationException($"FFVII autoplay repeated the same two-field route three times without a change in recorded native progress: {session.Field.Name}, story {story}.");
        }
    }

    internal bool Observe(string field, long fieldGeneration, string progressWitness)
    {
        if (generation == fieldGeneration)
        {
            return false;
        }
        generation = fieldGeneration;
        return guard.Observe((field, progressWitness));
    }
}
