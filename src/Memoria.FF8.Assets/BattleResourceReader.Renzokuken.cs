using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattleRenzokukenModels(BattleModel Body, BattleModel? Weapon);

internal static partial class BattleResourceReader
{
    public static BattleRenzokukenModels ReadRenzokukenModels(BattleNativeRenzokukenResource resource, BattleModel body, BattleModel? weapon)
    {
        using BinaryReader reader = Reader(resource.RawResource.ToArray());
        int[] offsets = Directory(reader);
        if (offsets.Length != 3)
        {
            throw new InvalidDataException("Native Renzokuken pointer replacement requires its three-section embedded resource.");
        }
        IReadOnlyList<BattleAnimation> bodyAnimations = Animations(reader, offsets[0], body.Bones.Count);
        if (!bodyAnimations.Select(animation => checked((byte)animation.Frames.Count)).SequenceEqual(resource.AnimationFrameCounts))
        {
            throw new InvalidDataException("Decoded Renzokuken poses disagree with their original timing frame counts.");
        }
        NativeBattlePoseSource? bodySource = body.NativePoseSource is { } originalBody ? originalBody with { Animations = resource.RawResource.AsSpan().Slice(offsets[0], offsets[1] - offsets[0]).ToArray().ToImmutableArray() } : null;
        BattleModel changedBody = body with { Animations = bodyAnimations, Sequences = new(resource.GraphicsResource.SequenceSection.ToArray()), NativePoseSource = bodySource };
        //5022C0 passes mask4 to502270 for the linked weapon: replace only animation pointer+8, retain its original sequence pointer+12.
        NativeBattlePoseSource? weaponSource = weapon?.NativePoseSource is { } originalWeapon ? originalWeapon with { Animations = resource.RawResource.AsSpan().Slice(offsets[2]).ToArray().ToImmutableArray() } : null;
        BattleModel? changedWeapon = weapon is null ? null : weapon with { Animations = Animations(reader, offsets[2], weapon.Bones.Count), NativePoseSource = weaponSource };
        return new(changedBody, changedWeapon);
    }
}

public sealed partial class BattleAssets
{
    public BattleRenzokukenModels ReadNativeRenzokukenModels(BattleNativeRenzokukenResource resource, BattleModel body, BattleModel? weapon)
    {
        BattleRenzokukenModels result = BattleResourceReader.ReadRenzokukenModels(resource, body with { EditableOverride = null }, weapon is null ? null : weapon with { EditableOverride = null });
        BattleModel changedBody = modelOverrides?.ApplyBattleBank("battle/" + body.Name.ToLowerInvariant(), $"renzokuken-{resource.MagicId}", result.Body) ?? result.Body;
        BattleModel? changedWeapon = result.Weapon is { } decodedWeapon ? modelOverrides?.ApplyBattleBank("battle/" + decodedWeapon.Name.ToLowerInvariant(), $"renzokuken-{resource.MagicId}", decodedWeapon) ?? decodedWeapon : null;
        return new(changedBody, changedWeapon);
    }
}
