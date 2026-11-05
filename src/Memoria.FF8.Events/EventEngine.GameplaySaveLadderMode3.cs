using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static GameplaySaveActorLadderMode3? CaptureGameplayLadderMode3(ActorState actor)
    {
        if (actor.NativeLadderMode3 is not { Active: true } ladder)
        {
            return null;
        }
        if (!float.IsFinite(actor.AnimationFrame) || actor.AnimationFrame * 16 != MathF.Truncate(actor.AnimationFrame * 16) || actor.AnimationFrame * 16 is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Gameplay mode3 ladder requires its exact independent renderer phase.");
        }
        GameplaySaveActorLadderMode3 result = new(ladder.FiberSequence, ladder.ScriptId, ladder.Operation, ladder.Up, new(ladder.Entry.X, ladder.Entry.Y, ladder.Entry.Z), new(ladder.Target.X, ladder.Target.Y, ladder.Target.Z), ladder.Duration, ladder.InitializationSpeed, (ushort)(actor.AnimationFrame * 16));
        result.Validate();
        return result;
    }

    private static void RestoreGameplayLadderMode3(ActorState actor, GameplaySaveFieldActor saved)
    {
        if (saved.LadderMode3 is not { } ladder)
        {
            return;
        }
        ladder.Validate();
        GameplaySaveActorPosition raw = saved.Pose.FixedPosition ?? throw new InvalidDataException("Gameplay mode3 ladder requires its canonical exact raw actor pose.");
        actor.NativeLadderMode3 = new(1, true, ladder.FiberSequence, ladder.ScriptId, ladder.Operation, ladder.Up, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, new(ladder.Entry.X, ladder.Entry.Y, ladder.Entry.Z), new(ladder.Target.X, ladder.Target.Y, ladder.Target.Z), ladder.Duration, ladder.InitializationSpeed);
    }

    private void RestoreGameplayLadderRenderer(ActorState actor, GameplaySaveFieldActor saved)
    {
        if (saved.LadderMode3 is not { } ladder)
        {
            return;
        }
        int terminal = NativeAnimationTerminal(actor, actor.AnimationId);
        if (terminal <= 0)
        {
            throw new NotSupportedException("Gameplay mode3 ladder requires a nonempty selected current animation clip.");
        }
        // The renderer phase is independent of the physical header phase. Only presentation loops normalize to a changed clip length.
        actor.AnimationFrame = (ladder.RendererFrame < terminal ? ladder.RendererFrame : ladder.RendererFrame % terminal) / 16f;
    }

    private void ValidateGameplayLaddersMode3(IReadOnlyList<Fiber> current)
    {
        ValidateNativeLaddersMode3(actors, Scene, current, partyJoin, partySplit);
        foreach (ActorState actor in actors.Where(actor => actor.NativeLadderMode3 is { Active: true }))
        {
            NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
            if (header.BoundModelId is null or < 0 || header.Frame is null || header.Terminal is null || header.Flags is null || AnimationFrameCount?.Invoke(actor.RenderModelId, actor.AnimationId) is not (> 0 and <= 4095) || !float.IsFinite(actor.AnimationFrame) || actor.AnimationFrame * 16 != MathF.Truncate(actor.AnimationFrame * 16) || actor.AnimationFrame * 16 is < 0 or > ushort.MaxValue)
            {
                throw new NotSupportedException("Gameplay mode3 ladder requires its current bound animation and logical renderer phase.");
            }
        }
    }
}
