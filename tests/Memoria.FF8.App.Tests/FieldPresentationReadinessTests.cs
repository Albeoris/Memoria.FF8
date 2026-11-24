using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldPresentationReadinessTests
{
    [Fact]
    public void DefeatFieldWaitsForItsFirstBlackDrawAndRunsItsAuthoredFadeIn()
    {
        string directory = GameDirectory();
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        world.EnsureField();

        // Start at the presentation boundary with a declared, already settled defeat.
        string sourceHash = Convert.ToHexString(SHA256.HashData(world.Field!.Jsm));
        BattleEncounterOccurrence encounter = new("presentation-defeat", 94, world.BattleResources.FormationSourceHash(94), SceneKind.Field, world.Field.Id, world.Events.Frame, sourceHash, null);
        long clock = session.ExamTimer!.ClockCalls;
        NativeBattleAbandonment defeat = new(encounter, new(0), clock, clock, 0);
        session.RequestField(75, 0);
        session.Restore(session.State with { FieldLoadContext = NativeFieldLoadContext.FromBattleDefeat(defeat.Branch), AbandonedNativeBattles = [defeat], AppliedBattleResultIds = [encounter.OccurrenceId] });
        world.EnsureField();
        Assert.False(world.CanPresentCurrentField);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.False(world.CanPresentCurrentField);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.CanPresentCurrentField);
        Assert.Equal(NativeFadeDrawMode.Black, world.Events.FieldFade!.Draw.Mode);
        Assert.False(HasVisiblePixels(world.Render(TimeSpan.Zero)));

        for (int callback = 0; callback < 32 && world.Events.FieldFade!.Draw.Mode == NativeFadeDrawMode.Black; callback++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(world.Events.LastFault);
        Assert.True(world.CanPresentCurrentField);
        Assert.Equal(NativeFadeDrawMode.Subtract, world.Events.FieldFade!.Draw.Mode);
        Assert.Equal((short)2, world.Events.FieldFade.Physical.Mode);
        Assert.Equal((short)239, world.Events.FieldFade.Physical.Counter);

        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 16)).Accepted);
        Assert.Equal(NativeFadeDrawMode.None, world.Events.FieldFade.Draw.Mode);
        Assert.True(HasVisiblePixels(world.Render(TimeSpan.Zero)));

        static bool HasVisiblePixels(byte[] pixels)
        {
            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                if ((pixels[offset] | pixels[offset + 1] | pixels[offset + 2]) != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    [Fact]
    public void ColdProcessHasOneKnownOutputBeforeAnyFieldCallback()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeFieldShake cold = session.FieldShake!;
        Assert.True(cold.IsColdProcess);
        Assert.Equal((byte)0, cold.X.Cursor);
        Assert.Equal((byte)0, cold.Y.Cursor);
        Assert.Equal((sbyte)0, cold.X.Output);
        Assert.Equal((sbyte)0, cold.Y.Output);
        session.StartNewGame();
        Assert.Equal(cold, session.FieldShake);
        Assert.Equal(cold, JsonSerializer.Deserialize<NativeFieldShake>(JsonSerializer.Serialize(cold)));
    }

    [Fact]
    public void NormalFieldLoadRetainsExecutedShakeHistoryAndItsCurrentCheckpointOwner()
    {
        string directory = GameDirectory();
        using var assets = new GameAssets(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.Restore(session.State with { FieldId = 600, FieldName = "declared-shake-component" });
        var engine = new EventEngine(session) { ShakeTables = assets.ReadNativeShakeTables() };
        // This declared native component executes the producer; no original field
        // visit, guessed process phase or reconstructable journey is claimed.
        engine.LoadField(600, Program([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        engine.Step();
        NativeFieldShake history = session.FieldShake!;
        Assert.Equal((byte)1, history.X.Cursor);
        Assert.Equal((byte)1, history.Y.Cursor);
        Assert.True(history.X.Enabled);
        engine.LoadField(601, Program([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        NativeFieldShake reset = session.FieldShake!;
        Assert.Equal(history.ResetForLoad(), reset);
        Assert.Equal(history.X.Cursor, reset.X.Cursor);
        Assert.Equal(history.X.Output, reset.X.Output);
        Assert.Equal(history.X.Amplitude, reset.X.Amplitude);
        Assert.False(reset.X.Enabled);
        Assert.Equal((short)0, reset.X.Counter);
        Assert.Equal(reset, engine.FieldShake);
        session.Restore(session.State with { FieldId = 601, EventState = engine.Capture() });
        string path = Path.Combine(Path.GetTempPath(), "ff8-shake-owner-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            Assert.Equal(reset, GameSession.Load(path).FieldShake);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FreshOpeningWaitsForInitializationAndRenderingNeverChoosesOrAdvancesShakeHistory()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, GameDirectory());
        world.EnsureField();
        Assert.Equal(NativeFieldShake.FromColdProcess(), world.Events.FieldShake);
        for (int callback = 0; callback < 60; callback++)
        {
            if (session.State.Scene == SceneKind.Field && !FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization))
            {
                JsonElement vm = world.Events.Capture();
                NativeFieldShake owner = session.FieldShake!;
                Assert.NotEmpty(world.Render(TimeSpan.Zero));
                Assert.Equal(owner, session.FieldShake);
                Assert.True(JsonElement.DeepEquals(vm, world.Events.Capture()));
                Assert.Null(world.Events.LastFault);
                return;
            }
            Assert.True(world.Apply(new PlayerAction(session.IsMoviePlaying ? PlayerActionKind.SkipMovie : PlayerActionKind.Wait)).Accepted);
            world.EnsureField();
        }
        Assert.Fail("Fresh opening did not finish native field initialization within the ordinary callback budget.");
    }

    [Fact]
    public void FixedViewportRequiresTheActualCompletedProofAndExactNativeClamps()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, GameDirectory());
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        world.EnsureField();
        FieldAssets field = world.Field!;
        SceneEventState scene = world.Events.Scene;
        Assert.Equal(74, field.Id);
        Assert.Equal(0, scene.NativeFieldLoadVersion);
        Assert.NotNull(FixedViewportCameraCertificate.Prepare(field, scene));

        // These detached certificate inputs do not alter the actual session or its history.
        JsonNode wrongProof = JsonNode.Parse(JsonSerializer.Serialize(scene))!;
        wrongProof["NativeFieldInitialization"]!["ProgramHash"] = new string('0', 64);
        Assert.Null(FixedViewportCameraCertificate.Prepare(field, wrongProof.Deserialize<SceneEventState>()!));
        JsonNode pendingProof = JsonNode.Parse(JsonSerializer.Serialize(scene))!;
        pendingProof["NativeFieldInitialization"]!["Stage"] = (int)NativeFieldInitializationStage.Initializing;
        Assert.Null(FixedViewportCameraCertificate.Prepare(field, pendingProof.Deserialize<SceneEventState>()!));
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[0] = bounds[0] with { Left = -161 };
        Assert.Null(FixedViewportCameraCertificate.Prepare(field with { Information = field.Information with { CameraBounds = bounds } }, scene));
        Assert.NotNull(FixedViewportCameraCertificate.Prepare(field, scene));
    }

    private static byte[] Program((Jsm.Opcode Opcode, int Operand)[] operations)
    {
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int operand) = operations[index];
            int encoded = opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | operand & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field/shake corpus is required.");
        return directory;
    }
}
