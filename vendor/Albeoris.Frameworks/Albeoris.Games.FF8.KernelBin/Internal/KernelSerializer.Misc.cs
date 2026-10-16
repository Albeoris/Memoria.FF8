using Albeoris.Games.FF8.KernelBin.Abstractions;
using Albeoris.Games.FF8.KernelBin.Internal.Raw;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Maps between the public model types and the raw binary sections for Selphie's Slot limit
/// break, monster-devour effects, global timer settings, and the miscellaneous text section.
/// </summary>
internal static partial class KernelSerializer
{
    public static List<SelphieSlotSet> ReadSelphieSlotSets(SelphieSlotSetRaw[] raw)
    {
        List<SelphieSlotSet> result = new List<SelphieSlotSet>(raw.Length);
        foreach (SelphieSlotSetRaw item in raw)
        {
            SelphieSlotSet set = new();
            set.Slots[0] = ToModel(item.Slot1);
            set.Slots[1] = ToModel(item.Slot2);
            set.Slots[2] = ToModel(item.Slot3);
            set.Slots[3] = ToModel(item.Slot4);
            set.Slots[4] = ToModel(item.Slot5);
            set.Slots[5] = ToModel(item.Slot6);
            set.Slots[6] = ToModel(item.Slot7);
            set.Slots[7] = ToModel(item.Slot8);
            result.Add(set);
        }

        return result;
    }

    public static SelphieSlotSetRaw[] WriteSelphieSlotSets(List<SelphieSlotSet> sets)
    {
        SelphieSlotSetRaw[] result = new SelphieSlotSetRaw[sets.Count];
        for (Int32 i = 0; i < sets.Count; i++)
        {
            SelphieSlotSet set = sets[i];
            if (set.Slots.Length != 8)
                throw new InvalidOperationException($"SelphieSlotSet.Slots must contain exactly 8 entries, but found {set.Slots.Length}.");

            SelphieSlotSetRaw raw = new();
            raw.Slot1 = ToRaw(set.Slots[0]);
            raw.Slot2 = ToRaw(set.Slots[1]);
            raw.Slot3 = ToRaw(set.Slots[2]);
            raw.Slot4 = ToRaw(set.Slots[3]);
            raw.Slot5 = ToRaw(set.Slots[4]);
            raw.Slot6 = ToRaw(set.Slots[5]);
            raw.Slot7 = ToRaw(set.Slots[6]);
            raw.Slot8 = ToRaw(set.Slots[7]);
            result[i] = raw;
        }

        return result;
    }

    private static SelphieMagicCount ToModel(SelphieMagicCountRaw raw)
    {
        SelphieMagicCount count = new();
        count.MagicId = raw.MagicId;
        count.Count = raw.Count;
        return count;
    }

    private static SelphieMagicCountRaw ToRaw(SelphieMagicCount count)
    {
        SelphieMagicCountRaw raw = new();
        raw.MagicId = count.MagicId;
        raw.Count = count.Count;
        return raw;
    }

    public static List<DevourEffect> ReadDevourEffects(DevourEffectRaw[] raw, KernelTextBlobReader text)
    {
        List<DevourEffect> result = new List<DevourEffect>(raw.Length);
        foreach (DevourEffectRaw item in raw)
        {
            DevourEffect effect = new();
            effect.Description = text.ReadString(item.OffsetDescription);
            effect.Effect = item.Effect;
            effect.Quantity = item.Quantity;
            effect.Statuses1 = item.Statuses1;
            effect.Statuses0 = item.Statuses0;
            effect.StatFlags = item.StatFlags;
            effect.Hp = item.Hp;
            result.Add(effect);
        }

        return result;
    }

