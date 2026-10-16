using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Queued11 retains the selected crisis until48D200 prepares its control header and second241 packet.</summary>
public sealed record BattleNativeDuelInvocation([property: JsonRequired] byte CrisisLevel);
