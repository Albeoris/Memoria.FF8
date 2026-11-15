using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayHostConditions(bool Complete, string Fingerprint, string? Reason = null);

public sealed partial class Ff7AutoPlayDiscovery
{
    private static string CombineConditionFingerprints(string variables, string external)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{variables}|{external}")));
    }

    internal static Ff7AutoPlayHostConditions ReadHostConditions(IReadOnlyList<FieldHostRead> reads, Ff7Progress progress, Ff7FieldProcess process, Func<bool>? readMusicPlaying = null)
    {
        StringBuilder signature = new();
        try
        {
            foreach (FieldHostRead read in reads)
            {
                string value = read.Kind switch
                {
                    FieldHostReadKind.Gil => progress.Gil.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FieldHostReadKind.ItemQuantity => Ff7NativeInventory.Quantity(progress.Inventory, read.Parameter).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FieldHostReadKind.PartyAvailable => ((progress.AvailableCharacters & (1 << read.Parameter)) != 0).ToString(),
                    FieldHostReadKind.Actor => ReadActorCondition(process, read),
                    FieldHostReadKind.PreviousMap => (process.PreviousMap ?? throw new NotSupportedException("The native previous-map source fact is absent.")).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FieldHostReadKind.BattleResult => process.BattleResult.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FieldHostReadKind.MateriaCapacity => (progress.Materia.Length == 200).ToString(),
                    FieldHostReadKind.MovieFrame => process.Movie?.Frame.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unprepared",
                    FieldHostReadKind.MusicPlaying => (readMusicPlaying ?? throw new NotSupportedException("A native music-playing dependency requires its native timeline."))().ToString(),
                    _ => throw new NotSupportedException($"Native host dependency {read.Kind} has no read-only condition projection.")
                };
                signature.Append($"{read.Kind}:{read.Parameter}:{read.Selector}:{value}|");
            }
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            return new(false, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString()))), error.Message);
        }
        return new(true, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString()))));
    }

    private static string ReadActorCondition(Ff7FieldProcess process, FieldHostRead read)
    {
        Ff7Actor actor = process.Actors.SingleOrDefault(actor => actor.Entity == read.Parameter && actor.EntityBound) ?? throw new InvalidDataException("A native actor-query dependency has no bound actor.");
        return (FieldActorQueryKind)read.Selector switch
        {
            FieldActorQueryKind.Triangle => actor.Triangle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FieldActorQueryKind.Direction => actor.Direction.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FieldActorQueryKind.Coordinates => $"{((actor.X4096 >> 12) & 65535) | (actor.Y4096 >> 12) << 16}",
            FieldActorQueryKind.Position => $"{unchecked((short)(actor.X4096 >> 12))},{unchecked((short)(actor.Y4096 >> 12))},{unchecked((short)(actor.Z4096 >> 12))},{unchecked((short)actor.Triangle)}",
            _ => throw new NotSupportedException("The native actor query selector has no condition projection.")
        };
    }
}
