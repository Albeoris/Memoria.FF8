using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokukenVariantProfile(ushort Effect, uint Initializer, uint Entry, uint MainDescriptor, uint MainBank, uint RootCallback, uint ChildDescriptor, uint ChildBank, uint ControllerCallback, uint TrailCallback, uint SpiralCallback, uint ContactCallback, uint ImpactCallback, uint ParticleCallback, uint SharedEmitterCallback, uint SharedStreakCallback)
{
    public static NativeBattleRenzokukenVariantProfile Renzokuken141 { get; } = new(141, 0x61DE50, 0x61DE70, 0x24C1E40, 0x24C1E30, 0x61F710, 0x24C2388, 0x24C2398, 0x61DF70, 0x61E8B0, 0x61EDF0, 0x61EF20, 0x61F2B0, 0x61F760, 0x61E2F0, 0x61E580);
    public static NativeBattleRenzokukenVariantProfile Renzokuken259 { get; } = new(259, 0x5A6C00, 0x5A6C20, 0x2258520, 0x2258510, 0x5A84C0, 0x2258A80, 0x2258A90, 0x5A6D20, 0x5A7660, 0x5A7BA0, 0x5A7CD0, 0x5A8060, 0x5A8510, 0x5A70A0, 0x5A7330);
    public static NativeBattleRenzokukenVariantProfile Renzokuken332 { get; } = new(332, 0x58B910, 0x58B930, 0x21FD048, 0x21FD038, 0x58D470, 0x21FD5C8, 0x21FD5D8, 0x58BA30, 0x58C410, 0x58C950, 0x58CA80, 0x58CE50, 0x58D4C0, 0x58BE50, 0x58C0E0);
    public static NativeBattleRenzokukenVariantProfile Renzokuken333 { get; } = new(333, 0x589BB0, 0x589BD0, 0x21FAE70, 0x21FAE60, 0x58B6A0, 0x21FB3F0, 0x21FB400, 0x589CD0, 0x58A680, 0x58ABC0, 0x58ACF0, 0x58B0C0, 0x58B6F0, 0x58A0C0, 0x58A350);

    public ushort ChildCapacity => Effect == 332 ? (ushort)200 : (ushort)100;
    public int Contacts => Effect switch
    {
        141 => 5,
        259 => 6,
        332 => 7,
        333 => 8,
        _ => throw new NotSupportedException("Native Renzokuken profile has no original contact count.")
    };
    public int LateFrame => Effect switch
    {
        141 => 51,
        259 => 58,
        332 => 78,
        333 => 76,
        _ => throw new NotSupportedException("Native Renzokuken profile has no original late-effect trigger.")
    };
    public bool HasContactOffsets => Effect is 332 or 333;

    public static NativeBattleRenzokukenVariantProfile ForEffect(ushort effect) => effect switch
    {
        141 => Renzokuken141,
        259 => Renzokuken259,
        332 => Renzokuken332,
        333 => Renzokuken333,
        _ => throw new NotSupportedException($"Native Renzokuken variant {effect} has no proved profile.")
    };
}

public sealed record NativeBattleRenzokukenVariantState([property: JsonRequired] NativeBattleRenzokuken160State Core, [property: JsonRequired] NativeBattleVector LateOrigin, [property: JsonRequired] NativeBattleVector LateDirection, [property: JsonRequired] NativeBattleRenzokuken160Resources? RetainedResources)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActionMatrix? LateMatrix { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokukenContactPadding? ContactPadding { get; init; }
}

public sealed record NativeBattleRenzokukenContactPadding([property: JsonRequired] ushort Impacts, [property: JsonRequired] ulong Sparks, [property: JsonRequired] ulong Debris);
public readonly record struct NativeBattleRenzokukenContactOrigin(NativeBattleVector Position, ushort? Padding);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>? RenzokukenVariants { get; init; }

    public void ValidateRenzokukenVariants()
    {
        if (RenzokukenVariants is null)
        {
            return;
        }
        foreach ((ushort effect, NativeBattleRenzokukenVariantState state) in RenzokukenVariants)
        {
            if (effect is not (141 or 259 or 332 or 333))
            {
                throw new InvalidDataException("Native Renzokuken variant has an unsupported profile owner.");
            }
            NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.ForEffect(effect);
            if (state is null || state.Core is null || state.RetainedResources is not { } resources || resources.ActorResource is null || resources.ActorResource.MagicId != effect || resources.Animation is null || resources.Animation.Data.IsDefaultOrEmpty || resources.ParticleSprites.IsDefault || resources.ParticleSprites.Length != 3 || resources.SpiralColors.IsDefault || resources.SpiralColors.Length != 24 || resources.ContactLeadFrames.IsDefault || resources.ContactLeadFrames.Length != profile.Contacts || resources.Meshes.IsDefault || resources.Meshes.Length != (profile.HasContactOffsets ? 5 : 4) || resources.ImpactAxis.IsDefault || resources.ImpactAxis.Length != 3 || resources.StreakAgeOffsets.IsDefault || resources.StreakAgeOffsets.Length != 8 || resources.StreakPaletteOffsets.IsDefault || resources.StreakPaletteOffsets.Length != 8)
            {
                throw new InvalidDataException("Native Renzokuken variant lost its supported profile, retained local storage or actual immutable entry resources.");
            }
            state.Core.Validate();
            if (profile.HasContactOffsets)
            {
                if (resources.ContactOffsets is not { } offsets || offsets.IsDefault || offsets.Length != profile.Contacts || state.LateMatrix is null || state.ContactPadding is not { } padding || padding.Impacts >= 4096 || (padding.Sparks >> 40) != 0 || (padding.Debris >> 40) != 0)
                {
                    throw new InvalidDataException("Native Renzokuken variant lost its original contact offsets, captured matrix or indeterminate copied-padding owners.");
                }
                state.LateMatrix.Validate();
            }
        }
    }
}

public static partial class NativeBattleRenzokukenVariantController
{
    public static NativeBattleRenzokukenVariantState ReadState(NativeBattleRenzokuken160Frame frame)
    {
        return frame.Memory.RenzokukenVariants?.GetValueOrDefault(frame.Effect.Binding.Effect) ?? throw new NotSupportedException("Native Renzokuken variant requires its retained module owner.");
    }

    public static NativeBattleRenzokuken160Frame WriteState(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160State core)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        return frame with { Memory = frame.Memory with { RenzokukenVariants = frame.Memory.RenzokukenVariants!.SetItem(frame.Effect.Binding.Effect, state with { Core = core }) } };
    }

    public static NativeBattleRenzokuken160Frame MarkContactPaddingUnknown(NativeBattleRenzokuken160Frame frame, int bank, int index)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        NativeBattleRenzokukenContactPadding padding = state.ContactPadding ?? throw new NotSupportedException("Native contact offset requires its retained indeterminate-padding owner.");
        padding = bank switch
        {
            0 when index is >= 0 and < 12 => padding with { Impacts = (ushort)(padding.Impacts | (1 << index)) },
            1 when index is >= 0 and < 40 => padding with { Sparks = padding.Sparks | (1UL << index) },
            2 when index is >= 0 and < 40 => padding with { Debris = padding.Debris | (1UL << index) },
            _ => throw new InvalidDataException("Native contact padding write exceeds its original physical bank.")
        };
        return Store(frame, state with { ContactPadding = padding });
    }

    private static NativeBattleRenzokuken160Frame Store(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokukenVariantState state)
    {
        return frame with { Memory = frame.Memory with { RenzokukenVariants = frame.Memory.RenzokukenVariants!.SetItem(frame.Effect.Binding.Effect, state) } };
    }
}
