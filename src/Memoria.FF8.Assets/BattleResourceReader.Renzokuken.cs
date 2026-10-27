using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattleRenzokukenModels(BattleModel Body, BattleModel? Weapon);

internal static partial class BattleResourceReader
{
    public static BattleRenzokukenModels ReadRenzokukenModels(BattleNativeRenzokukenResource resource, BattleModel body, BattleModel? weapon)
    {
        using BinaryReader reader = Reader(resource.RawResource.ToArray());
        int[] offsets = Directory(resource.RawResource.AsSpan());
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
        BattleModel bodyBank = RetainExternalBankGeometry(result.Body, body.EditableOverride);
        BattleModel changedBody = modelOverrides?.ApplyBattleBank("battle/" + body.Name.ToLowerInvariant(), $"renzokuken-{resource.MagicId}", bodyBank) ?? bodyBank;
        BattleModel? weaponBank = result.Weapon is { } decodedWeapon ? RetainExternalBankGeometry(decodedWeapon, weapon?.EditableOverride) : null;
        BattleModel? changedWeapon = weaponBank is null ? null : modelOverrides?.ApplyBattleBank("battle/" + weaponBank.Name.ToLowerInvariant(), $"renzokuken-{resource.MagicId}", weaponBank) ?? weaponBank;
        return new(changedBody, changedWeapon);
    }

    private static BattleModel RetainExternalBankGeometry(BattleModel decoded, EditableModel? geometry)
    {
        if (geometry is null)
        {
            return decoded;
        }
        EditableModel bank = ModelExchange.FromBattle(decoded);
        int[] sourceJoints = ModelExchange.NativeJointOrder(bank);
        int[] targetJoints = ModelExchange.NativeJointOrder(geometry);
        if (sourceJoints.Length != targetJoints.Length)
        {
            throw new NotSupportedException("The external animation bank requires the retained native joint set.");
        }
        Dictionary<int, int> joints = sourceJoints.Zip(targetJoints).ToDictionary(pair => pair.First, pair => pair.Second);
        EditableAnimation[] animations = bank.Animations.Select(animation => animation with
        {
            Channels = animation.Channels.Select(channel => channel with { Bone = joints[channel.Bone] }).ToArray()
        }).ToArray();
        return decoded with { EditableOverride = geometry with { Animations = animations } };
    }

}
