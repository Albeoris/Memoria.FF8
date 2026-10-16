using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldBarMirror([property: JsonRequired] short Mode, [property: JsonRequired] short Value, [property: JsonRequired] short Maximum, [property: JsonRequired] short Minimum, [property: JsonRequired] short Flags, [property: JsonRequired] short Y, [property: JsonRequired] short X);
public sealed record NativeFieldBarDisplay([property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] short Minimum, [property: JsonRequired] short Maximum, [property: JsonRequired] short Value, [property: JsonRequired] short Width, [property: JsonRequired] short Fill, [property: JsonRequired] byte Flags)
{
    [JsonIgnore]
    public bool Visible => (Flags & 0x80) != 0;

    [JsonIgnore]
    public bool Flash => (Flags & 1) != 0 && (Flags & 0x40) != 0 && NativeFieldBarsState.Scale(Value, Minimum, Maximum, Width) <= Width / 4;
}

public sealed record NativeFieldBarSlot([property: JsonRequired] NativeFieldBarMirror Mirror, [property: JsonRequired] NativeFieldBarDisplay Display);

/// <summary>The two global native field-bar mirror and UI records; null slots are hidden with otherwise opaque words.</summary>
public sealed record NativeFieldBarsState([property: JsonRequired] int Version, [property: JsonRequired] NativeFieldBarSlot? First, [property: JsonRequired] NativeFieldBarSlot? Second)
{
    public static NativeFieldBarsState CreateHidden() => new(1, null, null);

    [JsonIgnore]
    public IEnumerable<NativeFieldBarDisplay> VisibleBars => new[] { First, Second }.OfType<NativeFieldBarSlot>().Select(slot => slot.Display).Where(display => display.Visible);

    public void Validate()
    {
        if (Version != 1)
        {
            throw new InvalidDataException("Unsupported native field bar version.");
        }
        foreach (NativeFieldBarSlot slot in new[] { First, Second }.OfType<NativeFieldBarSlot>())
        {
            if (slot.Mirror is null || slot.Display is null || slot.Mirror.Mode is not (0 or 1) || slot.Display.Width != 96 || (slot.Mirror.Mode == 1) != slot.Display.Visible)
            {
                throw new InvalidDataException("Native field bar mirror and display visibility disagree or have unsupported geometry.");
            }
            if (slot.Mirror.X != slot.Display.X || slot.Mirror.Y != slot.Display.Y || slot.Mirror.Minimum != slot.Display.Minimum || slot.Mirror.Maximum != slot.Display.Maximum || slot.Mirror.Value != slot.Display.Value || (unchecked((byte)slot.Mirror.Flags) & 0x3F) != (slot.Display.Flags & 0x3F))
            {
                throw new InvalidDataException("Native field bar UI words disagree with their retained script mirror.");
            }
        }
    }

    public NativeFieldBarsState Display(int id, int x, int y, int flags, int minimum, int maximum, int value)
    {
        RequireId(id);
        NativeFieldBarMirror mirror = new(1, unchecked((short)value), unchecked((short)maximum), unchecked((short)minimum), unchecked((short)flags), unchecked((short)y), unchecked((short)x));
        NativeFieldBarDisplay display = new(unchecked((short)x), unchecked((short)y), unchecked((short)minimum), unchecked((short)maximum), unchecked((short)value), 96, unchecked((short)Scale(value, minimum, maximum, 96)), unchecked((byte)(flags | 0x80)));
        return Set(id, new(mirror, display));
    }

    public NativeFieldBarsState SetValue(int id, int value)
    {
        RequireId(id);
        NativeFieldBarSlot slot = Get(id) ?? throw new NotSupportedException("Native SETBAR requires its retained UI min/max/width words from an executed display command.");
        int fill = Scale(value, slot.Display.Minimum, slot.Display.Maximum, slot.Display.Width);
        NativeFieldBarDisplay display = slot.Display with { Value = unchecked((short)value), Fill = (slot.Display.Flags & 2) != 0 ? unchecked((short)fill) : slot.Display.Fill };
        return Set(id, new(slot.Mirror with { Value = unchecked((short)value) }, display));
    }

    public NativeFieldBarsState Hide(int id)
    {
        RequireId(id);
        NativeFieldBarSlot? slot = Get(id);
        return slot is null ? this : Set(id, new(slot.Mirror with { Mode = 0 }, slot.Display with { Flags = (byte)(slot.Display.Flags & ~0x80) }));
    }

    /// <summary>49A990 runs the smoother once per UI tick, then49AB80/4B7A40 toggles low-value flash once per draw.</summary>
    public NativeFieldBarsState Advance(int uiTicks)
    {
        if (uiTicks is not (2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(uiTicks));
        }
        NativeFieldBarSlot? AdvanceSlot(NativeFieldBarSlot? slot)
        {
            if (slot?.Display.Visible != true)
            {
                return slot;
            }
            NativeFieldBarDisplay display = slot.Display;
            int target = Scale(display.Value, display.Minimum, display.Maximum, display.Width);
            int fill = display.Fill;
            for (int tick = 0; tick < uiTicks; tick++)
            {
                fill = fill < target ? Math.Min(fill + 2, target) : Math.Max(fill - 2, target);
                fill = unchecked((short)fill);
            }
            byte flags = display.Flags;
            if ((flags & 1) != 0 && target <= display.Width / 4)
            {
                flags ^= 0x40;
            }
            return slot with { Display = display with { Fill = unchecked((short)fill), Flags = flags } };
        }
        return this with { First = AdvanceSlot(First), Second = AdvanceSlot(Second) };
    }

    public static int Scale(int value, int minimum, int maximum, int width)
    {
        if (value < minimum)
        {
            return 0;
        }
        if (value >= maximum)
        {
            return width;
        }
        return unchecked(width * (value - minimum)) / unchecked(maximum - minimum);
    }

    private NativeFieldBarSlot? Get(int id) => id == 0 ? First : Second;
    private NativeFieldBarsState Set(int id, NativeFieldBarSlot slot) => id == 0 ? this with { First = slot } : this with { Second = slot };

    private static void RequireId(int id)
    {
        if (id is not (0 or 1))
        {
            throw new NotSupportedException("Native bar index addresses outside the two owned mirror/UI records.");
        }
    }
}
