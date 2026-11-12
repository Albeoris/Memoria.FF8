using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public enum Ff7EnemyIntegrationPreference
{
    Automatic,
    Enabled,
    Disabled
}
public sealed record Ff7EnemyIntegrationContext(Ff7EnemyIntegrationPreference Preference = Ff7EnemyIntegrationPreference.Automatic, bool EnteredFromFf8 = false)
{
    public bool Effective => Preference == Ff7EnemyIntegrationPreference.Enabled || Preference == Ff7EnemyIntegrationPreference.Automatic && EnteredFromFf8;
}
public sealed record Ff7EnemyDrawSlot(byte MagicId, byte Resistance);
public sealed record Ff7EnemyDrawTier(int MinimumDelta, Ff7EnemyDrawSlot[] Slots);
public sealed record Ff7EnemyItemReplacement(int MinimumDelta, string Channel, [property: JsonRequired] short FromItemId, [property: JsonRequired] short ToItemId);
public sealed record Ff7EnemyExtensionDelta
{
    [JsonRequired]
    public short EnemyId { get; init; }
    public string? ScanDescription { get; init; }
    public bool? ScaleLevel { get; init; }
    public Dictionary<string, int[]>? Growth { get; init; }
    public Dictionary<string, int[]>? RewardGrowth { get; init; }
    public Ff7EnemyDrawTier[]? DrawTiers { get; init; }
    public Ff7EnemyItemReplacement[]? ItemReplacements { get; init; }
}
public sealed record Ff7EnemyExtensionDocument(int SchemaVersion, string DocumentId, Ff7EnemyExtensionDelta[] Enemies)
{
    public static Ff7EnemyExtensionDocument Parse(string json)
    {
        using JsonDocument syntax = JsonDocument.Parse(json);
        void RejectDuplicates(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                HashSet<string> names = [];
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new InvalidDataException($"Duplicate FFVII extension property {property.Name}.");
                    }
                    RejectDuplicates(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    RejectDuplicates(item);
                }
            }
        }
        RejectDuplicates(syntax.RootElement);
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        Ff7EnemyExtensionDocument document = JsonSerializer.Deserialize<Ff7EnemyExtensionDocument>(json, options) ?? throw new InvalidDataException("An FFVII enemy extension document is required.");
        if (document.SchemaVersion != 1 || string.IsNullOrWhiteSpace(document.DocumentId) || document.Enemies is null || document.Enemies.Any(e => e is null) || document.Enemies.Select(e => e.EnemyId).Distinct().Count() != document.Enemies.Length)
        {
            throw new InvalidDataException("Invalid FFVII enemy extension document identity or duplicate enemy ID.");
        }
        foreach (Ff7EnemyExtensionDelta enemy in document.Enemies)
        {
            if (enemy is null || enemy.EnemyId < 0 || enemy.ScanDescription is not null && string.IsNullOrWhiteSpace(enemy.ScanDescription))
            {
                throw new InvalidDataException("Enemy extension IDs and descriptions must be valid.");
            }
            foreach (var row in enemy.Growth ?? [])
            {
                if (row.Key is not ("hp" or "attack" or "magic" or "mp") || row.Value is null || row.Value.Length != 4 || row.Value[0] != 1_000_000 || row.Value.Any(v => v < 0))
                {
                    throw new InvalidDataException("Enemy growth requires supported anchored fixed-point coefficient rows.");
                }
            }
            foreach (var row in enemy.RewardGrowth ?? [])
            {
                if (row.Key is not ("exp" or "ap" or "gil") || row.Value is null || row.Value.Length != 4 || row.Value[0] != 1_000_000 || row.Value.Any(v => v < 0))
                {
                    throw new InvalidDataException("Reward growth requires supported native-unit fixed-point rows.");
                }
            }
            int previous = -1;
            foreach (Ff7EnemyDrawTier tier in enemy.DrawTiers ?? [])
            {
                if (tier is null || tier.MinimumDelta <= previous || tier.MinimumDelta > 100 || tier.Slots is null || tier.Slots.Length > 4 || tier.Slots.Any(s => s is null || s.MagicId is < 1 or > 56) || tier.Slots.Select(s => s.MagicId).Distinct().Count() != tier.Slots.Length)
                {
                    throw new InvalidDataException("Enemy Draw tiers require increasing deltas and up to four distinct FFVIII stock spells.");
                }
                previous = tier.MinimumDelta;
            }
            foreach (Ff7EnemyItemReplacement replacement in enemy.ItemReplacements ?? [])
            {
                if (replacement is null || replacement.MinimumDelta is <= 0 or > 100 || replacement.Channel is not ("drop" or "steal" or "morph") || replacement.FromItemId is < 0 or > 638 || replacement.ToItemId is < 0 or > 638)
                {
                    throw new InvalidDataException("Item deltas require positive growth and a native item channel.");
                }
            }
            if (enemy.ItemReplacements is { } replacements && replacements.Select(r => (r.Channel, r.FromItemId, r.MinimumDelta)).Distinct().Count() != replacements.Length)
            {
                throw new InvalidDataException("Duplicate item delta channel/source/threshold.");
            }
        }
        return document;
    }
}

