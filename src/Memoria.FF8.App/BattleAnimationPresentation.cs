using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal readonly record struct BattleSequenceSoundCue(string ActorId, long ActionFrame, int CueIndex, int SoundId, bool ActorSound, int Attribute);
internal readonly record struct BattleAnimationSample(int Animation, float Frame, Vector3 Offset, float Opacity = 1, bool Loop = false);

/// <summary>Projects installed skeletal action clips onto the existing deterministic combat scaffold.</summary>
internal sealed class BattleAnimationPresentation(BattleModel model, BattleSequenceSet? sequences, BattleCombatantDefinition definition)
{
    private readonly Dictionary<BattleEvent, double> firstFrames = [];
    private double frames;
    private double? firstSessionFrame;

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        frames += elapsed.TotalSeconds * BattleSession.FramesPerSecond;
    }

    public BattleAnimationSample Sample(BattleSession session, BattleCombatant actor, Vector3 origin, Vector3 target)
    {
        firstSessionFrame ??= session.Frame - frames;
        if (sequences is null)
        {
            return new BattleAnimationSample(0, 0, Vector3.Zero, actor.IsAlive ? 1 : 0.25f);
        }
        BattleEvent? defeated = session.Events.LastOrDefault(item => item.Kind == "Defeated" && item.TargetId == actor.Id);
        if (!actor.IsAlive)
        {
            int[] clips = Sequence(3).Animations.ToArray();
            // TODO(TD-009): Native party KO includes an already-KO branch before the fall/settled suffix. The scaffold enters from standing; full status-branch execution remains deferred
            if (actor.IsPlayer && clips.Length > 1 && clips[0] == clips[^1])
            {
                clips = clips[1..];
            }
            double age = defeated is null ? double.MaxValue : Age(session, defeated);
            if (clips.Length != 0)
            {
                return Play(clips, age, Vector3.Zero);
            }
            // TODO(TD-009): Native effect-only deaths require the battle effect VM; fade is presentation tuning
            return new BattleAnimationSample(0, 0, Vector3.Zero, (float)Math.Clamp(1 - age / 15, 0, 1));
        }
        BattleEvent? windup = session.Events.LastOrDefault(item => item.Kind == "Windup" && item.ActorId == actor.Id);
        if (windup is not null)
        {
            double age = Age(session, windup);
            const int duration = BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames;
            if (age < duration)
            {
                int[] clips = Sequence(AttackSequence).Animations.ToArray();
                if (clips.Length == 0)
                {
                    throw new NotSupportedException($"{model.Name} physical action sequence has no skeletal pose track.");
                }
                // TODO(TD-009): Native clips use scaffold phase duration/approach until the full sequence movement/effect VM executes
                double progress = age / duration;
                float approach = age < BattleSession.AttackWindupFrames ? (float)(age / BattleSession.AttackWindupFrames) : (float)((duration - age) / BattleSession.AttackRecoveryFrames);
                Vector3 offset = definition.IsPlayer && Melee ? (target - origin) * Math.Clamp(approach, 0, 1) * 0.7f : Vector3.Zero;
                return Play(clips, progress * Length(clips), offset, definition.IsPlayer && Melee);
            }
        }
        BattleEvent? damage = session.Events.LastOrDefault(item => item.Kind == "Damage" && item.Amount > 0 && item.TargetId == actor.Id);
        if (damage is not null)
        {
            int[] clips = Sequence(4).Animations.ToArray();
            double age = Age(session, damage);
            if (clips.Length != 0 && age < Length(clips))
            {
                return Play(clips, age, Vector3.Zero);
            }
        }
        if (actor.IsPlayer && session.Outcome == BattleOutcome.Victory)
        {
            BattleEvent? outcome = session.Events.LastOrDefault(item => item.Kind == "Outcome");
            int[] clips = Sequence(17).Animations.ToArray();
            if (clips.Length != 0)
            {
                return Play(clips, outcome is null ? 0 : Age(session, outcome), Vector3.Zero);
            }
        }
        return new BattleAnimationSample(0, (float)Math.Max(session.Frame, firstSessionFrame.Value + frames) / 2, Vector3.Zero, Loop: true);
    }

    public bool Melee { get; init; }
    public int AttackSequence { get; init; } = definition.IsPlayer ? 13 : 0;

    public float GroundOffset(BattleAnimationSample sample, bool alive, float standingOffset)
    {
        // TODO(TD-009): Ground fallen geometry at the selected pose instead of subtracting the standing-foot anchor from a flat KO pose
        return alive ? standingOffset : model.Pose(sample.Animation, sample.Frame, sample.Loop).SelectMany(mesh => mesh).Min(vertex => vertex.Y);
    }

    public IReadOnlyList<BattleSequenceSoundCue> CollectSounds(BattleSession session)
    {
        if (sequences is null)
        {
            return [];
        }
        List<BattleSequenceSoundCue> result = [];
        foreach (BattleEvent item in session.Events)
        {
            int sequence = item.Kind switch
            {
                "Windup" when item.ActorId == definition.Id => AttackSequence,
                "Defeated" when item.TargetId == definition.Id => 3,
                "Damage" when item.TargetId == definition.Id && item.Amount > 0 => 4,
                "Outcome" when definition.IsPlayer && session.Outcome == BattleOutcome.Victory => 17,
                _ => 0
            };
            if (sequence == 0)
            {
                continue;
            }
            BattleSequence track = Sequence(sequence);
            double age = Age(session, item);
            for (int index = 0; index < track.Sounds.Count; index++)
            {
                BattleSequenceSound sound = track.Sounds[index];
                double due = Length(track.Animations.Take(sound.AnimationIndex));
                if (sequence == AttackSequence)
                {
                    due = due / Math.Max(1, Length(track.Animations)) * (BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames);
                }
                if (age < due || sound.ActorSound && sound.SoundId == (definition.IsPlayer && definition.ModelId == 0 ? 1 : 0) && sequence == AttackSequence)
                {
                    continue;
                }
                result.Add(new BattleSequenceSoundCue(definition.Id, item.Frame, sequence * 1024 + index, sound.SoundId, sound.ActorSound, sound.Attribute));
            }
        }
        return result;
    }

    private BattleSequence Sequence(int sequence)
    {
        BattleSequence result = sequences!.Read(sequence);
        foreach (int animation in result.Animations)
        {
            if ((uint)animation >= model.Animations.Count)
            {
                throw new NotSupportedException($"{model.Name} sequence {sequence} references absent animation {animation}.");
            }
        }
        return result;
    }

    private double Age(BattleSession session, BattleEvent item)
    {
        if (!firstFrames.TryGetValue(item, out double first))
        {
            first = frames - (session.Frame - item.Frame);
            firstFrames.Add(item, first);
        }
        return Math.Max(session.Frame - item.Frame, frames - first);
    }

    private int Length(IEnumerable<int> clips) => clips.Sum(animation => model.Animations[animation].Frames.Count);

    private BattleAnimationSample Play(IReadOnlyList<int> clips, double elapsed, Vector3 offset, bool stabilizeHorizontalRoot = false)
    {
        for (int index = 0; index < clips.Count; index++)
        {
            int animation = clips[index];
            int duration = model.Animations[animation].Frames.Count;
            if (elapsed < duration || index == clips.Count - 1)
            {
                float frame = (float)Math.Min(elapsed, duration - 1);
                if (stabilizeHorizontalRoot)
                {
                    int current = (int)MathF.Floor(frame);
                    int next = Math.Min(current + 1, duration - 1);
                    Vector3 root = Vector3.Lerp(model.Animations[animation].Frames[current].Translation, model.Animations[animation].Frames[next].Translation, frame - current);
                    Vector3 delta = root - model.Animations[0].Frames[0].Translation;
                    Vector3 local = new Vector3(-delta.X, 0, delta.Z) * model.Scale;
                    // TODO(TD-009): The scaffold owns approach translation; remove the clip's horizontal root displacement once, preserving its vertical motion
                    offset -= Vector3.TransformNormal(local, Matrix4x4.CreateRotationY(definition.IsPlayer ? MathF.PI : 0));
                }
                return new BattleAnimationSample(animation, frame, offset);
            }
            elapsed -= duration;
        }
        throw new NotSupportedException($"{model.Name} has no skeletal pose to present.");
    }
}
