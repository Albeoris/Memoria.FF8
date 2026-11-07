using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleActivatedComponentSources
{
    internal static bool Supports(ushort effect) => effect is 79 or 83 or 283 or 289;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string output)
    {
        if (!Supports(effect))
        {
            return null;
        }
        string carrierOutput = Path.Combine(output, "carrier");
        string focalOutput = Path.Combine(output, "focal");
        if (Directory.Exists(carrierOutput) || Directory.Exists(focalOutput))
        {
            throw new IOException("Activated component preparation requires fresh owned carrier and focal directories.");
        }
        Directory.CreateDirectory(carrierOutput);
        Directory.CreateDirectory(focalOutput);
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(gameDirectory);
        (ushort carrierEffect, Type carrierOwner, Type focalOwner) = effect switch
        {
            79 => ((ushort)8, typeof(NativeBattleDoom8PresentationTests), typeof(NativeBattleEffect79PresentationTests)),
            83 => ((ushort)8, typeof(NativeBattleDoom8PresentationTests), typeof(NativeBattleEffect83PresentationTests)),
            283 => ((ushort)303, typeof(NativeBattleEffect303PresentationTests), typeof(NativeBattleEffect283PresentationTests)),
            289 => ((ushort)315, typeof(NativeBattleEffect315PresentationTests), typeof(NativeBattleEffect289PresentationTests)),
            _ => throw new InvalidOperationException("A supported activated component has no original carrier.")
        };
        NativeEffectFixtureRequest carrierRequest = Select(carrierOwner, assets, carrierEffect);
        BattleSnapshot carrier;
        if (carrierEffect == 303)
        {
            carrier = PrepareDeclared(carrierOwner, game, assets, carrierRequest, gameDirectory, carrierOutput, null);
        }
        else
        {
            string fixture = Path.Combine(carrierOutput, "fixture");
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(carrierRequest, gameDirectory, fixture);
            if (exported.Status == "blocked")
            {
                throw new InvalidDataException($"Original activation carrier failed at {exported.Boundary}: {exported.FirstBlocker}");
            }
            carrier = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
        }
        BattleSession active = BattleSession.Restore(carrier);
        active.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        MethodInfo released = typeof(NativeBattleActorDirectorPresentationTests).GetMethod("Released", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException("Original component release observer is unavailable.");
        BattleSnapshot? completed = null;
        int callbacks = 0;
        using (BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native))
        {
            for (; callbacks < 4096; callbacks++)
            {
                active.Tick();
                BattleSnapshot current = active.Capture();
                if ((bool)Invoke(released, current)! && active.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    completed = current;
                    break;
                }
            }
        }
        if (completed is null)
        {
            throw new InvalidDataException("The original activation carrier did not fully drain within its existing component callback budget.");
        }
        NativeEffectFixtureRequest focalRequest = Select(focalOwner, assets, effect);
        if (completed.Definition.EncounterId != focalRequest.EncounterId)
        {
            throw new InvalidDataException("The completed original carrier and selected focal component have different formations.");
        }
        string sourcePath = Path.Combine(carrierOutput, "carrier-ready.battle.json");
        File.WriteAllText(sourcePath, JsonSerializer.Serialize(completed));
        BattleSnapshot prepared = PrepareDeclared(focalOwner, game, assets, focalRequest, gameDirectory, focalOutput, sourcePath);
        File.WriteAllText(Path.Combine(output, "carrier-preparation.json"), JsonSerializer.Serialize(new { Scope = "Original-resource controlled component after completed original carrier; no ordinary trigger or formula admission", CarrierEffect = carrierEffect, CarrierOwner = carrierOwner.Name, CarrierCallbacks = callbacks + 1, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback, completed.Definition.EncounterId, completed.Definition.StageId, FocalEffect = effect, FocalOwner = focalOwner.Name, FocalRequest = focalRequest }));
        foreach (string directory in new[] { carrierOutput, focalOutput })
        {
            foreach (string snapshot in Directory.EnumerateFiles(directory, "*.battle.json", SearchOption.AllDirectories))
            {
                File.Delete(snapshot);
            }
        }
        return prepared;
    }

    private static NativeEffectFixtureRequest Select(Type owner, BattleAssets assets, ushort effect)
    {
        MethodInfo selector = owner.GetMethod("SelectOriginalFixture", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException(owner.FullName, "SelectOriginalFixture");
        return (NativeEffectFixtureRequest)(Invoke(selector, assets, effect) ?? throw new InvalidDataException("The original component selector returned no request."));
    }

    private static BattleSnapshot PrepareDeclared(Type owner, GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request, string gameDirectory, string output, string? sourcePath)
    {
        MethodInfo prepare = owner.GetMethod("PrepareDeclaredSource", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException(owner.FullName, "PrepareDeclaredSource");
        Dictionary<string, string?> selected = new()
        {
            ["MEMORIA_FF8_GAME_DIR"] = gameDirectory,
            [$"MEMORIA_FF8_EFFECT{request.Effect}_EVIDENCE"] = output
        };
        if (sourcePath is not null)
        {
            selected[$"MEMORIA_FF8_EFFECT{request.Effect}_SOURCE_SNAPSHOT"] = sourcePath;
        }
        Dictionary<string, string?> previous = selected.ToDictionary(item => item.Key, item => Environment.GetEnvironmentVariable(item.Key));
        try
        {
            foreach ((string key, string? value) in selected)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
            return (BattleSnapshot)(Invoke(prepare, game, assets, request) ?? throw new InvalidDataException("The original component preparation returned no source."));
        }
        finally
        {
            foreach ((string key, string? value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static object? Invoke(MethodInfo method, params object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }
}
