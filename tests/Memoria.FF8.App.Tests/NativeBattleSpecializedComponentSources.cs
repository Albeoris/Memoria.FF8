using System.Reflection;
using System.Runtime.ExceptionServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleSpecializedComponentSources
{
    internal static bool Supports(ushort effect) => effect is 64 or 66 or 69 or 76 or 80 or 316;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string output)
    {
        if (!Supports(effect))
        {
            return null;
        }
        Directory.CreateDirectory(output);
        string[] intermediates = [Path.Combine(output, "entry"), Path.Combine(output, "allocation")];
        if (intermediates.Any(Directory.Exists))
        {
            throw new IOException("Specialized component preparation requires fresh owned entry and allocation directories.");
        }
        BattleSnapshot prepared = PrepareSupported(effect, gameDirectory, output);
        foreach (string directory in intermediates.Where(Directory.Exists))
        {
            foreach (string snapshot in Directory.EnumerateFiles(directory, "*.battle.json"))
            {
                File.Delete(snapshot);
            }
        }
        return prepared;
    }

    private static BattleSnapshot PrepareSupported(ushort effect, string gameDirectory, string output)
    {
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(gameDirectory);
        NativeEffectFixtureRequest request = effect switch
        {
            69 => new("original69-entry", 69, 8, 350, 0, 1200, 240) { EncounterId = 509, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true },
            76 => new("original76-entry", 76, 8, 355, 3, 1200, 240) { EncounterId = 981, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true },
            316 => new("original130-entry", 316, 8, 203, 0, 1200, 240) { EncounterId = 441, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true },
            _ => new("original511-entry", 207, 8, 365, 0, 1200, 240) { EncounterId = 511, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true }
        };
        string entryOutput = Path.Combine(output, "entry");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, gameDirectory, entryOutput);
        if (exported.Status == "blocked")
        {
            throw new InvalidDataException($"Original component entry failed at {exported.Boundary}: {exported.FirstBlocker}");
        }
        string sourcePath = Path.Combine(entryOutput, "after-entry.battle.json");
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath);
        if (effect is 64 or 66 or 80)
        {
            string allocationOutput = Path.Combine(output, "allocation");
            source = NativeBattleEffect80FixturePreparation.Prepare(game, assets, source, allocationOutput).Snapshot;
            sourcePath = Path.Combine(allocationOutput, "allocation-ready.battle.json");
        }
        if (effect == 316)
        {
            return NativeBattleEffect316PresentationTests.QueueComponent(source, game, assets);
        }
        Type owner = effect switch
        {
            64 => typeof(NativeBattleEffect64PresentationTests),
            66 => typeof(NativeBattleEffect66PresentationTests),
            69 => typeof(NativeBattleEffect69PresentationTests),
            76 => typeof(NativeBattleEffect76PresentationTests),
            80 => typeof(NativeBattleEffect80PresentationTests),
            _ => throw new InvalidOperationException("A supported component has no source preparation owner.")
        };
        MethodInfo prepare = owner.GetMethod("PrepareSource", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException(owner.FullName, "PrepareSource");
        string variable = $"MEMORIA_FF8_EFFECT{effect}_SOURCE_SNAPSHOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, sourcePath);
        try
        {
            return (BattleSnapshot)(prepare.Invoke(null, [game, assets, output]) ?? throw new InvalidDataException("The original component preparation returned no standalone source."));
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }
}
