using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed class AchievementProfile
{
    public int SchemaVersion { get; set; } = 1;
    public long Revision { get; set; }
    public Dictionary<string, long> Counters { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> Unlocks { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, HashSet<string>> Receipts { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, AchievementDestinationState> Destinations { get; set; } = new(StringComparer.Ordinal);
    internal AchievementProfile Copy() => new()
    {
        SchemaVersion = SchemaVersion,
        Revision = Revision,
        Counters = new(Counters, StringComparer.Ordinal),
        Unlocks = new(Unlocks, StringComparer.Ordinal),
        // Receipt sets are immutable once committed; Record clones only the touched encounter.
        Receipts = new(Receipts, StringComparer.Ordinal),
        Destinations = Destinations.ToDictionary(pair => pair.Key, pair => new AchievementDestinationState
        {
            Contributions = new(pair.Value.Contributions, StringComparer.Ordinal),
            DeliveredContributions = new(pair.Value.DeliveredContributions, StringComparer.Ordinal),
            ConfirmedRemote = new(pair.Value.ConfirmedRemote, StringComparer.Ordinal),
            ConfirmedUnlocks = new(pair.Value.ConfirmedUnlocks, StringComparer.Ordinal),
            Pending = pair.Value.Pending
        }, StringComparer.Ordinal)
    };
    internal void Validate()
    {
        if (SchemaVersion != 1 || Revision < 0 || Counters is null || Unlocks is null || Receipts is null || Destinations is null || Counters.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0) || Receipts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Any(string.IsNullOrWhiteSpace)))
        {
            throw new InvalidDataException("Unsupported or invalid achievement profile.");
        }
        foreach (var pair in Destinations)
        {
            AchievementDestinationState state = pair.Value;
            if (state is null || state.Contributions is null || state.DeliveredContributions is null || state.ConfirmedRemote is null || state.ConfirmedUnlocks is null || state.ConfirmedUnlocks.Any(string.IsNullOrWhiteSpace) || state.Contributions.Values.Any(value => value < 0) || state.DeliveredContributions.Values.Any(value => value < 0) || state.ConfirmedRemote.Values.Any(value => value < 0) || state.Pending is { } batch && (batch.Revision <= 0 || batch.Targets is null || batch.CoveredContributions is null || batch.Unlocks is null || batch.Targets.Values.Any(value => value < 0)))
            {
                throw new InvalidDataException("Invalid achievement delivery state.");
            }
        }
    }
}

public sealed class AchievementDestinationState
{
    public Dictionary<string, long> Contributions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> DeliveredContributions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> ConfirmedRemote { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> ConfirmedUnlocks { get; set; } = new(StringComparer.Ordinal);
    public AchievementDeliveryBatch? Pending { get; set; }
}

public interface IAchievementRepository : IDisposable
{
    AchievementProfile Load();
    void Save(AchievementProfile profile);
}

/// <summary>One profile writer; a flushed replacement commits receipts, progress and pending delivery together.</summary>
public sealed class FileAchievementRepository : IAchievementRepository
{
    private readonly string path;
    private readonly FileStream writerLock;

    public FileAchievementRepository(string path)
    {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        writerLock = new FileStream(this.path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public AchievementProfile Load()
    {
        // An uncommitted temporary file is never promoted: its gameplay outbox remains replayable.
        if (!File.Exists(path))
        {
            return new();
        }
        AchievementProfile profile = JsonSerializer.Deserialize<AchievementProfile>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Empty achievement profile.");
        profile.Validate();
        return profile;
    }

    public void Save(AchievementProfile profile)
    {
        profile.Validate();
        string temporary = path + ".pending";
        using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, profile);
            stream.Flush(true);
        }
        File.Move(temporary, path, true);
    }

    public void Dispose() => writerLock.Dispose();
}
