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
        BattleModel changedBody = body with { Animations = bodyAnimations, Sequences = new(resource.GraphicsResource.SequenceSection.ToArray()) };
        //5022C0 passes mask4 to502270 for the linked weapon: replace only animation pointer+8, retain its original sequence pointer+12.
        BattleModel? changedWeapon = weapon is null ? null : weapon with { Animations = Animations(reader, offsets[2], weapon.Bones.Count) };
        return new(changedBody, changedWeapon);
    }
}

public sealed partial class BattleAssets
{
    public BattleRenzokukenModels ReadNativeRenzokukenModels(BattleNativeRenzokukenResource resource, BattleModel body, BattleModel? weapon)
    {
        return BattleResourceReader.ReadRenzokukenModels(resource, body, weapon);
    }
}
