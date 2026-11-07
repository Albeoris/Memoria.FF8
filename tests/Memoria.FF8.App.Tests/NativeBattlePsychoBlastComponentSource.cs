using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattlePsychoBlastComponentSource
{
    internal static bool Supports(ushort effect) => effect == 23;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string output)
    {
        if (!Supports(effect))
        {
            return null;
        }
        if (Directory.Exists(output))
        {
            throw new IOException("Psycho Blast preparation requires a fresh owned output directory.");
        }
        Directory.CreateDirectory(output);
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(gameDirectory);
        NativeEffectFixtureRequest carrierRequest = (NativeEffectFixtureRequest)Invoke(typeof(NativeBattleEffect20PresentationTests), "SelectOriginalFixture", assets, (ushort)20)!;
        string fixture = Path.Combine(output, "carrier");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(carrierRequest, gameDirectory, fixture);
        if (exported.Status == "blocked")
        {
            throw new InvalidDataException($"Original activation carrier failed at {exported.Boundary}: {exported.FirstBlocker}");
        }
        BattleSession active = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json")));
        active.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
        BattleSnapshot? completed = null;
        int callbacks = 0;
        using (BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native))
        {
            for (; callbacks < 4096; callbacks++)
            {
                active.Tick();
                BattleSnapshot current = active.Capture();
                if (current.NativeGraphics!.OriginalEffect is { Binding.Effect: 20, ReturnedDescriptor: 0 } prior && prior.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0)) && current.NativeEffectMemory!.Active == 0 && current.ActiveAction is null && current.QueuedActions.Length == 0 && active.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    completed = current;
                    break;
                }
            }
        }
        if (completed is null)
        {
            throw new InvalidDataException("The original effect20 activation carrier did not finish and drain before Psycho Blast preparation.");
        }
        string sourcePath = Path.Combine(output, "activated-source.battle.json");
        File.WriteAllText(sourcePath, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(completed)));
        NativeEffectFixtureRequest request = (NativeEffectFixtureRequest)Invoke(typeof(NativeBattlePsychoBlast23PresentationTests), "SelectOriginalFixture", assets, effect)!;
        string? previousSource = Environment.GetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_SOURCE");
        string? previousEvidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_EVIDENCE");
        BattleSnapshot prepared;
        try
        {
            Environment.SetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_SOURCE", sourcePath);
            Environment.SetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_EVIDENCE", output);
            prepared = (BattleSnapshot)Invoke(typeof(NativeBattlePsychoBlast23PresentationTests), "PrepareStandalone", game, assets, request)!;
        }
        finally
        {
            Environment.SetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_SOURCE", previousSource);
            Environment.SetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_EVIDENCE", previousEvidence);
        }
        File.WriteAllText(Path.Combine(output, "activation-preparation.json"), JsonSerializer.Serialize(new { Scope = "Original effect20 activation followed by existing Psycho Blast component preparation; no ordinary AI or formula admission", CarrierEffect = 20, CarrierCallbacks = callbacks + 1, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback, Effect = effect, request.EncounterId, request.CasterActorIndex }));
        foreach (string snapshot in Directory.EnumerateFiles(output, "*.battle.json", SearchOption.AllDirectories))
        {
            File.Delete(snapshot);
        }
        return prepared;
    }

    private static object? Invoke(Type owner, string name, params object?[] arguments)
    {
        MethodInfo method = owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static) ?? throw new MissingMethodException(owner.FullName, name);
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
