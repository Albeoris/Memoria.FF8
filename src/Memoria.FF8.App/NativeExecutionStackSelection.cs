using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>A user-selected external execution environment for fresh processes; existing saves keep their own owners.</summary>
internal static class NativeExecutionStackSelection
{
    internal static string DefaultPath => Path.Combine(Memoria.FF8.Platform.ApplicationPaths.Current.LegacyState, "native-execution-stack.json");

    internal static string? ConfiguredPath
    {
        get
        {
            string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXECUTION_STACK_PROFILE");
            if (!string.IsNullOrEmpty(selected))
            {
                return selected;
            }
            string path = DefaultPath;
            return File.Exists(path) ? path : null;
        }
    }

    internal static NativeBattleExecutionStack? Read(string? path)
    {
        if (path is null)
        {
            return null;
        }
        using FileStream stream = File.OpenRead(path);
        NativeBattleExecutionStack profile = JsonSerializer.Deserialize<NativeBattleExecutionStack>(stream) ?? throw new InvalidDataException("The selected native execution-stack file contains no profile.");
        profile.Validate();
        profile.HeroCrtStack?.Validate(profile);
        return profile;
    }

    internal static NativeBattleRetainedMemory Apply(NativeBattleRetainedMemory memory, NativeBattleExecutionStack? profile)
    {
        if (profile is null)
        {
            return memory;
        }
        profile.Validate();
        profile.HeroCrtStack?.Validate(profile);
        NativeBattleEffectMemory effects = memory.EffectMemory ?? throw new InvalidDataException("The cold process has no native effect-memory owner for the selected execution stack.");
        return memory with { EffectMemory = effects with { ExecutionStack = profile, HeroCrtStack = profile.HeroCrtStack } };
    }
}
