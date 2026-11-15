using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentSourceTests
{
    [Fact]
    public void Prepare_or_render_one_selected_existing_component()
    {
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_EFFECT_ID");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_PREPARATION_RECEIPT");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(selected is not null && output is not null && game is not null, "Select installed resources, one existing component selector and a preparation receipt destination.");
        ushort effect = ushort.Parse(selected!, System.Globalization.CultureInfo.InvariantCulture);
        string receiptPath = Path.GetFullPath(output!);
        string evidence = Path.Combine(Path.GetDirectoryName(receiptPath)!, "selector-evidence");
        BattleSnapshot snapshot = PrepareSelected(effect, game!, evidence);
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics ?? throw new InvalidDataException("Component preparation requires its current native graphics owner.");
        ComponentSourceAdmission? admission = GameWorld.ValidateEffectComponentSource(snapshot, BattleSession.Restore(snapshot), effect);
        string boundary = admission is null ? "queued-action104" : graphics.OriginalEffect!.Parameters is null ? "module-loaded-before-entry" : "admitted-before-entry";
        int headerIndex = admission?.HeaderIndex ?? checked((int)((graphics.Descriptors.Packets[graphics.Descriptors.Queue.Single(index => graphics.Descriptors.Packets[index].Command == 104)].Argument - 0x1D280C4u) / 20));
        NativeBattleActionHeaderSlot header = snapshot.NativeActionPackets!.ReadHeader(new BattleActionHeaderReference(headerIndex));
        var inputs = new
        {
            Effect = effect,
            Executable = snapshot.Definition.NativeRetainedMemory?.ColdExecutableSha256,
            snapshot.Definition.EncounterId,
            snapshot.Definition.StageId,
            Combatants = snapshot.Definition.Combatants.Select(actor => new { actor.Id, actor.ModelId, actor.SceneSlot, actor.IsPlayer }).ToArray(),
            header.Command,
            header.Action,
            Header = headerIndex,
            snapshot.Frame,
            NativeCallback = graphics.Callback
        };
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(inputs)));
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        using (FileStream receipt = new(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            JsonSerializer.Serialize(receipt, new { Phase = "prepared", Boundary = boundary, Scope = "native-effect-component", Effect = effect, InputsSha256 = hash, Inputs = inputs, OrdinaryAdmissionCertified = false, FunctionalCompletionCertified = false });
        }
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_PREVIEW_PATH") is { } request)
        {
            RunGraphicalComponent(game!, request, new(snapshot, hash));
        }
    }

    internal static BattleSnapshot PrepareSelected(ushort effect, string game, string evidence)
    {
        NativeBattleComponentSelectorEntry entry = NativeBattleComponentSelectorCatalog.Require(effect);
        Dictionary<string, string?> saved = [];
        Action<ushort, BattleSnapshot>? previous = NativeBattleComponentSelectorCatalog.PreparedSource.Value;
        BattleSnapshot? prepared = null;
        try
        {
            NativeBattleComponentSelectorCatalog.PreparedSource.Value = (selected, source) =>
            {
                if (selected != effect || prepared is not null)
                {
                    throw new InvalidDataException("The selected factory must publish exactly one matching component source.");
                }
                prepared = source;
            };
            NativeBattleComponentSourceAvailabilityEntry availability = NativeBattleComponentSourceAvailability.Get(effect);
            NativeBattleComponentSelector? selector = entry.ExistingSelectors.FirstOrDefault(item => item.SupportsQueuedSourceHandoff && item.TestClass == availability.Provider && item.TestMethod == availability.Method);
            if (selector is not null)
            {
                Set(selector.EnvironmentPrefix + "_EFFECT", effect.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Set(selector.EnvironmentPrefix + "_EVIDENCE", evidence);
                Type type = typeof(NativeBattleComponentSourceTests).Assembly.GetType("Memoria.FF8.App.Tests." + selector.TestClass, true)!;
                MethodInfo method = type.GetMethod(selector.TestMethod, BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidDataException("The indexed component selector is unavailable.");
                Invoke(method, Activator.CreateInstance(type), []);
            }
            else
            {
                BattleSnapshot? source = null;
                foreach (string name in NativeBattleComponentSourceAvailability.FactoryTypes)
                {
                    Type? type = typeof(NativeBattleComponentSourceTests).Assembly.GetType("Memoria.FF8.App.Tests." + name);
                    MethodInfo? method = type?.GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, [typeof(ushort), typeof(string), typeof(string)]);
                    MethodInfo? supports = type?.GetMethod("Supports", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, [typeof(ushort)]);
                    if (method is not null && supports is not null && (bool)Invoke(supports, null, [effect])!)
                    {
                        source = (BattleSnapshot?)Invoke(method, null, [effect, game, evidence]);
                        if (source is not null)
                        {
                            break;
                        }
                    }
                }
                if (source is null)
                {
                    throw new InvalidDataException($"Effect {effect} has no registered fresh component source factory. Its existing prepared-source executor does not authorize constructing another header or carrier.");
                }
                NativeBattleComponentSelectorCatalog.PublishPreparedSource(effect, source);
            }
            return prepared ?? throw new InvalidDataException("The existing selector did not publish its actual prepared component source.");
        }
        finally
        {
            NativeBattleComponentSelectorCatalog.PreparedSource.Value = previous;
            foreach ((string name, string? value) in saved)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        void Set(string name, string value)
        {
            saved.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static void RunGraphicalComponent(string game, string request, EffectComponentPreviewSource source)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(request))!;
        LaunchOptions options = LaunchOptions.Parse(["--game-dir", game, "--effect-preview", request, "--resolution", "1920x1080", "--settings-dir", Path.Combine(directory, "settings"), "--control-dir", Path.Combine(directory, "control"), "--save-state", Path.Combine(directory, "saves", "checkpoint.json"), "--mute-audio"]);
        ExceptionDispatchInfo? failure = null;
        Thread render = new(() =>
        {
            try
            {
                GameSession session = GameWorld.CreateColdProcess(options.GameDirectory);
                using IDisposable? backgroundWindow = DiagnosticBackgroundWindow.FromEnvironment(options);
                using RecreateGame host = new(options, session, effectComponentSource: source);
                host.RunWithErrorCapture();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        if (OperatingSystem.IsWindows())
        {
            render.SetApartmentState(ApartmentState.STA);
        }
        render.Start();
        render.Join();
        failure?.Throw();
    }

    private static object? Invoke(MethodInfo method, object? instance, object?[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
