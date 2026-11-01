namespace Memoria.FF8.GameUi;

public enum JunctionEffectFamily
{
    Elemental,
    Status
}

public enum JunctionEffectDirection
{
    Attack,
    Defense
}

/// <summary>Presentation-only state. Pins and inspection never change a saved assignment.</summary>
public sealed class JunctionDashboardState
{
    private readonly Dictionary<string, HashSet<string>> pins = new(StringComparer.Ordinal);
    private readonly Dictionary<JunctionEffectFamily, string> effectDestinations = [];
    private object? sessionIdentity;

    public void SetSession(object? identity)
    {
        if (Equals(sessionIdentity, identity))
        {
            return;
        }
        sessionIdentity = identity;
        pins.Clear();
        effectDestinations.Clear();
        Destination = "";
        ElementalDirection = JunctionEffectDirection.Defense;
        StatusDirection = JunctionEffectDirection.Defense;
    }

    public string Destination { get; private set; } = "";
    public JunctionEffectDirection ElementalDirection { get; private set; } = JunctionEffectDirection.Defense;
    public JunctionEffectDirection StatusDirection { get; private set; } = JunctionEffectDirection.Defense;

    public bool SetDestination(string semanticSlot)
    {
        Destination = semanticSlot;
        JunctionEffectFamily? family = semanticSlot.StartsWith("Elem-", StringComparison.Ordinal) ? JunctionEffectFamily.Elemental : semanticSlot.StartsWith("ST-", StringComparison.Ordinal) ? JunctionEffectFamily.Status : null;
        if (family is not { } relevant || effectDestinations.GetValueOrDefault(relevant) == semanticSlot)
        {
            return false;
        }
        if (semanticSlot is not ("Elem-Atk" or "ST-Atk") && !IsDefenseSlot(semanticSlot))
        {
            return false;
        }
        effectDestinations[relevant] = semanticSlot;
        JunctionEffectDirection direction = semanticSlot.EndsWith("-Atk", StringComparison.Ordinal) ? JunctionEffectDirection.Attack : JunctionEffectDirection.Defense;
        bool changed = (relevant == JunctionEffectFamily.Elemental ? ElementalDirection : StatusDirection) != direction;
        SetDirection(relevant, direction);
        return changed;
    }

    public void SetDirection(JunctionEffectFamily family, JunctionEffectDirection direction)
    {
        if (family == JunctionEffectFamily.Elemental)
        {
            ElementalDirection = direction;
        }
        else
        {
            StatusDirection = direction;
        }
    }

    public bool IsPinned(string recordId, string slot) => pins.TryGetValue(recordId, out HashSet<string>? recordPins) && recordPins.Contains(slot);

    public IReadOnlySet<string> GetPinnedSlots(string recordId) => pins.TryGetValue(recordId, out HashSet<string>? recordPins) ? new HashSet<string>(recordPins, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);

    public void SetPinned(string recordId, string slot, bool pinned)
    {
        if (slot is not ("HP" or "Str" or "Vit" or "Mag" or "Spr" or "Spd" or "Eva" or "Hit" or "Luck"))
        {
            throw new ArgumentException("Only stat junctions can be pinned for optimization.", nameof(slot));
        }
        if (!pins.TryGetValue(recordId, out HashSet<string>? recordPins))
        {
            recordPins = new(StringComparer.Ordinal);
            pins.Add(recordId, recordPins);
        }
        if (pinned)
        {
            recordPins.Add(slot);
        }
        else
        {
            recordPins.Remove(slot);
        }
    }

    private static bool IsDefenseSlot(string slot) => slot is "Elem-Def 1" or "Elem-Def 2" or "Elem-Def 3" or "Elem-Def 4" or "ST-Def 1" or "ST-Def 2" or "ST-Def 3" or "ST-Def 4";
}
