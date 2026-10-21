namespace Memoria.FF8.Runtime;

public static class NativeBattleMightyGuardGeneric
{
    public static NativeBattleCuragaResources GenericNodes(this NativeBattleMightyGuardResources resources)
    {
        resources.Validate();
        return new(resources.Animation, resources.Sound) { Effect = resources.Effect, Executable = resources.NodeSource };
    }

    public static NativeBattleCuragaFrame ToGeneric(this NativeBattleMightyGuardFrame frame)
    {
        frame.State.Validate();
        NativeBattleMightyGuardState state = frame.State;
        if (state.Effect != frame.Effect.Binding.Effect || frame.Profile.Entry != frame.Effect.Binding.Entry)
        {
            throw new InvalidDataException("Mighty Guard cannot execute another module's generic nodes.");
        }
        NativeBattleCuragaState generic = new(state.SourceArenaOffset ?? throw new NotSupportedException("Mighty Guard requires its executed arena allocation."), state.PrimaryDrawCursor, state.SecondaryDrawCursor, state.AuxiliaryCursor ?? throw new NotSupportedException("Mighty Guard requires its actual auxiliary cursor."), frame.Camera)
        {
            Auxiliary25F8 = state.Emitters,
            Auxiliary16F80 = state.Geometry,
            Counter26D76C0 = state.EmitterScan,
            Counter26D82E8 = state.GeometryScan,
            Counter26D82D8 = state.GeometryReset,
            Counter26D75C4 = state.EmitterReset,
            PassCount26D82EA = state.PassEmitters,
            PassCount26D82DA = state.PassGeometry
        };
        return new(frame.Memory.WithMightyGuard(state), frame.Effect, generic);
    }

    public static NativeBattleMightyGuardFrame WithGeneric(this NativeBattleMightyGuardFrame frame, NativeBattleCuragaFrame generic)
    {
        if (generic.Effect.Binding.Effect != frame.State.Effect || generic.Effect.Binding.Entry != frame.Profile.Entry)
        {
            throw new InvalidDataException("Mighty Guard cannot publish another module's generic nodes.");
        }
        NativeBattleCuragaState nodes = generic.State;
        NativeBattleMightyGuardState retained = generic.Memory.RequireMightyGuard(frame.State.Effect);
        retained = retained with
        {
            SourceArenaOffset = nodes.SourceArenaOffset,
            PrimaryDrawCursor = nodes.PrimaryDrawCursor,
            SecondaryDrawCursor = nodes.SecondaryDrawCursor,
            AuxiliaryCursor = nodes.AuxiliaryCursor,
            Emitters = nodes.Auxiliary25F8,
            Geometry = nodes.Auxiliary16F80,
            EmitterScan = nodes.Counter26D76C0 ?? throw new NotSupportedException("Mighty Guard lost its actual emitter scan cursor."),
            GeometryScan = nodes.Counter26D82E8 ?? throw new NotSupportedException("Mighty Guard lost its actual geometry scan cursor."),
            EmitterReset = nodes.Counter26D75C4 ?? throw new NotSupportedException("Mighty Guard lost its actual emitter reset counter."),
            GeometryReset = nodes.Counter26D82D8 ?? throw new NotSupportedException("Mighty Guard lost its actual geometry reset counter."),
            PassEmitters = nodes.PassCount26D82EA ?? throw new NotSupportedException("Mighty Guard lost its actual emitter pass count."),
            PassGeometry = nodes.PassCount26D82DA ?? throw new NotSupportedException("Mighty Guard lost its actual geometry pass count.")
        };
        return frame with { Memory = generic.Memory.WithMightyGuard(retained), Effect = generic.Effect, State = retained };
    }

    public static NativeBattleCuragaExecutableState CaptureExecutable(NativeBattleMightyGuardState state)
    {
        state.Validate();
        return new(state.InitializationFlag, state.Executable) { ResourceAddress = NativeBattleMightyGuardProfile.ForEffect(state.Effect).SourceAddress };
    }
}
