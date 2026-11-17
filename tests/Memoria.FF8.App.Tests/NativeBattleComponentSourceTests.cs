using System.Security.Cryptography;
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
        string? requestPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_PREVIEW_PATH");
        EffectPreviewRequest request = requestPath is null
            ? new(1, 0, effect)
            : JsonSerializer.Deserialize<EffectPreviewRequest>(File.ReadAllText(requestPath)) ?? throw new InvalidDataException("The component arena request is empty.");
        if (request.EffectId != effect)
        {
            throw new InvalidDataException("The selected effect and preview arena request differ.");
        }
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = world.StartEffectPreview(request);
        source.PublishDiagnosticEffectPreview(effect);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        BattleDefinition definition = source.ReadDefinition();
        string boundary = admission.ModuleLoaded ? "module-loaded-before-entry" : "queued-action104";
        var inputs = new
        {
            Effect = effect,
            Executable = definition.NativeRetainedMemory?.ColdExecutableSha256,
            definition.EncounterId,
            definition.StageId,
            Combatants = definition.Combatants.Select(actor => new { actor.Id, actor.ModelId, actor.SceneSlot, actor.IsPlayer }).ToArray(),
            admission.Command,
            admission.Action,
            Header = admission.HeaderIndex,
            source.Frame,
            NativeCallback = admission.Callback
        };
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(inputs)));
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        using (FileStream receipt = new(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            JsonSerializer.Serialize(receipt, new { Phase = "prepared", Boundary = boundary, Scope = "native-effect-component", Effect = effect, InputsSha256 = hash, Inputs = inputs, OrdinaryAdmissionCertified = false, FunctionalCompletionCertified = false });
        }
        if (requestPath is not null)
        {
            RunGraphicalComponent(game!, requestPath, new(source, hash));
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
}
