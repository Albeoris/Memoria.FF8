using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentSourceTests
{
    private static readonly JsonSerializerOptions RequestJson = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void Component_preparation_reads_the_graphical_request_transport(ushort effect)
    {
        string json = $$"""{"effect_id":{{effect}},"enemy_id":0,"ally_id":0,"duration_seconds":300}""";
        EffectPreviewRequest request = ReadRequest(json);
        request.Validate();
        Assert.Equal(effect, request.EffectId);
        Assert.Equal(0, request.EnemyId);
        Assert.Equal((byte)0, request.AllyId);
        Assert.Equal(300d, request.DurationSeconds);
        Assert.Throws<JsonException>(() => ReadRequest(json.Replace("duration_seconds", "unknown_setting", StringComparison.Ordinal)));
    }

    private static EffectPreviewRequest ReadRequest(string json)
    {
        return JsonSerializer.Deserialize<EffectPreviewRequest>(json, RequestJson) ?? throw new InvalidDataException("The component arena request is empty.");
    }

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
            : ReadRequest(File.ReadAllText(requestPath));
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
                Assert.Null(typeof(RecreateGame).GetField("error", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(host));
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
