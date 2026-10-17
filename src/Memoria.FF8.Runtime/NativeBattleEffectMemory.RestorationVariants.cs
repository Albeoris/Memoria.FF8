using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRestorationExecutableState? Effect57Executable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRestorationExecutableState? RemedyExecutable { get; init; }

    public NativeBattleRestorationExecutableState? GetRestorationExecutable(ushort effect)
    {
        return effect switch
        {
            24 => EsunaExecutable,
            27 => FullLifeExecutable,
            57 => Effect57Executable,
            67 => RemedyExecutable,
            _ => throw new NotSupportedException("Native restoration executable selection requires its original effect profile.")
        };
    }

    public NativeBattleEffectMemory WithRestorationExecutable(NativeBattleRestorationExecutableState executable)
    {
        executable.Validate();
        return executable.Effect switch
        {
            24 => this with { EsunaExecutable = executable },
            27 => this with { FullLifeExecutable = executable },
            57 => this with { Effect57Executable = executable },
            67 => this with { RemedyExecutable = executable },
            _ => throw new NotSupportedException("Native restoration executable publication requires its original effect profile.")
        };
    }

    private void ValidateRestorationVariants()
    {
        Effect57Executable?.Validate();
        RemedyExecutable?.Validate();
        if (Effect57Executable is { Effect: not 57 } || RemedyExecutable is { Effect: not 67 })
        {
            throw new InvalidDataException("Native restoration executable owners cannot be exchanged between spells.");
        }
    }
}
