using System.Reflection;
using System.Runtime.ExceptionServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Recreates the original entry source selected by an existing request factory, then delegates header preparation to its existing test contract.</summary>
internal static class NativeBattleExistingComponentSourceBridge
{
    internal static bool Supports(ushort effect) => effect is 54 or 63 or 167 or 209 or 227 or 290 or 301 or 318 or 319 or 324;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string outputDirectory)
    {
        if (!Supports(effect))
        {
            return null;
        }
        Type type = typeof(NativeBattleExistingComponentSourceBridge).Assembly.GetType($"Memoria.FF8.App.Tests.NativeBattleEffect{effect}PresentationTests", true)!;
        MethodInfo requestFactory = type.GetMethod("CreateOriginalRequest", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidDataException("The original component request factory is unavailable.");
        MethodInfo sourceFactory = type.GetMethod("PrepareSource", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidDataException("The original component source factory is unavailable.");
        NativeEffectFixtureRequest request = (NativeEffectFixtureRequest)Invoke(requestFactory, [])!;
        if (Directory.Exists(outputDirectory))
        {
            throw new IOException("Component source preparation requires a fresh owned output directory.");
        }
        Directory.CreateDirectory(outputDirectory);
        string fixture = Path.Combine(outputDirectory, "fixture");
        NativeEffectFixtureExportResult result = NativeBattleEffectFixtureExporter.Export(request, gameDirectory, fixture);
        if (result.Status == "blocked")
        {
            throw new InvalidDataException(result.FirstBlocker ?? "The original component entry source is blocked.");
        }
        string entry = Path.Combine(fixture, "after-entry.battle.json");
        if (!File.Exists(entry))
        {
            throw new InvalidDataException("The original request did not produce its entry-complete source.");
        }
        string variable = $"MEMORIA_FF8_EFFECT{effect}_SOURCE_SNAPSHOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, entry);
            using GameAssets game = new(gameDirectory);
            using BattleAssets assets = new(gameDirectory);
            BattleSnapshot source = (BattleSnapshot)Invoke(sourceFactory, [game, assets, outputDirectory])!;
            foreach (string snapshot in Directory.EnumerateFiles(outputDirectory, "*.battle.json", SearchOption.AllDirectories))
            {
                File.Delete(snapshot);
            }
            return source;
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    private static object? Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