    public static DevourEffectRaw[] WriteDevourEffects(List<DevourEffect> effects, KernelTextBlobWriter text)
    {
        DevourEffectRaw[] result = new DevourEffectRaw[effects.Count];
        for (Int32 i = 0; i < effects.Count; i++)
        {
            DevourEffect effect = effects[i];
            DevourEffectRaw raw = new();
            raw.OffsetDescription = text.Write(effect.Description);
            raw.Effect = effect.Effect;
            raw.Quantity = effect.Quantity;
            raw.Statuses1 = effect.Statuses1;
            raw.Statuses0 = effect.Statuses0;
            raw.StatFlags = effect.StatFlags;
            raw.Hp = effect.Hp;
            result[i] = raw;
        }

        return result;
    }

    public static TimerSettings ReadTimerSettings(TimerSettingsRaw[] raw)
    {
        if (raw.Length != 1)
            throw new InvalidDataException($"Expected exactly 1 kernel.bin timer settings record, but found {raw.Length}.");

        TimerSettingsRaw item = raw[0];
        TimerSettings settings = new();
        unsafe
        {
            for (Int32 i = 0; i < settings.StatusTimers.Length; i++)
                settings.StatusTimers[i] = item.StatusTimers[i];
            for (Int32 i = 0; i < settings.StatusLimitEffects.Length; i++)
                settings.StatusLimitEffects[i] = item.StatusLimitEffects[i];
            for (Int32 i = 0; i < settings.DuelTimersAndStartMoves.Length; i++)
                settings.DuelTimersAndStartMoves[i] = item.DuelTimersAndStartMoves[i];
            for (Int32 i = 0; i < settings.ShotTimers.Length; i++)
                settings.ShotTimers[i] = item.ShotTimers[i];
        }

        settings.AtbSpeedMultiplier = item.AtbSpeedMultiplier;
        settings.DeadTimer = item.DeadTimer;
        return settings;
    }

    public static TimerSettingsRaw[] WriteTimerSettings(TimerSettings settings)
    {
        if (settings.StatusTimers.Length != 14)
            throw new InvalidOperationException($"TimerSettings.StatusTimers must contain exactly 14 entries, but found {settings.StatusTimers.Length}.");
        if (settings.StatusLimitEffects.Length != 32)
            throw new InvalidOperationException($"TimerSettings.StatusLimitEffects must contain exactly 32 entries, but found {settings.StatusLimitEffects.Length}.");
        if (settings.DuelTimersAndStartMoves.Length != 8)
            throw new InvalidOperationException($"TimerSettings.DuelTimersAndStartMoves must contain exactly 8 entries, but found {settings.DuelTimersAndStartMoves.Length}.");
        if (settings.ShotTimers.Length != 4)
            throw new InvalidOperationException($"TimerSettings.ShotTimers must contain exactly 4 entries, but found {settings.ShotTimers.Length}.");

        TimerSettingsRaw raw = new();
        unsafe
        {
            for (Int32 i = 0; i < settings.StatusTimers.Length; i++)
                raw.StatusTimers[i] = settings.StatusTimers[i];
            for (Int32 i = 0; i < settings.StatusLimitEffects.Length; i++)
                raw.StatusLimitEffects[i] = settings.StatusLimitEffects[i];
            for (Int32 i = 0; i < settings.DuelTimersAndStartMoves.Length; i++)
                raw.DuelTimersAndStartMoves[i] = settings.DuelTimersAndStartMoves[i];
            for (Int32 i = 0; i < settings.ShotTimers.Length; i++)
                raw.ShotTimers[i] = settings.ShotTimers[i];
        }

        raw.AtbSpeedMultiplier = settings.AtbSpeedMultiplier;
        raw.DeadTimer = settings.DeadTimer;
        return new TimerSettingsRaw[] { raw };
    }

    public static List<String?> ReadMiscTexts(UInt16[] pointers, KernelTextBlobReader text)
    {
        List<String?> result = new List<String?>(pointers.Length);
        foreach (UInt16 offset in pointers)
            result.Add(text.ReadString(offset));

        return result;
    }

    public static UInt16[] WriteMiscTexts(List<String?> texts, KernelTextBlobWriter text)
    {
        UInt16[] result = new UInt16[texts.Count];
        for (Int32 i = 0; i < texts.Count; i++)
            result[i] = text.Write(texts[i]);

        return result;
    }
}
