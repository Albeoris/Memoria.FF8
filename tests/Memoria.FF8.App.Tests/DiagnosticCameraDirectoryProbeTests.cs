using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticCameraDirectoryProbeTests
{
    [Fact]
    public void External_recipe_preserves_actual_camera_selector_and_directory_at_first_invocation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAMERA_PROBE_RECIPE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAMERA_PROBE_OUTPUT");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath) && output is not null, "Select installed resources, an external camera diagnostic recipe and an exclusive facts output path.");
        Assert.True(Path.IsPathFullyQualified(output!));
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipePath!), options)!;
        request.Validate();
        DiagnosticBattleDriverRecipe driver = Assert.IsType<DiagnosticBattleDriverRecipe>(request.Driver);
        Assert.Equal((ushort)338, driver.ExpectedEffect);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        battle.DiagnosticEffectDispatched += dispatches.Add;
        string stage = "entry";
        object? preceding = null;
        Exception? fault = null;
        bool completed = false;
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            stage = "readiness";
            for (int callback = 0; callback < 4096 && !Ready(); callback++)
            {
                Tick();
            }
            if (!Ready())
            {
                throw new InvalidOperationException("The original command did not reach a drained ready boundary within4096 ticks.");
            }
            stage = "baseline";
            for (int frame = 0; frame < Math.Ceiling(driver.BaselineSeconds * 60); frame++)
            {
                Tick();
            }
            if (!Ready())
            {
                throw new InvalidOperationException("Baseline changed the original command readiness.");
            }
            stage = "invocation";
            world.ApplyBattleInput(driver.Input);
            for (int callback = 0; callback < 4096; callback++)
            {
                Tick();
                if (dispatches.Any(value => value.ActorId == driver.Input.ActorId && value.RequestedEffect == driver.ExpectedEffect) && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    completed = true;
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or IOException or ArgumentException)
        {
            fault = exception;
        }
        object facts = new { Scope = "Bounded installed-resource logical invocation; no graphical-host acceptance or state mutation beyond the supplied recipe.", Stage = stage, Completed = completed, Fault = fault?.ToString(), Preceding = preceding, Current = Observe(), Dispatches = dispatches };
        Directory.CreateDirectory(Path.GetDirectoryName(output!)!);
        using (FileStream file = new(output!, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            JsonSerializer.Serialize(file, facts, options);
        }
        Assert.True(fault is null && completed, JsonSerializer.Serialize(facts, options));

        bool Ready()
        {
            return battle.Outcome == BattleOutcome.Active && battle.NativeEntryReady && battle.CanChoosePlayerCommand(driver.Input.ActorId!) && battle.ObserveDiagnosticDrainBlockers().Count == 0 && world.VerifyDiagnosticTarget() is not null;
        }

        void Tick()
        {
            preceding = Observe();
            battle.Tick();
            audio.Update(battle);
        }

        object Observe()
        {
            NativeBattleGraphicsState? graphics = battle.NativeGraphics;
            NativeBattleCameraState? camera = battle.NativeCamera;
            NativeBattleEffectAnimationRegisters? resource = camera?.ControlResource;
            int? selector = graphics?.ActionContext?.TargetIndex is byte target ? graphics.Models[target].SequenceByte145 : null;
            object? track = null;
            if (resource is not null && selector is int selected)
            {
                int bank = resource.ModelTableOffset;
                int group = selected >> 4 & 15;
                int? groups = Word(bank);
                int? directoryOffset = groups is int count && group < count ? Word(bank + 2 + 2 * group) : null;
                int? relativeWord = directoryOffset is int relative ? Word(bank + relative + 2 * (selected & 7)) : null;
                int? trackOffset = directoryOffset is int directoryStart && relativeWord is int word ? bank + directoryStart + 2 * unchecked((short)word) : null;
                track = new { Selector = selected, Group = group, Channel = selected & 7, Groups = groups, DirectoryRelative = directoryOffset, TrackRelative = relativeWord is int signed ? (short?)unchecked((short)signed) : null, SourceTrackOffset = trackOffset, TrackWord = trackOffset is int offset ? Word(offset) : null };
            }
            return new
            {
                battle.Frame,
                battle.NativeHostCallbacks,
                battle.Outcome,
                Camera = camera is null ? null : new { camera.ControlCursor, camera.Variables, camera.Mask, camera.ForceCamera, Resource = resource is null ? null : new { resource.Source.Identity, Length = resource.Source.Data.Length, resource.BaseOffset, resource.StreamOffset, resource.ModelTableOffset, ControlPrefix = Convert.ToHexString(resource.Source.Data.AsSpan().Slice(resource.StreamOffset, Math.Min(8, resource.Source.Data.Length - resource.StreamOffset))) }, Track = track },
                Action = graphics?.ActionContext is { } action ? new { action.ActorIndex, action.TargetIndex, action.SourceTargetMask, action.PhysicalTargetMask } : null,
                Models = graphics?.Models.Select(model => new { model.Index, model.ModelId4, model.Flags0, model.SequenceByte145 }).ToArray(),
                Effect = graphics?.OriginalEffect is { } effect ? new { effect.RequestedEffect, effect.SelectedIndex } : null
            };

            int? Word(int offset)
            {
                return resource is not null && offset >= 0 && offset <= resource.Source.Data.Length - 2 ? BinaryPrimitives.ReadUInt16LittleEndian(resource.Source.Data.AsSpan().Slice(offset, 2)) : null;
            }
        }
    }
}
