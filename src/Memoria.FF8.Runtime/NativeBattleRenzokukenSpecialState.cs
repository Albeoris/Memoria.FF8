using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokukenSpecialProfile(ushort Effect, uint Entry, uint MainDescriptor, uint MainBank, uint RootCallback, uint ChildDescriptor, uint ChildBank, uint ControllerCallback)
{
    public uint Initializer => Entry - 0x20;
    public int PacketStride { get; init; } = 0x8000;
}

public static partial class NativeBattleRenzokukenSpecialProfiles
{
    public static NativeBattleRenzokukenSpecialProfile Renzokuken161 { get; } = new(161, 0x5FD100, 0x23D1B18, 0x23D1B08, 0x5FEC10, 0x23D25D8, 0x23D25E8, 0x5FD230);
}

public sealed record NativeBattleRenzokukenSpecialState([property: JsonRequired] NativeBattleRenzokuken160State Core, [property: JsonRequired] NativeBattleRenzokuken160Resources Resources)
{
    [JsonRequired]
    public NativeBattleVector PreviousOrigin { get; init; }
    [JsonRequired]
    public NativeBattleVector PreviousDirection { get; init; }
    [JsonRequired]
    public ushort PreviousOriginPadding { get; init; }
    [JsonRequired]
    public uint PreviousDirectionPadding { get; init; }
    [JsonRequired]
    public NativeBattleRenzokukenContactPadding ContactPadding { get; init; } = new(0, 0, 0);
    [JsonRequired]
    public ImmutableArray<NativeBattleVector> ContactOffsets { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActionMatrix? CapturedMatrix { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> TargetParticles { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<byte> BurstParticles { get; init; } = [];
    [JsonRequired]
    public ulong ExtraStorageUnknownPaddingLow { get; init; }
    [JsonRequired]
    public ulong ExtraStorageUnknownPaddingHigh { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> ExtraStorage { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<NativeBattleSearchSpriteResource> ExtraSprites { get; init; } = [];

    public void Validate(ushort effect)
    {
        if ((ExtraStorageUnknownPaddingHigh >> 36) != 0 || ContactPadding is null || Core is null || Resources is null || Resources.ActorResource is null || Resources.Animation is null || Resources.Animation.Data.IsDefaultOrEmpty || Resources.ParticleSprites.IsDefault || Resources.ParticleSprites.Length != 3 || Resources.SpiralColors.IsDefault || Resources.SpiralColors.Length != 24 || Resources.ContactLeadFrames.IsDefaultOrEmpty || Resources.Meshes.IsDefault || Resources.Meshes.Length < 3 || Resources.ImpactAxis.IsDefault || Resources.ImpactAxis.Length != 3 || Resources.StreakAgeOffsets.IsDefault || Resources.StreakAgeOffsets.Length != 8 || Resources.StreakPaletteOffsets.IsDefault || Resources.StreakPaletteOffsets.Length != 8 || ExtraSprites.IsDefault)
        {
            throw new InvalidDataException("Native special Renzokuken lost its actual immutable entry resources.");
        }
        Core.Validate();
        if (Resources.ActorResource.MagicId != effect || Resources.ActorResource.TargetResources.IsDefault || Resources.ActorResource.TargetResources.Length != (effect == 339 ? 2 : 1) || TargetParticles.IsDefault || BurstParticles.IsDefault || ExtraStorage.IsDefault || (effect == 161 && (TargetParticles.Length != 800 || BurstParticles.Length != 600 || ExtraSprites.Length != 2)) || (effect != 161 && ExtraStorage.Length != 3200) || ContactPadding.Impacts >= 4096 || (ContactPadding.Sparks >> 40) != 0 || (ContactPadding.Debris >> 40) != 0)
        {
            throw new InvalidDataException("Native special Renzokuken lost its retained actor, target or physical local banks.");
        }
        if (effect != 161)
        {
            int meshes = effect switch
            {
                334 or 336 or 337 => 6,
                335 or 341 or 342 or 343 => 4,
                339 => 5,
                340 => 7,
                _ => throw new InvalidDataException("Native special Renzokuken has an unsupported resource owner.")
            };
            int sprites = effect switch
            {
                334 or 335 or 336 or 337 => 2,
                341 => 0,
                _ => 1
            };
            int alignment = effect is 334 or 335 or 337 or 339 or 340 or 341 ? 3 : 0;
            ImmutableArray<NativeBattleVector> offsets = Resources.ContactOffsets ?? ContactOffsets;
            if (Resources.Meshes.Length != meshes || Resources.SpecialSprites.IsDefault || Resources.SpecialSprites.Length != sprites || Resources.SpecialAlignmentAxis.IsDefault || Resources.SpecialAlignmentAxis.Length != alignment || offsets.IsDefault || offsets.Length != Resources.ContactLeadFrames.Length)
            {
                throw new InvalidDataException("Native special Renzokuken lost its family-specific meshes, sprites, alignment or contact offsets.");
            }
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>? RenzokukenSpecials { get; init; }

    public void ValidateRenzokukenSpecials()
    {
        if (RenzokukenSpecials is null)
        {
            return;
        }
        foreach ((ushort effect, NativeBattleRenzokukenSpecialState state) in RenzokukenSpecials)
        {
            if (effect is not (161 or 334 or 335 or 336 or 337 or 339 or 340 or 341 or 342 or 343))
            {
                throw new InvalidDataException("Native special Renzokuken snapshot has an unsupported owner.");
            }
            state.Validate(effect);
        }
    }
}

public sealed record NativeBattleRenzokukenSpecialTargetResource(BattleNativeRenzokukenResource Resource, byte Target, byte Bank, byte Sequence) : NativeBattleRenzokuken160Operation
{
    public bool StartSequence { get; init; } = true;
}
public sealed record NativeBattleRenzokukenSpecialTargetSequence(byte Target, byte Sequence) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokukenSpecialCameraRotation(short Delta) : NativeBattleRenzokuken160Operation;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160Frame MarkContactPaddingUnknown(NativeBattleRenzokuken160Frame frame, int bank, int index)
    {
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        NativeBattleRenzokukenContactPadding padding = state.ContactPadding;
        padding = bank switch
        {
            0 when index is >= 0 and < 12 => padding with { Impacts = (ushort)(padding.Impacts | (1 << index)) },
            1 when index is >= 0 and < 40 => padding with { Sparks = padding.Sparks | (1UL << index) },
            2 when index is >= 0 and < 40 => padding with { Debris = padding.Debris | (1UL << index) },
            _ => throw new InvalidDataException("Native special Renzokuken padding write exceeds its physical bank.")
        };
        return Store(frame, state with { ContactPadding = padding });
    }

    public static NativeBattleRenzokukenSpecialState ReadState(NativeBattleRenzokuken160Frame frame) => frame.Memory.RenzokukenSpecials?.GetValueOrDefault(frame.Effect.Binding.Effect) ?? throw new NotSupportedException("Native special Renzokuken requires its retained local owner.");

    public static NativeBattleRenzokuken160Frame Store(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokukenSpecialState state) => frame with { Memory = frame.Memory with { RenzokukenSpecials = frame.Memory.RenzokukenSpecials!.SetItem(frame.Effect.Binding.Effect, state) } };

    public static NativeBattleRenzokuken160Frame WriteState(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160State core) => Store(frame, ReadState(frame) with { Core = core });
}
