using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveFieldColor([property: JsonRequired] byte Red, [property: JsonRequired] byte Green, [property: JsonRequired] byte Blue);
public sealed record GameplaySaveFieldShade([property: JsonRequired] ushort Duration, [property: JsonRequired] GameplaySaveFieldColor First, [property: JsonRequired] GameplaySaveFieldColor Second, [property: JsonRequired] long StartFrame);
public sealed record GameplaySaveFieldShadeLoop([property: JsonRequired] int ObjectId, [property: JsonRequired] ushort FadeFirst, [property: JsonRequired] ushort FadeSecond, [property: JsonRequired] GameplaySaveFieldColor First, [property: JsonRequired] GameplaySaveFieldColor Second, [property: JsonRequired] byte HoldFirst, [property: JsonRequired] byte HoldSecond, [property: JsonRequired] bool FirstToSecond, [property: JsonRequired] int Elapsed, [property: JsonRequired] int Remaining, [property: JsonRequired] byte HoldRemaining, [property: JsonRequired] GameplaySaveFieldColor Current);
public sealed record GameplaySaveFieldScrollPhase([property: JsonRequired] short? X, [property: JsonRequired] short? Y, [property: JsonRequired] short TargetX, [property: JsonRequired] short TargetY, [property: JsonRequired] short? PreviousX, [property: JsonRequired] short? PreviousY, [property: JsonRequired] short? Counter, [property: JsonRequired] byte Mode, [property: JsonRequired] byte Status, [property: JsonRequired] short? Duration);

/// <summary>Logical module colors and layer interpolation words. No draw samples, native buffers or command history.</summary>
public sealed record GameplaySaveFieldAmbient([property: JsonRequired] Dictionary<int, GameplaySaveFieldShade> Shades, [property: JsonRequired] Dictionary<int, GameplaySaveFieldShadeLoop> ShadeLoops, [property: JsonRequired] Dictionary<int, GameplaySaveFieldScrollPhase> Scrolls)
{
    public void Validate(long frame)
    {
        if (frame < 0 || Shades is null || ShadeLoops is null || Scrolls is null || Shades.Any(pair => pair.Key < 0 || pair.Value is null || pair.Value.First is null || pair.Value.Second is null || pair.Value.StartFrame < 0 || pair.Value.StartFrame > frame && pair.Value.StartFrame - frame > 1 || ShadeLoops.ContainsKey(pair.Key)))
        {
            throw new InvalidDataException("Gameplay background shade has an invalid module, endpoint or logical clock.");
        }
        foreach ((int module, GameplaySaveFieldShadeLoop loop) in ShadeLoops)
        {
            if (loop is null)
            {
                throw new InvalidDataException("Gameplay background shade loop is missing.");
            }
            int duration = loop.FirstToSecond ? loop.FadeFirst : loop.FadeSecond;
            if (module < 0 || loop.ObjectId < 0 || loop.First is null || loop.Second is null || loop.Current is null || loop.Remaining < 0 || (loop.Remaining == 0 ? loop.Elapsed != 0 : duration == 0 || loop.Elapsed < 0 || loop.Elapsed > duration || loop.Remaining != duration + 1 - loop.Elapsed))
            {
                throw new InvalidDataException("Gameplay background shade loop has an invalid logical phase.");
            }
        }
        if (Scrolls.Any(pair => pair.Key is < 0 or >= 8 || pair.Value is null || pair.Value.X.HasValue != pair.Value.Y.HasValue || pair.Value.PreviousX.HasValue != pair.Value.PreviousY.HasValue || pair.Value.Mode is not (3 or 4 or 5) || pair.Value.Status > 2))
        {
            throw new InvalidDataException("Gameplay layer scroll has an invalid logical phase.");
        }
    }
}
