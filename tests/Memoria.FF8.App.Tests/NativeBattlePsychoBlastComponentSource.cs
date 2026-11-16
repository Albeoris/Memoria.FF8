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
        BattleSnapshot carrier = NativeBattleEffectFixtureExporter.Prepare(carrierRequest, gameDirectory, fixture).Snapshot;
        BattleSession active = BattleSession.Restore(carrier);
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
        NativeEffectFixtureRequest request = (NativeEffectFixtureRequest)Invoke(typeof(NativeBattlePsychoBlast23PresentationTests), "SelectOriginalFixture", assets, effect)!;
        BattleSnapshot prepared = NativeBattlePsychoBlast23PresentationTests.PrepareActivatedSource(game, assets, request, NativeBattleStorageSnapshots.Capture(completed));
        File.WriteAllText(Path.Combine(output, "activation-preparation.json"), JsonSerializer.Serialize(new { Scope = "Original effect20 activation followed by existing Psycho Blast component preparation; no ordinary AI or formula admission", CarrierEffect = 20, CarrierCallbacks = callbacks + 1, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback, Effect = effect, request.EncounterId, request.CasterActorIndex }));
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
