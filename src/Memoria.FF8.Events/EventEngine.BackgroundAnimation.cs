namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private BackgroundAnimation GetBackgroundAnimation(int moduleIndex)
    {
        if (!backgroundAnimations.TryGetValue(moduleIndex, out BackgroundAnimation? animation))
        {
            animation = new BackgroundAnimation();
            backgroundAnimations.Add(moduleIndex, animation);
        }
        return animation;
    }

    private static int BackgroundFrame(int position) => position == -1 ? -1 : (byte)(position >> 6);

    private void HoldBackground(int moduleIndex, int position)
    {
        BackgroundAnimation animation = GetBackgroundAnimation(moduleIndex);
        animation.Position = position;
        animation.Speed = 1;
        animation.Counter = 1;
        animation.Active = false;
        animation.Complete = false;
        animation.Loop = false;
        backgroundStates[moduleIndex] = BackgroundFrame(position);
    }

    private void StepBackgroundAnimation(int moduleIndex)
    {
        BackgroundAnimation animation = GetBackgroundAnimation(moduleIndex);
        animation.Counter = (short)(animation.Counter - 1);
        if (animation.Counter > 0)
        {
            return;
        }
        animation.Counter = animation.Speed;
        if (!animation.Active)
        {
            return;
        }
        bool reverse = animation.Last < animation.First;
        int endpoint = (short)((animation.Last << 6) + (reverse ? 0 : 63));
        animation.Position = (short)(animation.Position + (reverse ? -64 : 64));
        if (reverse ? animation.Position < endpoint : animation.Position >= endpoint)
        {
            if (animation.Loop)
            {
                animation.Position = (short)((animation.First << 6) + (reverse ? 63 : 0));
                backgroundStates[moduleIndex] = BackgroundFrame(animation.Position);
                return;
            }
            animation.Position = endpoint;
            animation.Active = false;
            animation.Complete = true;
        }
        backgroundStates[moduleIndex] = BackgroundFrame(animation.Position);
    }

    private string? UnsupportedBackgroundAnimationFlag(Fiber fiber)
    {
        return fiber.Stack.Count > 0 && fiber.Stack.PeekValue().IsExact && program!.Objects[fiber.ObjectId].BackgroundModuleIndex == fiber.BackgroundId ? null : "BGANIMEFLAG requires its exact original operand and calling background module";
    }

    private int[] ExecuteBackgroundAnimationFlag(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        //520880 stores only lowWORD404 and returns2;52AEF0 and475480
        //consume independent animation/shade words without reading this field.
        GetBackgroundAnimation(fiber.BackgroundId).NativeFlag404 = unchecked((short)args[0]);
        return args;
    }
    private sealed class BackgroundAnimation
    {
        public int First;
        public int Last;
        public int Speed;
        public int Counter;
        public int Position = -1;
        public bool Active;
        public bool Complete;
        public bool Loop;
        public int NativeVersion = 1;
        public short? NativeFlag404;
    }
}
