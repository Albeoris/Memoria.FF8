using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed class Ff7AutoPlayTransitionGuard
{
    private readonly AutoPlayTransitionCycleGuard<(string Field, string Progress)> guard = new();
    private long generation = -1;
    private readonly HashSet<string> completedSources = new(StringComparer.Ordinal);

    public void Reset()
    {
        generation = -1;
        guard.Reset();
        completedSources.Clear();
    }

    public void Observe(Ff7Session session)
    {
        if (generation == session.FieldGeneration)
        {
            return;
        }
        if (Observe(session.Field.Name, session.FieldGeneration, session.CaptureTransitionProgressWitness(), session.AutoPlayState))
        {
            short story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
            throw new InvalidOperationException($"FFVII autoplay repeated the same two-field route three times without a change in recorded native progress: {session.Field.Name}, story {story}.");
        }
    }

    internal bool Observe(string field, long fieldGeneration, string materialProgress, Ff7AutoPlayState? state)
    {
        if (generation == fieldGeneration)
        {
            return false;
        }
        if (state is not null)
        {
            foreach (var visit in state.Coverage.Visits)
            {
                foreach (var item in visit.Events.Where(item => item.Completed && item.Conditions is not null))
                {
                    foreach (var route in item.Routes)
                    {
                        if (route.TargetId is not string target || route.ConditionSignature is not string condition || !item.Conditions!.ConsumedEntries.Contains(condition) || target != item.Event.Id && item.Event.Variants?.Contains(target, StringComparer.Ordinal) != true)
                        {
                            continue;
                        }
                        if (ReadStaticCompletedSource(visit.Visit.Key, target) is string source)
                        {
                            completedSources.Add(source);
                        }
                    }
                }
            }
        }
        string progress = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Material = materialProgress, Sources = completedSources.Order(StringComparer.Ordinal).ToArray() })));
        return Observe(field, fieldGeneration, progress);
    }

    private static string? ReadStaticCompletedSource(string visit, string target)
    {
        string[] source = visit.Split(':');
        if (source.Length != 3 || source[0] != "ff7" || !ushort.TryParse(source[1], NumberStyles.None, CultureInfo.InvariantCulture, out ushort field) || source[2].Length != 64 || !source[2].All(Uri.IsHexDigit))
        {
            return null;
        }
        string[] parts = target.Split(':');
        string? identity = null;
        if (parts.Length == 2 && parts[0] == "gateway" && byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte gateway))
        {
            identity = $"gateway:{gateway}";
        }
        else if (parts.Length == 4 && parts[0] == "actor" && parts[2] == "script" && byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte actor) && byte.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out byte actorScript) && actorScript < 32)
        {
            identity = $"actor:{actor}:script:{actorScript}";
        }
        else if (parts.Length == 6 && parts[0] == "line" && parts[2] == "entity" && parts[4] == "script" && byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte line) && byte.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out byte entity) && byte.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out byte lineScript) && lineScript < 32)
        {
            identity = $"line:{line}:entity:{entity}:script:{lineScript}";
        }
        return identity is null ? null : $"ff7:{field}:{source[2].ToUpperInvariant()}|{identity}";
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
