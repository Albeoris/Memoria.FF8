namespace Memoria.FF8.Runtime;

public sealed record NativeWorldFootAnimationResult(int Animation, ushort Counter, int SharedCounter, NativeWorldForestState Forest);

/// <summary>Ordinary controlled foot branch of native54A2A0, after its actual draw qualification.</summary>
public static class NativeWorldFootAnimation
{
    public static NativeWorldFootAnimationResult Step(int entityType, int priorAnimation, ushort priorCounter, int loadedAnimationLength, bool moving, int? forcedAnimation, NativeWorldForestState forest, ReadOnlySpan<byte> random)
    {
        forest.Validate();
        if (!NativeWorldEntityTypes.IsSupportedFoot(entityType) || priorAnimation is < 0 or > 4 || loadedAnimationLength is < 1 or > ushort.MaxValue || random.Length != 256)
        {
            throw new InvalidDataException("Native foot animation requires its retained animation, loaded native length and original random table.");
        }
        if (forcedAnimation is null || forcedAnimation is < -1 or > 4 && forcedAnimation != 128)
        {
            throw new NotSupportedException("Native foot animation requires the actual54A7F0/54B460 forced-animation owner.");
        }

        int animation;
        int counter = priorCounter;
        if (moving)
        {
            animation = 1;
        }
        else if (priorAnimation == 1)
        {
            animation = 0;
            counter = 0;
        }
        else
        {
            animation = forcedAnimation.Value;
            if (animation < 0)
            {
                animation = priorAnimation;
                if (priorAnimation == 0)
                {
                    forest = forest.DrawRandom(random, out byte value);
                    if ((value & 0xFE) == 0)
                    {
                        animation = 128;
                    }
                }
            }
            if (animation == 128 && priorAnimation == 0)
            {
                if (entityType == 6)
                {
                    forest = forest.DrawRandom(random, out byte value);
                    animation = value > 0xC8 ? 2 : 4;
                }
                else
                {
                    animation = entityType <= 1 ? 2 : 3;
                }
                counter = 0;
            }
        }

        if (animation == 128)
        {
            throw new NotSupportedException("Native forced128 with a nonzero foot animation requires its separate producer contract.");
        }
        //54A2A0 uses the previously loaded model length, before653380 selects the new animation.
        if (!(priorAnimation == 1 && !moving))
        {
            if (priorAnimation != 0 && animation == priorAnimation)
            {
                counter += 16;
            }
            else if (animation != priorAnimation)
            {
                counter = 0;
            }
        }
        if (counter >= loadedAnimationLength)
        {
            if (animation == 1)
            {
                counter %= loadedAnimationLength;
            }
            else
            {
                if (entityType <= 1)
                {
                    if (priorAnimation == 3)
                    {
                        animation = 0;
                        counter = 0;
                    }
                    else
                    {
                        forest = forest.DrawRandom(random, out byte value);
                        animation = (value & 0xFC) == 0 ? 3 : animation;
                        counter = animation == 3 ? 0 : loadedAnimationLength - 1;
                    }
                }
                else if (entityType == 6)
                {
                    if (animation is 3 or 4)
                    {
                        animation = 0;
                        counter = 0;
                    }
                    else
                    {
                        forest = forest.DrawRandom(random, out byte value);
                        animation = (value & 0xFC) == 0 ? 3 : animation;
                        counter = animation == 3 ? 0 : loadedAnimationLength - 1;
                    }
                }
                else if (animation == 2)
                {
                    forest = forest.DrawRandom(random, out byte value);
                    animation = value > 0x40 ? 2 : 4;
                }
                else
                {
                    animation = animation == 3 ? 2 : 0;
                }
                if (entityType == 2)
                {
                    counter = 0;
                }
            }
        }
        return new(animation, unchecked((ushort)counter), counter, forest);
    }
}
