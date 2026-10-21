namespace Memoria.FF8.Runtime;

public static class NativeBattleActorDirectorGeneric
{
    public static NativeBattleCuragaResources GenericNodes(this NativeBattleActorDirectorResources resources)
    {
        resources.Validate();
        return new(resources.Animation, resources.Sound) { Effect = resources.Effect, Executable = resources.NodeSource };
    }

    public static NativeBattleCuragaFrame ToGeneric(this NativeBattleActorDirectorFrame frame)
    {
        RequireFrame(frame);
        return new(frame.Memory, frame.Effect, frame.State.Generic);
    }

    public static NativeBattleActorDirectorFrame WithGeneric(this NativeBattleActorDirectorFrame frame, NativeBattleCuragaFrame generic)
    {
        RequireFrame(frame);
        if (generic.Effect.Binding.Effect != frame.Profile.Effect || generic.Effect.Binding.Entry != frame.Profile.Entry)
        {
            throw new InvalidDataException("The actor director cannot publish another module's generic frame.");
        }
        return frame with { Memory = generic.Memory, Effect = generic.Effect, State = frame.State with { Generic = generic.State } };
    }

    private static void RequireFrame(NativeBattleActorDirectorFrame frame)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != frame.State.Effect || frame.Effect.Binding.Entry != frame.Profile.Entry)
        {
            throw new InvalidDataException("The actor director requires its original generic frame binding.");
        }
    }
}
