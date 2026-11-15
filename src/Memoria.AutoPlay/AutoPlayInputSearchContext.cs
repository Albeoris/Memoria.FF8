using System.Text.Json.Serialization;

namespace Memoria.AutoPlay;

public sealed class AutoPlayInputSearchContext
{
    public string? Visit { get; set; }
    public string? Navigation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutoPlayNavigationEventSource? NavigationSource { get; set; }
    public string? Event { get; set; }
    public int Field { get; set; }
    public int? Story { get; set; }
    public int ObjectId { get; set; }
    public string? Conditions { get; set; }
    public string? BeforeCheckpoint { get; set; }
    public string? Target { get; set; }
    public string? AbsenceGuard { get; set; }
    public string? Availability { get; set; }
    public bool ObservedControlLoss { get; set; }
    public List<string> SceneChoices { get; set; } = [];
}