public sealed class Ff7EnemyExtensionCatalog
{
    public static Ff7EnemyExtensionCatalog Empty { get; } = new([]);
    private readonly Dictionary<short, Ff7EnemyExtensionDelta> enemies = [];
    public int Count => enemies.Count;

    public Ff7EnemyExtensionCatalog(IEnumerable<Ff7EnemyExtensionDocument> documents)
    {
        HashSet<string> ids = [];
        Dictionary<short, List<string>> owners = [];
        foreach (Ff7EnemyExtensionDocument document in documents)
        {
            if (!ids.Add(document.DocumentId))
            {
                throw new InvalidDataException($"Duplicate FFVII document {document.DocumentId}.");
            }
            foreach (Ff7EnemyExtensionDelta delta in document.Enemies)
            {
                if (!enemies.TryGetValue(delta.EnemyId, out Ff7EnemyExtensionDelta? existing))
                {
                    enemies.Add(delta.EnemyId, Clone(delta));
                    owners[delta.EnemyId] = [document.DocumentId];
                    continue;
                }
                bool itemOverlap = (existing.ItemReplacements ?? []).Any(a => (delta.ItemReplacements ?? []).Any(b => (a.Channel, a.FromItemId, a.MinimumDelta) == (b.Channel, b.FromItemId, b.MinimumDelta)));
                if (existing.ScanDescription is not null && delta.ScanDescription is not null || existing.ScaleLevel is not null && delta.ScaleLevel is not null || existing.DrawTiers is not null && delta.DrawTiers is not null || itemOverlap || (existing.Growth?.Keys.Intersect(delta.Growth?.Keys.AsEnumerable() ?? []).Any() ?? false) || (existing.RewardGrowth?.Keys.Intersect(delta.RewardGrowth?.Keys.AsEnumerable() ?? []).Any() ?? false))
                {
                    throw new InvalidDataException($"Conflicting FFVII extension properties for enemy {delta.EnemyId}: {string.Join(", ", owners[delta.EnemyId])} and {document.DocumentId}.");
                }
                Dictionary<string, int[]> growth = existing.Growth is null ? [] : new(existing.Growth);
                foreach (var row in delta.Growth ?? [])
                {
                    growth.Add(row.Key, row.Value);
                }
                Dictionary<string, int[]> rewards = existing.RewardGrowth is null ? [] : new(existing.RewardGrowth);
                foreach (var row in delta.RewardGrowth ?? [])
                {
                    rewards.Add(row.Key, row.Value);
                }
                enemies[delta.EnemyId] = Clone(existing with { ScanDescription = delta.ScanDescription ?? existing.ScanDescription, ScaleLevel = delta.ScaleLevel ?? existing.ScaleLevel, DrawTiers = delta.DrawTiers ?? existing.DrawTiers, ItemReplacements = [.. existing.ItemReplacements ?? [], .. delta.ItemReplacements ?? []], Growth = growth, RewardGrowth = rewards });
                owners[delta.EnemyId].Add(document.DocumentId);
            }
        }
    }

