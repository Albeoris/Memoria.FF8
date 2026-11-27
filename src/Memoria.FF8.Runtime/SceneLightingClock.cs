namespace Memoria.FF8.Runtime;

public sealed class SceneLightingCycle
{
    public SceneLightingCycle(double day = 480, double night = 240, double fade = 40, double battleSlower = 3, double evening = 0.9)
    {
        Day = double.IsFinite(day) && day > 0 ? day : 480;
        Night = double.IsFinite(night) && night > 0 ? night : 240;
        Fade = Math.Min(double.IsFinite(fade) && fade >= 0 ? fade : 40, Math.Min(Day, Night));
        BattleSlower = double.IsFinite(battleSlower) && battleSlower > 0 ? battleSlower : 3;
        Evening = double.IsFinite(evening) ? Math.Clamp(evening, 0.5, 1) : 0.9;
    }

    public double Day { get; }
    public double Night { get; }
    public double Fade { get; }
    public double BattleSlower { get; }
    public double Evening { get; }
    public double Length => Day + Night;

    public double Wrap(double seconds)
    {
        if (!double.IsFinite(seconds))
        {
            return 0;
        }
        double wrapped = seconds % Length;
        return wrapped < 0 ? wrapped + Length : wrapped;
    }

    public double NightAmount(double seconds)
    {
        double time = Wrap(seconds);
        double half = Fade / 2;
        if (Fade == 0)
        {
            return time < Day ? 0 : 1;
        }
        if (time < half)
        {
            return 1 - Smooth((time + half) / Fade);
        }
        if (time < Day - half)
        {
            return 0;
        }
        if (time < Day + half)
        {
            return Smooth((time - (Day - half)) / Fade);
        }
        if (time < Length - half)
        {
            return 1;
        }
        return 1 - Smooth((time - (Length - half)) / Fade);
    }

    public double EveningAmount(double seconds)
    {
        double time = Wrap(seconds);
        double half = Fade / 2;
        double from = Math.Min(Day * Evening, Day - half);
        double to = Day + half;
        return time >= from && time < to && to > from ? Smooth((time - from) / (to - from)) : NightAmount(time);
    }

    private static double Smooth(double fraction)
    {
        double t = Math.Clamp(fraction, 0, 1);
        return t * t * (3 - 2 * t);
    }
}

public sealed class SceneLightingClock
{
    public SceneLightingClock(SceneLightingCycle cycle, SceneClockSnapshot? saved, uint playSeconds)
    {
        Seconds = cycle.Wrap(saved?.Seconds ?? cycle.Fade / 2 + playSeconds);
        Paused = saved?.Paused ?? false;
    }

    public double Seconds { get; private set; }
    public bool Paused { get; set; }

    public void Advance(SceneLightingCycle cycle, TimeSpan elapsed, bool battle, bool held)
    {
        if (Paused || held)
        {
            return;
        }
        double step = Math.Clamp(elapsed.TotalSeconds, 0, 0.25);
        Seconds = cycle.Wrap(Seconds + (battle ? step / cycle.BattleSlower : step));
    }
}
