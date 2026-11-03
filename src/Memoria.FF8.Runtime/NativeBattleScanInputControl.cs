namespace Memoria.FF8.Runtime;

/// <summary>A mapped button edge retained until one accepted native Scan input callback publishes it.</summary>
public sealed record NativeBattleScanControlPulse([property: System.Text.Json.Serialization.JsonRequired] BattleScanButton Button, long? PublishedCallback = null)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Button) || PublishedCallback < 0)
        {
            throw new InvalidDataException("Queued Scan input requires a supported button and a nonnegative native callback identity.");
        }
    }
}

public sealed record NativeBattleScanControlSample(NativeBattleScanControlPulse? Pending, ushort MappedButtons);

public static class NativeBattleScanInputControl
{
    public static bool IsReady(NativeBattleOriginalEffectState? effect, NativeBattleScanProcess? scan)
    {
        if (scan is not { InputInitialized: true, Text.Initialized: true } || effect is not { RequestedEffect: 40, Binding.Effect: 40, Binding.Entry: 0x84D030, ReturnedDescriptor: 0x269ABA8 })
        {
            return false;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            for (int index = 0; index < pool.Slots.Length; index++)
            {
                NativeBattleModulePoolTask viewer = pool.Slots[index];
                if ((viewer.Flags & 1) == 0 || viewer.Callback != 0x84F860)
                {
                    continue;
                }
                int phase = unchecked((sbyte)viewer.ReadByte(0x29));
                if (phase == 2)
                {
                    return true;
                }
                uint address = checked(pool.BankAddress + (uint)(index * pool.Stride));
                if (phase == 1 && effect.Pools.SelectMany(candidate => candidate.Slots).Any(result => (result.Flags & 1) != 0 && result.Callback == 0x84FAF0 && result.ReadByte(0x29) == 2 && result.ReadDword(0x18) == address))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static NativeBattleScanControlSample Sample(NativeBattleScanControlPulse pulse, long callback)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        pulse.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(callback);
        if (pulse.PublishedCallback is long published)
        {
            if (callback < published)
            {
                throw new InvalidDataException("Queued Scan input belongs to a future native callback; restore its matching captured callback owner.");
            }
            if (callback > published)
            {
                return new(null, 0);
            }
        }
        return new(pulse with { PublishedCallback = callback }, MappedButtons(pulse.Button));
    }

    public static ushort MappedButtons(BattleScanButton button) => button switch
    {
        BattleScanButton.Confirm => 0x40,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };
}
