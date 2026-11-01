using System.Text.Json;

namespace Memoria.FF8.GameUi;

/// <param name="Commands">Three configurable command ability IDs in slot order; empty is 0 or null.</param>
public sealed record JunctionCommandConfiguration(string RecordId, IReadOnlyList<string?> Commands, IReadOnlyCollection<string> AvailableCommands, bool SmartMagic = false, string MagicCommandId = "20", string DrawCommandId = "22");

public sealed record JunctionCommandAttention(string RecordId, int EquippedCount, IReadOnlyList<int> ActionableEmptySlots, bool MissingSmartMagicDependency)
{
    public bool NeedsAttention => ActionableEmptySlots.Count > 0 || MissingSmartMagicDependency;
}

/// <summary>Command assistance and acknowledgments belonging to one loaded-game session.</summary>
public sealed class JunctionCommandAudit
{
    private readonly Dictionary<string, string> acknowledgments = new(StringComparer.Ordinal);
    private readonly HashSet<string> affectedRecords = new(StringComparer.Ordinal);
    private object? sessionIdentity;

    public void SetSession(object? identity)
    {
        if (Equals(sessionIdentity, identity))
        {
            return;
        }
        sessionIdentity = identity;
        ResetSession();
    }

    public void BeginVisit() => affectedRecords.Clear();

    public void MarkAffected(string recordId) => affectedRecords.Add(recordId);

    public void ResetSession()
    {
        acknowledgments.Clear();
        affectedRecords.Clear();
    }

    public static JunctionCommandAttention Inspect(JunctionCommandConfiguration configuration)
    {
        Validate(configuration);
        HashSet<string> equipped = configuration.Commands.Where(id => !IsEmpty(id)).Select(id => id!).ToHashSet(StringComparer.Ordinal);
        int alternatives = configuration.AvailableCommands.Where(id => !IsEmpty(id) && !equipped.Contains(id)).Distinct(StringComparer.Ordinal).Count();
        int[] actionable = alternatives == 0 ? [] : Enumerable.Range(0, 3).Where(slot => IsEmpty(configuration.Commands[slot])).ToArray();
        bool missingDependency = configuration.SmartMagic && !equipped.Contains(configuration.MagicCommandId) && !equipped.Contains(configuration.DrawCommandId);
        return new(configuration.RecordId, configuration.Commands.Count(id => !IsEmpty(id)), actionable, missingDependency);
    }

    public bool RequiresReview(JunctionCommandConfiguration configuration) => Inspect(configuration).NeedsAttention && acknowledgments.GetValueOrDefault(configuration.RecordId) != Fingerprint(configuration);

    public void Acknowledge(JunctionCommandConfiguration configuration) => acknowledgments[configuration.RecordId] = Fingerprint(configuration);

    public IReadOnlyList<JunctionCommandAttention> Review(string selectedRecordId, IEnumerable<JunctionCommandConfiguration> configurations)
    {
        return configurations.Where(configuration => configuration.RecordId == selectedRecordId || affectedRecords.Contains(configuration.RecordId)).Where(RequiresReview).Select(Inspect).ToArray();
    }

    public static IReadOnlyList<string?> ProposeFill(JunctionCommandConfiguration configuration)
    {
        Validate(configuration);
        string?[] result = configuration.Commands.ToArray();
        HashSet<string> equipped = result.Where(id => !IsEmpty(id)).Select(id => id!).ToHashSet(StringComparer.Ordinal);
        HashSet<string> available = configuration.AvailableCommands.ToHashSet(StringComparer.Ordinal);
        Queue<string> additions = new(new[] { "20", "22", "23", "21" }.Where(id => available.Contains(id) && !equipped.Contains(id)));
        for (int slot = 0; slot < result.Length && additions.Count > 0; slot++)
        {
            if (IsEmpty(result[slot]))
            {
                result[slot] = additions.Dequeue();
            }
        }
        return result;
    }

    private static string Fingerprint(JunctionCommandConfiguration configuration)
    {
        Validate(configuration);
        return JsonSerializer.Serialize(new
        {
            Commands = configuration.Commands.Select(id => IsEmpty(id) ? "0" : id).ToArray(),
            Capabilities = configuration.AvailableCommands.Where(id => !IsEmpty(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            configuration.SmartMagic,
            configuration.MagicCommandId,
            configuration.DrawCommandId
        });
    }

    private static bool IsEmpty(string? id) => string.IsNullOrEmpty(id) || id == "0";

    private static void Validate(JunctionCommandConfiguration configuration)
    {
        if (configuration.Commands.Count != 3)
        {
            throw new ArgumentException("A command configuration must contain exactly three configurable positions.", nameof(configuration));
        }
    }
}
