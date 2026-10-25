namespace Memoria.FF8.Runtime;

/// <summary>Publisher-defined keys are edition compatibility data, separate from gameplay rules.</summary>
public static class SteamAchievementMappings
{
    public static AchievementBinding[] ForProduct(string? productId)
    {
        return productId switch
        {
            "39150" => [new(AchievementRegistry.EnemiesDefeated100, "TOTAL_KILLS_100", "nmy_kill")],
            "1026680" => [new(AchievementRegistry.EnemiesDefeated100, "NEW_ACHIEVEMENT_1_10")],
            _ => []
        };
    }
}