    private static Ff7EnemyExtensionDelta Clone(Ff7EnemyExtensionDelta source) => source with { Growth = source.Growth?.ToDictionary(p => p.Key, p => p.Value.ToArray()), RewardGrowth = source.RewardGrowth?.ToDictionary(p => p.Key, p => p.Value.ToArray()), DrawTiers = source.DrawTiers?.Select(t => t with { Slots = t.Slots.ToArray() }).ToArray(), ItemReplacements = source.ItemReplacements?.ToArray() };

    public Ff7EnemyExtensionDelta? Find(short id) => enemies.TryGetValue(id, out Ff7EnemyExtensionDelta? value) ? Clone(value) : null;

    public static int Scale(int baseline, int level, int baseLevel, int[] coefficients, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return checked((int)ScaleUnsigned((uint)baseline, level, baseLevel, coefficients, (uint)maximum));
    }

    public static uint ScaleUnsigned(uint baseline, int level, int baseLevel, int[]? coefficients, uint maximum)
    {
        if (coefficients is null || level <= baseLevel || baseline == 0)
        {
            return baseline;
        }
        Int128 d = Math.Max(1, 100 - baseLevel);
        Int128 n = Math.Min(level - baseLevel, (int)d);
        Int128 numerator = checked(coefficients[0] * d * d * d + coefficients[1] * n * d * d + coefficients[2] * n * n * d + coefficients[3] * n * n * n);
        Int128 value = checked(baseline * numerator) / (1_000_000 * d * d * d);
        uint ceiling = Math.Max(baseline, maximum);
        return value >= ceiling ? ceiling : checked((uint)value);
    }
}

public sealed record Ff7ResolvedEnemyExtension(int ActorSlot, short EnemyId, byte BaseLevel, byte Level, string? ScanDescription, Ff7EnemyDrawSlot[] DrawSlots, uint Hp, ushort Mp, byte Attack, byte Magic)
{
    public uint Experience { get; init; }
    public uint Gil { get; init; }
    public ushort AbilityPoints { get; init; }
    public SceneItemSlot[] Items { get; init; } = [];
    public short? MorphItemId { get; init; }
}

public sealed partial class Ff7BattleSession
{
    public Ff7ResolvedEnemyExtension? EnemyExtension(int slot)
    {
        Ff7ResolvedEnemyExtension? extension = state.EnemyExtensions?.SingleOrDefault(e => e.ActorSlot == slot);
        return extension is null ? null : extension with { DrawSlots = extension.DrawSlots.ToArray(), Items = extension.Items.ToArray() };
    }

    private static SceneBattleData ApplyEnemyExtensions(SceneBattleData source, Ff7ResolvedEnemyExtension[] extensions)
    {
        if (extensions.Any(e => e is null || !source.Enemies.Any(enemy => enemy.FormationSlot + 4 == e.ActorSlot)) || extensions.Select(e => e.ActorSlot).Distinct().Count() != extensions.Length)
        {
            throw new InvalidDataException("Resolved enemy extension allocations must match the source encounter.");
        }
        return source with { Enemies = source.Enemies.Select(enemy =>
        {
            Ff7ResolvedEnemyExtension? extension = extensions.SingleOrDefault(e => e.ActorSlot == enemy.FormationSlot + 4);
            if (extension is null)
            {
                return enemy;
            }
            if (extension.EnemyId != enemy.Placement.EnemyId || extension.Level < extension.BaseLevel || extension.DrawSlots is null || extension.Items is null || extension.DrawSlots.Any(slot => slot is null || slot.MagicId is < 1 or > 56))
            {
                throw new InvalidDataException("A resolved enemy extension does not match its source actor or level/Draw contract.");
            }
            ReadOnlyMemory<byte> raw = enemy.Rewards.Raw;
            if (extension.MorphItemId is short morph)
            {
                byte[] copy = raw.ToArray();
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(copy.AsSpan(160, 2), morph);
                raw = copy;
            }
            return enemy with { Level = extension.Level, MaxHitPoints = extension.Hp, MaxMagicPoints = extension.Mp, Strength = extension.Attack, Magic = extension.Magic, Rewards = enemy.Rewards with { Experience = extension.Experience, Gil = extension.Gil, AbilityPoints = extension.AbilityPoints, Items = extension.Items, Raw = raw } };
        }).ToArray() };
    }

