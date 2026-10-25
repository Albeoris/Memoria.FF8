namespace Memoria.FF8.Runtime;

/// <summary>Publisher-defined keys are edition compatibility data, separate from gameplay rules.</summary>
public static class SteamAchievementMappings
{
    public static AchievementBinding[] ForProduct(string? productId)
    {
        return productId switch
        {
            "39150" => OriginalAchievementCatalog.Entries.Where(entry => entry.Supported).Select(entry => new AchievementBinding(entry.Id, entry.NativeKey, entry.Index switch { 16 => "mag_stck", 35 => "won_card", 38 => "mag_draw", 42 => "nmy_kill", _ => null })).ToArray(),
            "1026680" => [new(AchievementRegistry.EnemiesDefeated100, "NEW_ACHIEVEMENT_1_10")],
            _ => []
        };
    }
}
