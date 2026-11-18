using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
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
            ? CreateComponentRequest(effect)
            : ReadRequest(File.ReadAllText(requestPath));
        if (request.EffectId != effect)
        {
            throw new InvalidDataException("The selected effect and preview arena request differ.");
        }
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = PrepareComponentSource(world, request);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        BattleDefinition definition = source.ReadDefinition();
        NativeBattleActionHeaderSlot header = source.Presentation!.ActionPackets.ReadHeader(new(admission.HeaderIndex));
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
            header.Sequence,
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

    internal static int OriginalComponentEnemy(ushort effect)
    {
        return effect switch
        {
            17 => 91,
            20 => 68,
            44 => 83,
            64 => 124,
            66 => 124,
            167 => 70,
            308 => 105,
            314 => 103,
            315 => 75,
            318 => 38,
            _ => 0
        };
    }

    internal static EffectPreviewRequest CreateComponentRequest(ushort effect)
    {
        return new(OriginalComponentEnemy(effect), 0, effect) { StageId = effect is 64 or 66 ? 142 : 0 };
    }

    internal static BattleSession PrepareComponentSource(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("A component source requires its selected effect.");
        int enemy = OriginalComponentEnemy(effect);
        if (enemy != 0 && request.EnemyId != enemy)
        {
            throw new InvalidDataException("The selected component requires its original caster model.");
        }
        if (effect == 17)
        {
            return PrepareOriginalLandingComponent(world);
        }
        if (effect == 20)
        {
            return PrepareOriginalEffect20Component(world);
        }
        if (effect == 44)
        {
            return PrepareOriginalEffect44Component(world, request);
        }
        if (effect == 64)
        {
            return PrepareOriginalEffect64Component(world, request);
        }
        if (effect == 66)
        {
            return PrepareOriginalEffect66Component(world, request);
        }
        if (effect == 308)
        {
            return PrepareOriginalEffect308Component(world);
        }
        if (effect is 314 or 315)
        {
            return PrepareOriginalCompanionContext(world, effect);
        }
        BattleSession source = world.StartEffectPreview(request);
        if (enemy == 0)
        {
            source.PublishDiagnosticEffectPreview(effect);
            return source;
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidOperationException("Component preparation requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The original component caster is absent.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient is absent.");
        (byte command, ushort action, byte sequence) = effect switch
        {
            167 => ((byte)8, (ushort)274, (byte)12),
            318 => ((byte)8, (ushort)278, (byte)13),
            _ => throw new InvalidOperationException("The original component source is unsupported.")
        };
        // These carriers retain the selected original caster and sequence without evaluating an AI turn.
        NativeBattleActionHeaderPreparation header = new(caster, command, sequence, -1, action, effect, null);
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, 0, 0, 0), null);
        process.BeginDiagnosticAction(header, [contact]);
        return source;
    }

    private static BattleSession PrepareOriginalLandingComponent(GameWorld world)
    {
        BattleSession source = world.StartDiagnosticBattle(new(29, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 1, 5] }
        });
        BattleCombatant biggs = source.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 74);
        int attacks = 0;
        for (int callback = 0; callback < 18000 && source.Outcome == BattleOutcome.Active; callback++)
        {
            BattlePresentationProcess process = source.Presentation!;
            if (process.ActiveActionToken is not null && process.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Effect == 17))
            {
                _ = GameWorld.ValidateEffectComponentSource(source, 17);
                return source;
            }
            if (source.NativeEntryReady && source.ActiveAction is null && source.QueuedActions.Count == 0 && biggs.IsActive && biggs.IsAlive && !biggs.Untargetable && !biggs.Suspended)
            {
                BattleCombatant? ready = source.Combatants.FirstOrDefault(actor => actor.IsPlayer && source.CanChoosePlayerCommand(actor.Id));
                if (ready is not null)
                {
                    if (++attacks > 256)
                    {
                        throw new InvalidDataException("The original landing source exceeded its bounded ordinary attack preparation.");
                    }
                    source.QueueAttack(ready.Id, biggs.Id);
                }
            }
            source.Tick();
        }
        throw new InvalidDataException("The original encounter did not publish its unstarted landing component within the preparation budget.");
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