    private static Ff7ResolvedEnemyExtension[] ResolveEnemyExtensions(SceneBattleData source, Ff7EnemyExtensionCatalog? catalog, IReadOnlyList<byte>? partyLevels)
    {
        if (catalog is null || partyLevels is not { Count: > 0 })
        {
            return [];
        }
        int partyLevel = partyLevels.Sum(l => (int)l) / partyLevels.Count;
        List<Ff7ResolvedEnemyExtension> result = [];
        foreach (SceneBattleEnemy enemy in source.Enemies)
        {
            Ff7EnemyExtensionDelta? delta = catalog.Find(enemy.Placement.EnemyId);
            if (delta is null)
            {
                continue;
            }
            bool scaleLevel = delta.ScaleLevel ?? delta.Growth is { Count: > 0 };
            byte level = scaleLevel ? (byte)Math.Max(enemy.Level, Math.Min(100, partyLevel)) : enemy.Level;
            int Grow(string stat, int value, int maximum) => delta.Growth?.TryGetValue(stat, out int[]? row) == true ? Ff7EnemyExtensionCatalog.Scale(value, level, enemy.Level, row, maximum) : value;
            Ff7EnemyDrawSlot[] draw = delta.DrawTiers?.LastOrDefault(t => t.MinimumDelta <= level - enemy.Level)?.Slots ?? [];
            uint Reward(string channel, uint value, uint maximum) => Ff7EnemyExtensionCatalog.ScaleUnsigned(value, level, enemy.Level, delta.RewardGrowth?.GetValueOrDefault(channel), maximum);
            SceneItemSlot[] items = enemy.Rewards.Items.Select(item =>
            {
                string channel = (item.Rate & 128) != 0 ? "steal" : "drop";
                Ff7EnemyItemReplacement? replacement = delta.ItemReplacements?.Where(r => r.Channel == channel && r.FromItemId == item.ItemId && r.MinimumDelta <= level - enemy.Level).OrderBy(r => r.MinimumDelta).LastOrDefault();
                return replacement is null ? item : item with { ItemId = replacement.ToItemId };
            }).ToArray();
            short nativeMorph = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(enemy.Rewards.Raw.Span.Slice(160, 2));
            Ff7EnemyItemReplacement? morphReplacement = delta.ItemReplacements?.Where(r => r.Channel == "morph" && r.FromItemId == nativeMorph && r.MinimumDelta <= level - enemy.Level).OrderBy(r => r.MinimumDelta).LastOrDefault();
            uint hp = Ff7EnemyExtensionCatalog.ScaleUnsigned(enemy.MaxHitPoints, level, enemy.Level, delta.Growth?.GetValueOrDefault("hp"), int.MaxValue);
            result.Add(new(enemy.FormationSlot + 4, enemy.Placement.EnemyId, enemy.Level, level, delta.ScanDescription, draw, hp, (ushort)Grow("mp", enemy.MaxMagicPoints, ushort.MaxValue), (byte)Grow("attack", enemy.Strength, byte.MaxValue), (byte)Grow("magic", enemy.Magic, byte.MaxValue)) { Experience = Reward("exp", enemy.Rewards.Experience, uint.MaxValue), Gil = Reward("gil", enemy.Rewards.Gil, uint.MaxValue), AbilityPoints = (ushort)Reward("ap", enemy.Rewards.AbilityPoints, ushort.MaxValue), Items = items, MorphItemId = morphReplacement?.ToItemId });
        }
        return result.ToArray();
    }
}
