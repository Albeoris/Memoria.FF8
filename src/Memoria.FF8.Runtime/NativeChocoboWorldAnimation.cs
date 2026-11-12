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
        if (RiderAnimation is < 5 or > 7 || MountAnimation is < 0 or > 2 || CompanionAnimation is < 0 or > 2 || Trail.IsDefault || Trail.Length != 16 || TrailWriteIndex is < 0 or > 15 || CompanionTrailIndex is < 0 or > 15 || CompanionWalking && !CompanionPlaced)
        {
            throw new InvalidDataException("Native Chocobo animation or trail state is invalid.");
        }
    }

    // Native5472C0 runs before terrain acceptance and uses each model's previously loaded clip length.
    public NativeChocoboWorldAnimation AdvanceAnimation(bool moving, bool riderVisible, bool mountVisible, bool companionVisible, int riderClipLength, int companionClipLength, NativeWorldForestState randomState, ReadOnlySpan<byte> random, out NativeWorldForestState advancedRandomState)
    {
        Validate();
        if (random.Length != 256 || riderVisible && riderClipLength is < 1 or > ushort.MaxValue || companionVisible && companionClipLength is < 1 or > ushort.MaxValue)
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

        int companionAnimation = CompanionAnimation;
        int companionCounter = CompanionCounter;
        if (companionVisible)
        {
            if (CompanionWalking)
            {
                companionAnimation = 1;
            }
            else if (CompanionAnimation == 1)
            {
                companionAnimation = 0;
            }
            else if (CompanionAnimation == 0)
            {
                advancedRandomState = advancedRandomState.DrawRandom(random, out byte value);
                if ((value & 0xFE) == 0)
                {
                    companionAnimation = 2;
                }
            }

            companionCounter = companionAnimation == CompanionAnimation ? CompanionCounter + 48 : 0;
            if (companionCounter >= companionClipLength)
            {
                companionCounter %= companionClipLength;
                if (companionAnimation == 2)
                {
                    companionAnimation = 0;
                    companionCounter = 0;
                }
            }
        }

        return this with { RiderAnimation = riderAnimation, RiderCounter = unchecked((ushort)riderCounter), MountAnimation = mountAnimation, MountCounter = mountCounter, CompanionAnimation = companionAnimation, CompanionCounter = unchecked((ushort)companionCounter) };
    }

    // Native54DAD0 writes the accepted pose only when X or Z changes and advances the companion's independent cursor.
    public NativeChocoboWorldAnimation AdvanceTrail(int x, int height, int z, int yaw, bool forest, int acceptedSpeed, bool companionAllocated = true)
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
        bool walking = companionAllocated && reader != ((writer - 3) & 15);
        if (walking && (acceptedSpeed == 0 || reader == ((writer + 1) & 15)))
        {
            reader = (reader + 1) & 15;
        }

        return this with { Trail = trail, TrailWriteIndex = writer, CompanionTrailIndex = reader, LastTrailPose = pose, CompanionPlaced = companionAllocated, CompanionWalking = walking };
    }
}
