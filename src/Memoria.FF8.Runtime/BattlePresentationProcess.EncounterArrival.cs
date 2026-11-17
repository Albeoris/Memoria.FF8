using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private bool ApplyEncounterArrivalBeforeDraw(NativeBattleGraphicsState graphics, BattleModelOwner owner, ref NativeBattleGraphicsModel model)
    {
        string actorId = RequireCanonicalGraphicsActorId(owner);
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
        model = PublishEncounterArrivalPose(graphics, owner, model, state.Sample);
        return true;
    }

    private NativeBattleGraphicsModel PublishEncounterArrivalPose(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsModel model, EncounterArrivalSample sample)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, owner);
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
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source, ReadNativeModelScaleVector502AB0(graphics, model)) };
        return NativeBattleModelRings.BindWorldMatrix502AB0(graphics.ActorModels, owner, model);
    }

    private ImmutableArray<EncounterArrivalState> encounterArrivals = [];
    private ImmutableArray<EncounterArrivalState> sampledArrivalTracks;
    private ImmutableDictionary<string, float>? sampledAllocationOpacities;
    private ImmutableDictionary<string, float>? sampledArrivalOpacities;

    private ImmutableDictionary<string, float> ReadEncounterArrivalOpacities(ImmutableDictionary<string, float> allocationOpacities)
    {
        if (encounterArrivals.IsEmpty)
        {
            return allocationOpacities;
        }
        if (sampledArrivalOpacities is { } sampled && sampledArrivalTracks == encounterArrivals && ReferenceEquals(sampledAllocationOpacities, allocationOpacities))
        {
            return sampled;
        }
        ImmutableDictionary<string, float> opacities = allocationOpacities;
        foreach (EncounterArrivalState arrival in encounterArrivals)
        {
            if ((!arrival.Complete || arrival.Track.OpacityTo != 1) && !allocationOpacities.ContainsKey(arrival.Track.ActorId))
            {
                opacities = opacities.Add(arrival.Track.ActorId, arrival.Sample.Opacity);
            }
        }
        sampledArrivalTracks = encounterArrivals;
        sampledAllocationOpacities = allocationOpacities;
        sampledArrivalOpacities = opacities;
        return opacities;
    }

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
        StartEncounterArrival(tracks, null, null);
    }

    internal void StartEncounterArrival(IReadOnlyList<EncounterArrivalTrack> tracks, BattleModelOwner? anchor, IReadOnlyList<NativeBattleVector>? offsets)
    {
        RequireAdmissionWindow();
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleModelTransform? anchorTransform = null;
        if (anchor is not null)
        {
            anchorTransform = graphics.ActorModels.Get(anchor).Transform ?? throw new InvalidDataException("Encounter arrival requires the source actor's native placement.");
            if (offsets is null || offsets.Count != tracks.Count)
            {
                throw new InvalidDataException("Encounter arrival placements require one offset for each track.");
            }
        }
        BattleActorModels models = graphics.ActorModels;
        ImmutableArray<EncounterArrivalState>.Builder pending = ImmutableArray.CreateBuilder<EncounterArrivalState>();
        for (int index = 0; index < tracks.Count; index++)
        {
            EncounterArrivalTrack supplied = tracks[index];
            if (encounterArrivals.Any(state => state.Track.ActorId == supplied.ActorId) || pending.Any(state => state.Track.ActorId == supplied.ActorId))
            {
                throw new InvalidDataException($"Arrival for actor '{supplied.ActorId}' has already been started.");
            }
            BattleModelOwner owner = models.Roster.Owners.Single(model => StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(model), supplied.ActorId));
            NativeBattleGraphicsModel model = models.Get(owner);
            if ((model.Flags0 & 2) == 0)
            {
                throw new InvalidDataException($"Arrival actor '{supplied.ActorId}' requires a fully loaded native allocation.");
            }
            if (anchorTransform is not null)
            {
                NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("Arrival actor requires an initialized native transform.");
                NativeBattleVector offset = offsets![index];
                model = model with { Transform = transform with { Initial20 = Offset(anchorTransform.Initial20, offset), Position28 = Offset(anchorTransform.Position28, offset) } };
                models = models.Set(owner, model);
            }
            EncounterArrivalTrack track = supplied with { StartedAtCallback = graphics.Callback };
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics with { ActorModels = models }, owner);
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
            model = PublishEncounterArrivalPose(graphics with { ActorModels = models }, owner, model, state.Sample);
            models = models.Set(owner, model);
            pending.Add(state);
        }
        encounterArrivals = encounterArrivals.AddRange(pending);
        NativeGraphics = graphics with { ActorModels = models };

    }
    private static NativeBattleVector Offset(NativeBattleVector position, NativeBattleVector offset)
    {
        return new(checked((short)(position.X + offset.X)), checked((short)(position.Y + offset.Y)), checked((short)(position.Z + offset.Z)));
    }
    internal void RevealEncounterArrivalActors(ImmutableArray<BattleModelOwner> owners)
    {
        RequireAdmissionWindow();
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        BattleActorModels models = graphics.ActorModels;
        foreach (BattleModelOwner owner in owners)
        {
            NativeBattleGraphicsModel model = models.Get(owner);
            models = models.Set(owner, model with { Flags0 = (ushort)(model.Flags0 & ~4) });
        }
        NativeGraphics = graphics with { ActorModels = models };
    }

}
