using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7ChocoboSceneryPose(int Model, Ff7ChocoboPoint Position, byte RotationY, int Frame);
public sealed record Ff7ChocoboSceneryState(short[] Frames, short[] Increments, Ff7ChocoboSceneryPose[] Poses);

public sealed partial class Ff7ChocoboRuntime
{
    private short[] sceneryFrames = [];
    private short[] sceneryIncrements = [];
    private readonly Dictionary<int, Ff7ChocoboSceneryPose> sceneryPoses = [];

    public IReadOnlyList<Ff7ChocoboSceneryPose> SceneryPoses => sceneryPoses.Values.OrderBy(pose => pose.Model).ToArray();
    private Ff7ChocoboPresentationData Presentation => parameters[7] == 0 ? native.LongPresentation : native.ShortPresentation;

    private void InitializeScenery(Ff7ChocoboSceneryState? saved)
    {
        sceneryFrames = Presentation.Scenery.Select(descriptor => descriptor.Frame).ToArray();
        sceneryIncrements = Presentation.Scenery.Select(descriptor => descriptor.Increment).ToArray();
        if (saved is not null)
        {
            if (saved.Frames is null || saved.Increments is null || saved.Poses is null || saved.Frames.Length != sceneryFrames.Length || saved.Increments.Length != sceneryIncrements.Length || saved.Poses.Any(pose => pose is null || pose.Model < 16 || pose.Model >= Presentation.Models.Count))
            {
                throw new InvalidDataException("The FFVII Chocobo scenery snapshot differs from the selected native resources.");
            }
            saved.Frames.CopyTo(sceneryFrames, 0);
            saved.Increments.CopyTo(sceneryIncrements, 0);
            foreach (Ff7ChocoboSceneryPose pose in saved.Poses)
            {
                sceneryPoses[pose.Model] = pose;
            }
        }
        simulation.AnimationEvent += SetSceneryAnimation;
        Camera.AnimationEvent += SetSceneryAnimation;
    }

    private void SetSceneryAnimation(int value)
    {
        int descriptor = value > 128 ? value - 128 : value;
        if ((uint)descriptor >= sceneryIncrements.Length)
        {
            throw new InvalidDataException("The FFVII Chocobo animation event references absent scenery.");
        }
        sceneryIncrements[descriptor] = (short)(value > 128 ? -1 : 1);
    }

    private void AdvanceScenery()
    {
        sceneryPoses.Clear();
        int start = (Camera.CullStart + Course.Track.Count) % Course.Track.Count;
        int end = (Camera.CullEnd + Course.Track.Count) % Course.Track.Count;
        if (start > end)
        {
            Visit(0, end);
            Visit(start, Course.Track.Count);
        }
        else
        {
            Visit(start, end);
        }

        void Visit(int first, int last)
        {
            IReadOnlyList<int> counts = parameters[7] == 0 ? native.LongFrameCounts : native.ShortFrameCounts;
            for (int track = first; track < last; track++)
            {
                int descriptor = Course.Track[track].SceneryEvent - 1;
                if (descriptor == -1)
                {
                    continue;
                }
                if ((uint)descriptor >= sceneryFrames.Length)
                {
                    throw new InvalidDataException("The FFVII Chocobo course references absent scenery.");
                }
                Ff7ChocoboSceneryDescriptor scenery = Presentation.Scenery[descriptor];
                sceneryPoses[scenery.Model] = new(scenery.Model, new(scenery.X, scenery.Y, scenery.Z), scenery.RotationY, sceneryFrames[descriptor]);
                sceneryFrames[descriptor] = unchecked((short)(sceneryFrames[descriptor] + sceneryIncrements[descriptor]));
                if (sceneryFrames[descriptor] >= counts[scenery.Model])
                {
                    sceneryFrames[descriptor] = (scenery.Flags & 2) != 0 ? (short)0 : checked((short)counts[scenery.Model]);
                }
                if (sceneryFrames[descriptor] < 0)
                {
                    sceneryFrames[descriptor] = 0;
                }
            }
        }
    }

    private Ff7ChocoboSceneryState CaptureScenery()
    {
        return new(sceneryFrames.ToArray(), sceneryIncrements.ToArray(), sceneryPoses.Values.OrderBy(pose => pose.Model).ToArray());
    }
}
