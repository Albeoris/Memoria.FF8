namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrinkMagic37Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleDrinkMagic37State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        RequireEffect();
        return new(Memory, Effect, State.Nodes ?? throw new NotSupportedException("Drink Magic generic nodes require the executed initializer cursors."));
    }

    public NativeBattleDrinkMagic37Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        RequireEffect();
        if (generic.Effect.Binding.Effect != 37 || generic.Effect.Binding.Entry != NativeBattleDrinkMagic37Profile.Entry)
        {
            throw new InvalidDataException("Drink Magic cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State } };
    }

    private void RequireEffect()
    {
        if (Effect.Binding.Effect != 37 || Effect.Binding.Entry != NativeBattleDrinkMagic37Profile.Entry)
        {
            throw new InvalidDataException("Drink Magic requires its original module binding.");
        }
        State.Validate();
    }
}

public abstract record NativeBattleDrinkMagic37Operation;
public sealed record NativeBattleDrinkMagic37Sound(NativeBattleModuleSound Sound) : NativeBattleDrinkMagic37Operation;
public sealed record NativeBattleDrinkMagic37Contact(int Index) : NativeBattleDrinkMagic37Operation;
public sealed record NativeBattleDrinkMagic37GenericTask(uint Address) : NativeBattleDrinkMagic37Operation;
public sealed record NativeBattleDrinkMagic37ParticleTask(uint Address) : NativeBattleDrinkMagic37Operation;
public sealed record NativeBattleDrinkMagic37OrbTask(uint Address) : NativeBattleDrinkMagic37Operation;

public sealed record NativeBattleDrinkMagic37Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleDrinkMagic37Frame, NativeBattleDrinkMagic37Operation, NativeBattleDrinkMagic37Frame> Execute)
{
    public ushort? CameraPadding18 { get; init; }
    public NativeBattlePackedMatrix32? PackedCamera { get; init; }
}
