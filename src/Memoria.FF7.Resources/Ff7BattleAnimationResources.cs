using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7BattleAnimationResources(LgpArchive archive, Ff7BattlePlacementTables placement, LgpArchive magicArchive, IReadOnlyList<short[]> jumpDeltas, IReadOnlyList<Ff7BattleHitmarkDefinition> hitmarks, Ff7BattleActorModelResources? actorResources = null)
{
    private readonly Dictionary<string, (BattleActionBank Bank, BattleModelData Model)> models = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Prefix, int Action, ushort? Limit), BattleActionSequence> actions = [];
    private readonly Dictionary<(string Prefix, int Clip), IReadOnlyList<BattleClipFrame>> clips = [];
    private readonly Dictionary<(string Prefix, int Clip), IReadOnlyList<BattleClipFrame>> weaponClips = [];
    private readonly Dictionary<ushort, BattleLimitAnimation> limits = [];
    private readonly Dictionary<(string Prefix, int Clip, ushort Limit), IReadOnlyList<BattleClipFrame>> limitWeaponClips = [];
    private readonly Dictionary<(string Prefix, int Clip, ushort Limit), IReadOnlyList<BattleClipFrame>> limitBodyClips = [];

    public Ff7BattlePlacementTables Placement { get; } = placement;
    public IReadOnlyList<Ff7BattleHitmarkDefinition> Hitmarks { get; } = hitmarks;
    public Ff7BattleActorModelResources? ActorResources { get; } = actorResources;

    public short[] JumpDeltas(int selector)
    {
        if ((uint)selector >= jumpDeltas.Count)
        {
            throw new InvalidDataException($"FFVII jump selector {selector} requires its original vertical movement table.");
        }
        return (short[])jumpDeltas[selector].Clone();
    }

    public BattleActionSequence Get(string prefix, int action, ushort? limit = null)
    {
        if (!actions.TryGetValue((prefix, action, limit), out BattleActionSequence? sequence))
        {
            var model = GetModel(prefix);
            BattleModelData data = limit is ushort attack ? GetLimit(attack).ApplyTo(model.Model) : model.Model;
            sequence = BattleActionSequence.Read(model.Bank, data, action);
            actions.Add((prefix, action, limit), sequence);
        }
        return sequence;
    }

    public ushort CollisionRadius(string prefix) => GetModel(prefix).Bank.CollisionRadius;
    public int BoneCount(string prefix) => GetModel(prefix).Model.Bones.Count + 1;
    public byte JointSelector(string prefix, int selector) => GetModel(prefix).Bank.JointSelectors.Span[selector];
    public int ClipFrameCount(string prefix, int clip) => checked((int)GetModel(prefix).Model.BodyClips[clip].FrameCount);

    public Vector3 BonePosition(string prefix, int clip, int frame, Ff7BattlePlacement placement, int bone, BattleActorPoseTransform? extra = null)
    {
        return BaseBonePoint(prefix, clip, frame, placement, bone, Vector3.Zero, extra);
    }

    public Vector3 BaseBonePoint(string prefix, int clip, int frame, Ff7BattlePlacement placement, int bone, Vector3 localPoint, BattleActorPoseTransform? extra = null)
    {
        BattleModelData model = GetModel(prefix).Model;
        if (!clips.TryGetValue((prefix, clip), out IReadOnlyList<BattleClipFrame>? frames))
        {
            frames = BattleClipDecoder.Decode(model.BodyClips[clip]);
            clips.Add((prefix, clip), frames);
        }
        return BattleRigidPose.BonePoint(model, frames[frame % frames.Count], placement.Position, placement.Yaw * MathF.Tau / 4096, bone, localPoint, extra);
    }

    public Vector3 BaseWeaponPosition(string prefix, int clip, int frame, Ff7BattlePlacement placement, Vector3 localPoint, BattleActorPoseTransform? extra = null)
    {
        if (!weaponClips.TryGetValue((prefix, clip), out IReadOnlyList<BattleClipFrame>? frames))
        {
            frames = BattleClipDecoder.Decode(GetModel(prefix).Model.WeaponClips[clip]);
            weaponClips.Add((prefix, clip), frames);
        }
        return BattleRigidPose.WeaponPosition(frames[frame % frames.Count], placement.Position, placement.Yaw * MathF.Tau / 4096, localPoint, extra);
    }

    public BattleLimitAnimation GetLimit(ushort attack)
    {
        if (!limits.TryGetValue(attack, out BattleLimitAnimation? animation))
        {
            animation = Ff7BattleLimitAnimationResources.Load(magicArchive, attack);
            limits.Add(attack, animation);
        }
        return animation;
    }

    public Vector3 LimitWeaponPosition(string prefix, int clip, int frame, Ff7BattlePlacement placement, ushort limit, Vector3 localPoint, BattleActorPoseTransform? extra = null)
    {
        if (!limitWeaponClips.TryGetValue((prefix, clip, limit), out IReadOnlyList<BattleClipFrame>? frames))
        {
            BattlePackedClip packed = clip >= BattleLimitAnimation.FirstClip ? GetLimit(limit).Weapon[clip - BattleLimitAnimation.FirstClip] : GetModel(prefix).Model.WeaponClips[clip];
            frames = BattleClipDecoder.Decode(packed);
            limitWeaponClips.Add((prefix, clip, limit), frames);
        }
        return BattleRigidPose.WeaponPosition(frames[frame % frames.Count], placement.Position, placement.Yaw * MathF.Tau / 4096, localPoint, extra);
    }

    public Vector3 LimitBonePosition(string prefix, int clip, int frame, Ff7BattlePlacement placement, ushort limit, int bone, Vector3 localPoint, BattleActorPoseTransform? extra = null)
    {
        BattleModelData model = GetModel(prefix).Model;
        if (!limitBodyClips.TryGetValue((prefix, clip, limit), out IReadOnlyList<BattleClipFrame>? frames))
        {
            BattlePackedClip packed = clip >= BattleLimitAnimation.FirstClip ? GetLimit(limit).Body[clip - BattleLimitAnimation.FirstClip] : model.BodyClips[clip];
            frames = BattleClipDecoder.Decode(packed);
            limitBodyClips.Add((prefix, clip, limit), frames);
        }
        return BattleRigidPose.BonePoint(model, frames[frame % frames.Count], placement.Position, placement.Yaw * MathF.Tau / 4096, bone, localPoint, extra);
    }

    private (BattleActionBank Bank, BattleModelData Model) GetModel(string prefix)
    {
        if (!models.TryGetValue(prefix, out var model))
        {
            model = (BattleActionBank.Read(Read(prefix + "ab")), BattleModelData.Read(Read(prefix + "aa"), Read(prefix + "da")));
            models.Add(prefix, model);
        }
        return model;
    }

    public static string ModelPrefix(int identity, bool enemy)
    {
        if (enemy)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)identity, 675u);
            return new string([(char)('a' + identity / 26), (char)('a' + identity % 26)]);
        }
        return identity switch
        {
            0 => "rt",
            1 => "sb",
            2 => "ru",
            _ => throw new NotSupportedException($"FFVII party battle model {identity} is unsupported.")
        };
    }

    private byte[] Read(string name)
    {
        return archive.ReadEntry(archive.Find(name).Single());
    }
}
