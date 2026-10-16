using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeCommandBinding(byte Command, string Name, byte TargetFlags, byte ActionId);

/// <summary>Character/kernel inputs consumed by native4941F0,48CCE0 and48D200.</summary>
public sealed record BattleNativeLimitDefinition(int CharacterId, byte Command, byte Parameter, byte HitPointCoefficient, ImmutableArray<byte> StatusWeights)
{
    public ImmutableArray<BattleNativeCommandBinding> InvocationChoices { get; init; } = [];
    public ImmutableArray<byte> SlotsSelectionBytes { get; init; } = [];
    public ImmutableArray<byte> SlotsSpellBytes { get; init; } = [];
    public void Validate()
    {
        if (CharacterId is < 0 or > 10 || StatusWeights.IsDefault || StatusWeights.Length != 32)
        {
            throw new InvalidDataException("Native Limit availability requires its original character and all32 status weights.");
        }
    }
}

public static class NativeBattleLimitRules
{
    /// <summary>Native52CA90 copies field-block BYTE243 only while DWORD104 bit0x800 is set.</summary>
    public static byte ReadFieldBattleRules(NativeEventMemory memory)
    {
        NativeScalarValue flags = memory.Read(104, 4);
        if (!flags.IsExact)
        {
            throw new NotSupportedException("Native battle rules require the actual field-block flags DWORD104.");
        }
        if ((flags.Values[0] & 0x800) == 0)
        {
            return 0;
        }
        NativeScalarValue rules = memory.Read(243, 1);
        return rules.IsExact ? (byte)rules.Values[0] : throw new NotSupportedException("Native battle rules require the actual field-block BYTE243.");
    }

    /// <summary>Native483860/483880 retain random/KO/group markers alongside the selected physical slot.</summary>
    public static ushort PlayerTargetMask(byte targetFlags, int targetNativeIndex)
    {
        if (targetNativeIndex is < 0 or >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(targetNativeIndex));
        }
        int mask = 1 << targetNativeIndex;
        switch (targetFlags & 0x30)
        {
            case 0:
                mask |= 0x8000;
                break;
            case 0x10:
                break;
            case 0x20:
                mask = 0x80FF;
                break;
            default:
                throw new NotSupportedException("Original player target flags request an unresolved target mode.");
        }
        if ((targetFlags & 1) != 0)
        {
            mask |= 0x4000;
        }
        if ((targetFlags & 2) != 0)
        {
            mask |= 0x2000;
        }
        if ((targetFlags & 0x80) != 0)
        {
            mask |= 0x80;
        }
        return (ushort)mask;
    }

    /// <summary>Native4941F0 integer ordering; the caller owns whether this selection consumes a random byte.</summary>
    public static byte CrisisLevel(BattleNativeLimitDefinition definition, int currentHp, int maximumHp, ushort savedStatuses, uint battleStatuses, int deadPartyCount, byte randomByte, bool disabled = false, byte forcedLevel = 0)
    {
        definition.Validate();
        if (currentHp < 0 || maximumHp <= 0 || currentHp > maximumHp || deadPartyCount is < 0 or > 3 || forcedLevel > 4)
        {
            throw new InvalidDataException("Native Limit availability has invalid HP, party death count or forced level.");
        }
        if (forcedLevel != 0)
        {
            return forcedLevel;
        }
        if (disabled || (battleStatuses & 0x200) != 0)
        {
            return 0;
        }
        int statusModifier = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            if ((savedStatuses & (1 << bit)) != 0)
            {
                statusModifier += definition.StatusWeights[bit];
            }
        }
        for (int bit = 0; bit < 24; bit++)
        {
            if ((battleStatuses & (1u << bit)) != 0)
            {
                statusModifier += definition.StatusWeights[bit + 8];
            }
        }
        int crisis = (10 * (statusModifier + 4 * (5 * deadPartyCount + 40)) - 10 * definition.HitPointCoefficient * currentHp / maximumHp) / (randomByte + 160) - 4;
        return (byte)Math.Clamp(crisis, 0, 4);
    }

    /// <summary>Native48CF70/48CCE0 guest records use fixed source rows, not the character LimitParam byte.</summary>
    public static ushort GuestActionId(int characterId)
    {
        return characterId is >= 6 and <= 10 ? (ushort)(characterId - 6) : throw new InvalidOperationException("Character has no guest Limit source row.");
    }

    /// <summary>Native48D170 chooses the highest learned Angelo finisher at or below crisis minus one.</summary>
    public static ushort AngeloActionId(byte crisis, byte learnedFlags)
    {
        if (crisis is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(crisis));
        }
        for (int action = crisis - 1; action >= 0; action--)
        {
            if ((learnedFlags & (1 << action)) != 0)
            {
                return (ushort)action;
            }
        }
        throw new InvalidDataException("Native Combine has no learned Angelo finisher at or below its crisis level.");
    }
}
