using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeChocoboTrailPose(int X, int Height, int Z, ushort Yaw, bool Forest);

/// <summary>The animation and trail owners used while the player controls the ridden Chocobo.</summary>
public sealed record NativeChocoboWorldAnimation(
    [property: JsonRequired] int RiderAnimation,
    [property: JsonRequired] ushort RiderCounter,
    [property: JsonRequired] int MountAnimation,
    [property: JsonRequired] ushort MountCounter,
    [property: JsonRequired] int CompanionAnimation,
    [property: JsonRequired] ushort CompanionCounter,
    [property: JsonRequired] ImmutableArray<NativeChocoboTrailPose> Trail,
    [property: JsonRequired] int TrailWriteIndex,
    [property: JsonRequired] int CompanionTrailIndex,
    [property: JsonRequired] NativeChocoboTrailPose LastTrailPose,
    [property: JsonRequired] bool CompanionPlaced,
    [property: JsonRequired] bool CompanionWalking)
{
    public const int MountShape = 2;
    public const int CompanionShape = 3;

    [JsonIgnore]
    public NativeChocoboTrailPose CompanionPose
    {
        get
        {
            NativeChocoboTrailPose pose = Trail[CompanionTrailIndex];
            return pose with { Height = Math.Min(pose.Height, 64) };
        }
    }

    public static NativeChocoboWorldAnimation Create(int x, int height, int z, int yaw, bool forest)
    {
        NativeChocoboTrailPose pose = new(x, height, z, unchecked((ushort)yaw), forest);
        return new NativeChocoboWorldAnimation(5, 0, 0, 0, 0, 0, ImmutableArray.CreateRange(Enumerable.Repeat(pose, 16)), 3, 0, pose, false, false);
    }

    public void Validate()
    {
        if (RiderAnimation is < 4 or > 7 || MountAnimation is < 0 or > 3 || CompanionAnimation is < 0 or > 2 || Trail.IsDefault || Trail.Length != 16 || TrailWriteIndex is < 0 or > 15 || CompanionTrailIndex is < 0 or > 15 || CompanionWalking && !CompanionPlaced)
        {
            throw new InvalidDataException("Native Chocobo animation or trail state is invalid.");
        }
    }

    // Native5472C0 runs before terrain acceptance and uses each model's previously loaded clip length.
    public NativeChocoboWorldAnimation AdvanceAnimation(bool moving, bool riderVisible, bool mountVisible, bool companionVisible, int riderClipLength, int companionClipLength, NativeWorldForestState randomState, ReadOnlySpan<byte> random, out NativeWorldForestState advancedRandomState)
    {
        Validate();
        if (random.Length != 256 || riderVisible && riderClipLength is < 1 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Native Chocobo animation requires the original random table and lengths of the visible models' current clips.");
        }

        advancedRandomState = randomState;
        int riderAnimation = RiderAnimation;
        int riderCounter = RiderCounter;
        if (riderVisible)
        {
            if (moving)
            {
                riderAnimation = 6;
            }
            else if (RiderAnimation == 6)
            {
                riderAnimation = 5;
            }
            else if (RiderAnimation == 5)
            {
                advancedRandomState = advancedRandomState.DrawRandom(random, out byte value);
                if ((value & 0xFC) == 0)
                {
                    riderAnimation = 7;
                }
            }

            riderCounter = riderAnimation == RiderAnimation ? RiderCounter + 16 : 0;
            if (riderCounter >= riderClipLength)
            {
                riderCounter %= riderClipLength;
                if (riderAnimation == 7)
                {
                    riderAnimation = 5;
                    riderCounter = 0;
                }
            }
        }

        int mountAnimation = MountAnimation;
        ushort mountCounter = MountCounter;
        if (mountVisible)
        {
            mountAnimation = riderAnimation - 5;
            mountCounter = unchecked((ushort)riderCounter);
        }

        NativeChocoboWorldAnimation animation = this with { RiderAnimation = riderAnimation, RiderCounter = unchecked((ushort)riderCounter), MountAnimation = mountAnimation, MountCounter = mountCounter };
        return animation.AdvanceCompanionAnimation(companionVisible, companionClipLength, advancedRandomState, random, out advancedRandomState);
    }

    public NativeChocoboWorldAnimation AdvanceCompanionAnimation(bool visible, int clipLength, NativeWorldForestState randomState, ReadOnlySpan<byte> random, out NativeWorldForestState advancedRandomState)
    {
        advancedRandomState = randomState;
        if (!visible)
        {
            return this;
        }
        if (random.Length != 256 || clipLength is < 1 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Native companion animation requires its original random table and visible clip length.");
        }

        int animation = CompanionAnimation;
        if (CompanionWalking)
        {
            animation = 1;
        }
        else if (CompanionAnimation == 1)
        {
            animation = 0;
        }
        else if (CompanionAnimation == 0)
        {
            advancedRandomState = advancedRandomState.DrawRandom(random, out byte value);
            if ((value & 0xFE) == 0)
            {
                animation = 2;
            }
        }

        int counter = animation == CompanionAnimation ? CompanionCounter + 48 : 0;
        if (counter >= clipLength)
        {
            counter %= clipLength;
            if (animation == 2)
            {
                animation = 0;
                counter = 0;
            }
        }
        return this with { CompanionAnimation = animation, CompanionCounter = unchecked((ushort)counter) };
    }

    // Native54DAD0 writes the accepted pose only when X or Z changes and advances the companion's independent cursor.
    public NativeChocoboWorldAnimation AdvanceTrail(int x, int height, int z, int yaw, bool forest, int acceptedSpeed, bool companionAllocated = true, bool escaping = false)
    {
        Validate();
        NativeChocoboTrailPose pose = new(x, height, z, unchecked((ushort)yaw), forest);
        ImmutableArray<NativeChocoboTrailPose> trail = Trail;
        int writer = TrailWriteIndex;
        if (x != LastTrailPose.X || z != LastTrailPose.Z)
        {
            trail = trail.SetItem(writer, pose);
            writer = (writer + 1) & 15;
        }

        int reader = CompanionTrailIndex;
        bool walking = companionAllocated && (reader != ((writer - 3) & 15) || escaping && reader != writer);
        if (walking && (acceptedSpeed == 0 || reader == ((writer + 1) & 15)))
        {
            reader = (reader + 1) & 15;
        }

        return this with { Trail = trail, TrailWriteIndex = writer, CompanionTrailIndex = reader, LastTrailPose = pose, CompanionPlaced = companionAllocated, CompanionWalking = walking };
    }
}
