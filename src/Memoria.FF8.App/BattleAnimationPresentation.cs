using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal readonly record struct BattleSequenceSoundCue(string ActorId, long ActionFrame, int CueIndex, int SoundId, bool ActorSound, int Attribute);
internal readonly record struct BattleAnimationSample(int Animation, float Frame, Vector3 Offset, float Opacity = 1, bool Loop = false)
{
    public NativeBattleDecodedPose? NativePose { get; init; }
}

/// <summary>Projects installed skeletal action clips onto the existing deterministic combat scaffold.</summary>
internal sealed class BattleAnimationPresentation(BattleModel model, BattleSequenceSet? sequences, BattleCombatantDefinition definition)
{
    private readonly Dictionary<BattleEvent, double> firstFrames = [];
    private double frames;
    private double? idleOriginFrame;

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        frames += elapsed.TotalSeconds * BattleSession.FramesPerSecond;
    }

    public BattleAnimationSample Sample(BattleSession session, BattleCombatant actor, Vector3 origin, Vector3 target)
    {
        if (session.NativeGraphics is { } graphics)
        {
            int index = graphics.ActorIds.IndexOf(actor.Id);
            return index < 0 ? new BattleAnimationSample(0, 0, Vector3.Zero, 0) : SampleNativeModel(graphics.Models[index]);
        }
        idleOriginFrame ??= session.Frame - frames;
        if (sequences is null)
        {
            return new BattleAnimationSample(0, 0, Vector3.Zero, actor.IsAlive ? 1 : 0.25f);
        }
        BattleEvent? defeated = session.Events.LastOrDefault(item => item.Kind == "Defeated" && item.TargetId == actor.Id);
        if (!actor.IsAlive)
        {
            int[] clips = Sequence(3).Animations.ToArray();
            // TODO(TD-009): Native party KO includes an already-KO branch before the fall/settled suffix. The scaffold enters from standing; full status-branch execution remains deferred.
            if (actor.IsPlayer && clips.Length > 1 && clips[0] == clips[^1])
            {
                clips = clips[1..];
            }
            double age = defeated is null ? double.MaxValue : Age(session, defeated);
            if (clips.Length != 0)
            {
                return Play(clips, age, Vector3.Zero);
            }
            // TODO(TD-009): Native effect-only deaths require the battle effect VM; fade is presentation tuning.
            return new BattleAnimationSample(0, 0, Vector3.Zero, (float)Math.Clamp(1 - age / 15, 0, 1));
        }
        if (actor.IsPlayer && session.Outcome == BattleOutcome.Victory)
        {
            // Native502F90 selects17 through505C00/505BC0, replacing the previous sequence and resetting its position.
            BattleEvent? outcome = session.Events.LastOrDefault(item => item.Kind == "Outcome");
            int[] clips = Sequence(17).Animations.ToArray();
            if (clips.Length != 0)
            {
                return Play(clips, outcome is null ? 0 : Age(session, outcome), Vector3.Zero);
            }
        }
        BattleEvent? windup = session.Events.LastOrDefault(item => item.Kind == "Windup" && item.ActorId == actor.Id);
        if (windup is not null)
        {
            double age = Age(session, windup);
            const int duration = BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames;
            if (age < duration)
            {
                int[] clips = Sequence(CommandSequence(windup.Ability, windup.NativeSequence)).Animations.ToArray();
                if (clips.Length == 0)
                {
                    throw new NotSupportedException($"{model.Name} physical action sequence has no skeletal pose track.");
                }
                // TODO(TD-009): Native clips use scaffold phase duration/approach until the full sequence movement/effect VM executes.
                double progress = age / duration;
                float approach = age < BattleSession.AttackWindupFrames ? (float)(age / BattleSession.AttackWindupFrames) : (float)((duration - age) / BattleSession.AttackRecoveryFrames);
                bool approachTarget = definition.IsPlayer && Melee && windup.Ability == BattleAbility.Attack;
                Vector3 offset = approachTarget ? (target - origin) * Math.Clamp(approach, 0, 1) * 0.7f : Vector3.Zero;
                return Play(clips, progress * Length(clips), offset, approachTarget);
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
        // Native4842B0/4A9450 can hold gameplay while502AB0 continues the actor sequence/pose callback.
        // AdvanceTurn may dispatch hundreds of gameplay frames at once; idle time must not wait for wall time to catch up.
        return new BattleAnimationSample(0, (float)(idleOriginFrame.Value + frames) / 2, Vector3.Zero, Loop: true);
    }

    public bool Melee { get; init; }
    public int AttackSequence { get; init; } = definition.IsPlayer ? 13 : 0;

    internal BattleAnimationSample SampleNativeModel(NativeBattleGraphicsModel graphics)
    {
        //502AB0 draws only loaded, visible models; the pose is not initialized before the first509440 callback.
        if ((graphics.Flags0 & 6) != 2 || graphics.Flags2 is not ushort flags2 || (flags2 & 0x84) != 0 || graphics.SequenceState is not { Pose: byte pose, PoseFrame114: byte counter } state)
        {
            return new(0, 0, Vector3.Zero, 0);
        }
        return SampleNativePose(model, pose, counter, state.PoseFlags109);
    }

    internal static BattleAnimationSample SampleNativeLinkedModel(NativeBattleGraphicsModel graphics, BattleModel linked)
    {
        if ((graphics.Flags0 & 0x200) != 0)
        {
            return new(0, 0, Vector3.Zero, 0);
        }
        NativeBattlePoseProgress progress = graphics.SequenceState?.LinkedPose ?? throw new NotSupportedException("Native linked weapon rendering requires its independently decoded pose owner.");
        return SampleNativePose(linked, progress.Pose, progress.Frame, progress.Flags);
    }

    private static BattleAnimationSample SampleNativePose(BattleModel model, byte pose, byte counter, byte flags)
    {
        if (pose >= model.Animations.Count || counter == 0)
        {
            throw new InvalidDataException($"{model.Name} native pose has no decoded animation frame.");
        }
        //509440 decodes the first frame immediately.508F90 advances twice for fast and half-deltas for slow poses.
        float frame = (counter - 1) / ((flags & 1) != 0 ? 2f : 1f);
        if (frame >= model.Animations[pose].Frames.Count)
        {
            throw new InvalidDataException($"{model.Name} native pose counter exceeds its original body animation.");
        }
        NativeBattlePoseSource source = model.NativePoseSource ?? throw new NotSupportedException($"{model.Name} raw pose rendering requires its original compressed source.");
        return new(pose, frame, Vector3.Zero) { NativePose = source.Sample(pose, counter, flags) };
    }

    internal BattleAnimationSample SampleNativeSequence(int sequence, double poseFrames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(poseFrames);
        int[] clips = Sequence(sequence).Animations.ToArray();
        if (clips.Length == 0)
        {
            throw new NotSupportedException($"{model.Name} native sequence{sequence} has no skeletal pose track.");
        }
        //TODO(TD-009): Use the shared process clock and original embedded clips until raw sequence pose receipts replace static clip projection.
        return Play(clips, poseFrames, Vector3.Zero);
    }

    public float GroundOffset(BattleAnimationSample sample, bool alive, float standingOffset)
    {
        // TODO(TD-009): Ground fallen geometry at the selected pose instead of subtracting the standing-foot anchor from a flat KO pose.
        return alive ? standingOffset : (sample.NativePose is { } pose ? model.PoseNative(pose) : model.Pose(sample.Animation, sample.Frame, sample.Loop)).SelectMany(mesh => mesh).Min(vertex => vertex.Y);
    }

    public IReadOnlyList<BattleSequenceSoundCue> CollectSounds(BattleSession session)
    {
        if (session.NativeGraphics is not null)
        {
            //An event index is stable across restore even when gameplayFrame is frozen for many graphics callbacks.
            return session.Events.Select((item, index) => (item, index)).Where(entry => entry.item.ActorId == definition.Id && entry.item.NativeSound is not null).Select(entry => new BattleSequenceSoundCue(definition.Id, entry.item.Frame, entry.index, entry.item.NativeSound!.SoundId, entry.item.NativeSound.ActorSound, entry.item.NativeSound.Volume)).ToArray();
        }
        if (sequences is null)
        {
            return [];
        }
        List<BattleSequenceSoundCue> result = [];
        foreach (BattleEvent item in session.Events)
        {
            int sequence = item.Kind switch
            {
                "Windup" when item.ActorId == definition.Id => CommandSequence(item.Ability, item.NativeSequence),
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

    private int CommandSequence(BattleAbility ability, byte? nativeSequence = null)
    {
        if (nativeSequence is > 0)
        {
            return nativeSequence.Value;
        }
        // Native48D200 selects these original command sequences from executable B8150C.
        return !definition.IsPlayer ? AttackSequence : ability switch
        {
            BattleAbility.Attack => AttackSequence,
            BattleAbility.Magic => 11,
            BattleAbility.GuardianForce => 14,
            BattleAbility.Item => 15,
            BattleAbility.Draw => 30,
            _ => throw new NotSupportedException($"No original sequence for battle command {ability}.")
        };
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
                    // TODO(TD-009): The scaffold owns approach translation; remove the clip's horizontal root displacement once, preserving its vertical motion.
                    offset -= Vector3.TransformNormal(local, Matrix4x4.CreateRotationY(definition.IsPlayer ? MathF.PI : 0));
                }
                return new BattleAnimationSample(animation, frame, offset);
            }
            elapsed -= duration;
        }
        throw new NotSupportedException($"{model.Name} has no skeletal pose to present.");
    }
}
