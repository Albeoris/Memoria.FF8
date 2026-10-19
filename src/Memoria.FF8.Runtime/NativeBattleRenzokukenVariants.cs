using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokukenVariantProfile(ushort Effect, uint Initializer, uint Entry, uint MainDescriptor, uint MainBank, uint RootCallback, uint ChildDescriptor, uint ChildBank, uint ControllerCallback, uint TrailCallback, uint SpiralCallback, uint ContactCallback, uint ImpactCallback, uint ParticleCallback, uint SharedEmitterCallback, uint SharedStreakCallback)
{
    public static NativeBattleRenzokukenVariantProfile Renzokuken141 { get; } = new(141, 0x61DE50, 0x61DE70, 0x24C1E40, 0x24C1E30, 0x61F710, 0x24C2388, 0x24C2398, 0x61DF70, 0x61E8B0, 0x61EDF0, 0x61EF20, 0x61F2B0, 0x61F760, 0x61E2F0, 0x61E580);
}

public sealed record NativeBattleRenzokukenVariantState([property: JsonRequired] NativeBattleRenzokuken160State Core, [property: JsonRequired] NativeBattleVector LateOrigin, [property: JsonRequired] NativeBattleVector LateDirection, [property: JsonRequired] NativeBattleRenzokuken160Resources? RetainedResources);

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
            if (effect != 141 || state is null || state.Core is null || state.RetainedResources is not { } resources || resources.ActorResource is not { MagicId: 141 } || resources.Animation is null || resources.Animation.Data.IsDefaultOrEmpty || resources.ParticleSprites.IsDefault || resources.ParticleSprites.Length != 3 || resources.SpiralColors.IsDefault || resources.SpiralColors.Length != 24 || resources.ContactLeadFrames.IsDefault || resources.ContactLeadFrames.Length != 5 || resources.Meshes.IsDefault || resources.Meshes.Length != 4 || resources.ImpactAxis.IsDefault || resources.ImpactAxis.Length != 3 || resources.StreakAgeOffsets.IsDefault || resources.StreakAgeOffsets.Length != 8 || resources.StreakPaletteOffsets.IsDefault || resources.StreakPaletteOffsets.Length != 8)
            {
                throw new InvalidDataException("Native Renzokuken variant lost its supported profile, retained local storage or actual immutable entry resources.");
            }
            state.Core.Validate();
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

    private static NativeBattleRenzokuken160Frame Store(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokukenVariantState state)
    {
        return frame with { Memory = frame.Memory with { RenzokukenVariants = frame.Memory.RenzokukenVariants!.SetItem(frame.Effect.Binding.Effect, state) } };
    }
}
