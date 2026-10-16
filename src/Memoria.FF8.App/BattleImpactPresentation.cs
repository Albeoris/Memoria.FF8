using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal readonly record struct BattleImpactCue(BattleEvent Event, int Age)
{
    public bool IsHealing => Event.Kind == "Healing";
    public bool IsPhysical => Event.Kind == "Damage" && Event.ActorId is not null && Event.Ability == BattleAbility.Attack;
}

internal static class BattleImpactPresentation
{
    // TODO(TD-009): Hit sparks, floating HP values and their durations provide presentation feedback until native effect scripts/atlases are decoded.
    public const int Duration = 30;

    public static IReadOnlyList<BattleImpactCue> Collect(BattleSession session)
    {
        List<BattleImpactCue> result = [];
        IReadOnlyList<BattleEvent> events = session.Events;
        for (int index = events.Count - 1; index >= 0; index--)
        {
            BattleEvent item = events[index];
            long age = session.Frame - item.Frame;
            if (age >= Duration)
            {
                break;
            }
            if (age >= 0 && item.Amount > 0 && item.TargetId is not null && item.Kind is "Damage" or "Healing")
            {
                result.Add(new BattleImpactCue(item, (int)age));
            }
        }
        result.Reverse();
        return result;
    }
}

/// <summary>Device presentation time lets feedback finish while TurnBased or a result pauses dispatch.</summary>
internal sealed class BattleImpactTimeline
{
    private readonly Dictionary<BattleEvent, double> firstFrames = [];
    private double frames;

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        frames += elapsed.TotalSeconds * BattleSession.FramesPerSecond;
    }

    public IReadOnlyList<BattleImpactCue> Collect(BattleSession session)
    {
        List<BattleImpactCue> cues = [];
        foreach (BattleImpactCue cue in BattleImpactPresentation.Collect(session))
        {
            if (!firstFrames.TryGetValue(cue.Event, out double first))
            {
                first = frames - cue.Age;
                firstFrames.Add(cue.Event, first);
            }
            int age = (int)Math.Min(BattleImpactPresentation.Duration, Math.Max(cue.Age, frames - first));
            if (age < BattleImpactPresentation.Duration)
            {
                cues.Add(cue with { Age = age });
            }
        }
        return cues;
    }
}
