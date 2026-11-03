using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record EncounterArrivalState(EncounterArrivalTrack Track, NativeBattleSequenceState ResumeSequence, long ElapsedCallbacks = 0, long? LastCallback = null, bool Complete = false)
{
    public EncounterArrivalSample Sample => Track.Sample(checked(Track.StartedAtCallback + ElapsedCallbacks));

    public EncounterArrivalState Advance(long callback, bool paused)
    {
        if (callback < LastCallback)
        {
            throw new InvalidDataException("Arrival callback cannot move backwards.");
        }
        if (Complete || LastCallback == callback)
        {
            return this;
        }
        if (paused)
        {
            return this with { LastCallback = callback };
        }
        if (Sample.Complete)
        {
            return this with { LastCallback = callback, Complete = true };
        }
        return this with { LastCallback = callback, ElapsedCallbacks = checked(ElapsedCallbacks + (LastCallback.HasValue ? 1 : 0)) };
    }
}

public sealed partial class BattleSession
{
    private ImmutableArray<EncounterArrivalState> encounterArrivals = [];

    public ImmutableArray<EncounterArrivalState> CaptureEncounterArrivals() => encounterArrivals;

    public bool EncounterArrivalComplete(string actorId) => encounterArrivals.All(state => state.Track.ActorId != actorId || state.Complete);

    public void RestoreEncounterArrivals(ImmutableArray<EncounterArrivalState> states)
    {
        if (states.IsDefault || states.Select(state => state.Track.ActorId).Distinct(StringComparer.Ordinal).Count() != states.Length)
        {
            throw new InvalidDataException("Arrival snapshot requires distinct actor identities and an initialized track collection.");
        }
        foreach (EncounterArrivalState state in states)
        {
            _ = state.Sample;
            if (state.ElapsedCallbacks < 0)
            {
                throw new InvalidDataException("Arrival snapshot has a negative source clock.");
            }
        }
        encounterArrivals = states;
    }

    /// <summary>Call after the whole reinforcement batch is ready and before publishing its first visible frame.</summary>
    public void StartEncounterArrival(IReadOnlyList<EncounterArrivalTrack> tracks)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models;
        ImmutableArray<EncounterArrivalState>.Builder pending = ImmutableArray.CreateBuilder<EncounterArrivalState>();
        foreach (EncounterArrivalTrack supplied in tracks)
        {
            if (encounterArrivals.Any(state => state.Track.ActorId == supplied.ActorId) || pending.Any(state => state.Track.ActorId == supplied.ActorId))
            {
                throw new InvalidDataException($"Arrival for actor '{supplied.ActorId}' has already been started.");
            }
            int index = graphics.ActorIds.IndexOf(supplied.ActorId);
            if (index < 0 || (models[index].Flags0 & 2) == 0)
            {
                throw new InvalidDataException($"Arrival actor '{supplied.ActorId}' requires a fully loaded native allocation.");
            }
            EncounterArrivalTrack track = supplied with { StartedAtCallback = graphics.Callback };
            NativeBattleGraphicsModel model = models[index];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics with { Models = models }, index);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Arrival requires the selected model's native skeletal pose source.");
            ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(source.Animations.AsSpan());
            if (track.Animation >= counts.Length || track.LastFrame >= counts[track.Animation])
            {
                throw new InvalidDataException("Arrival endpoint exceeds the selected native animation's source frames.");
            }
            if (resource.LinkedPoseSource is { } linked)
            {
                ImmutableArray<byte> linkedCounts = NativeBattlePoseDecoder.ReadFrameCounts(linked.Animations.AsSpan());
                if (track.Animation >= linkedCounts.Length || track.LastFrame >= linkedCounts[track.Animation])
                {
                    throw new InvalidDataException("Arrival endpoint exceeds the selected linked model's native animation source frames.");
                }
            }
            EncounterArrivalState state = new(track, model.SequenceState ?? throw new InvalidDataException("Arrival requires an initialized native standing sequence."));
            model = PublishEncounterArrivalPose(graphics with { Models = models }, model, state.Sample);
            models = models.SetItem(index, model);
            pending.Add(state);
        }
        encounterArrivals = encounterArrivals.AddRange(pending);
        NativeGraphics = graphics with { Models = models };
        RefreshPreparedPresentation();
    }

    private bool ApplyEncounterArrivalBeforeDraw(NativeBattleGraphicsState graphics, ref NativeBattleGraphicsModel model)
    {
        string? actorId = graphics.ActorIds[model.Index];
        int found = -1;
        for (int index = 0; index < encounterArrivals.Length; index++)
        {
            if (encounterArrivals[index].Track.ActorId == actorId && !encounterArrivals[index].Complete)
            {
                found = index;
                break;
            }
        }
        if (found < 0)
        {
            return false;
        }
        EncounterArrivalState state = encounterArrivals[found].Advance(graphics.Callback, (graphics.Flags & 0x201) != 0);
        encounterArrivals = encounterArrivals.SetItem(found, state);
        if (state.Complete)
        {
            model = model with { SequenceState = state.ResumeSequence };
            return false;
        }
        model = PublishEncounterArrivalPose(graphics, model, state.Sample);
        return true;
    }

    private NativeBattleGraphicsModel PublishEncounterArrivalPose(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, EncounterArrivalSample sample)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, model.Index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Arrival requires the selected model's native skeletal pose source.");
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(source.Animations.AsSpan());
        if (sample.Animation >= counts.Length || sample.NativeFrameCounter > counts[sample.Animation])
        {
            throw new InvalidDataException("Arrival track exceeds the selected native animation's source frames.");
        }
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new InvalidDataException("Arrival requires its initialized native sequence owner.");
        sequence = sequence with { Pose = sample.Animation, PoseFlags109 = 0, PoseFrame114 = sample.NativeFrameCounter, PoseLimit115 = counts[sample.Animation], DecodedPose = source.Sample(sample.Animation, sample.NativeFrameCounter, 0) };
        if (resource.LinkedPoseSource is { } linked)
        {
            ImmutableArray<byte> linkedCounts = NativeBattlePoseDecoder.ReadFrameCounts(linked.Animations.AsSpan());
            sequence = sequence with { LinkedPose = new(sample.Animation, sample.NativeFrameCounter, linkedCounts[sample.Animation], 0), LinkedDecodedPose = linked.Sample(sample.Animation, sample.NativeFrameCounter, 0) };
        }
        model = model with { SequenceState = sequence };
        model = NativeBattleBodyBoneMatrices.Publish508F60(model, source, resource.LinkedPoseSource);
        return model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source, ReadNativeModelScaleVector502AB0(graphics, model)) };
    }
}
