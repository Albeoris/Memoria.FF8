using System.Numerics;
using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDrawPointParticleTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1495, 1261, 24)]
    public void FieldLoadInitializesTrailsAfterTheSetupScriptWithoutClearingItsStatus(int previousX, int previousY, int previousZ)
    {
        NativeDrawPointParticleTables tables = Tables();
        GameSession session = new(new GameState());
        session.SetExamTimer(NativeExamTimerState.FromColdProcess());
        EventEngine engine = new(session) { DrawPointParticleTables = tables };
        engine.RestartNativeDrawPointPresentationForMigration();
        engine.LoadField(133, Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, previousX), (Jsm.Opcode.PSHN_L, previousY), (Jsm.Opcode.PSHN_L, previousZ), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.RET, 0)), []);
        engine.Step();
        engine.LoadField(134, Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1495), (Jsm.Opcode.PSHN_L, 1261), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.RET, 0)), []);
        Assert.All(engine.Scene.NativeDrawPointParticles!.Sparks, spark => Assert.Equal(0, spark.Status));
        engine.Step();
        Assert.Null(engine.LastFault);
        NativeDrawPointParticles particles = engine.Scene.NativeDrawPointParticles!;
        NativeSparkPoint anchor = new(1495, 1261, 24);
        Assert.Empty(particles.Drawings);
        Assert.All(particles.Sparks, spark =>
        {
            Assert.Equal(1, spark.Status);
            Assert.Equal(anchor, spark.Anchor);
            Assert.Equal(new NativeSparkPoint(1495, 1261, 152), spark.Target);
            Assert.Equal(88, spark.Control.Z);
            Assert.Equal(0, spark.Counter);
            Assert.All(spark.FirstEdge, point => Assert.Equal(anchor, point));
            Assert.All(spark.SecondEdge, point => Assert.Equal(anchor, point));
        });
        NativeDrawPointParticles restored = JsonSerializer.Deserialize<NativeDrawPointParticles>(JsonSerializer.Serialize(particles))!;
        Assert.Equal(JsonSerializer.Serialize(particles.Step(30, tables)), JsonSerializer.Serialize(restored.Step(30, tables)));
        engine.LoadField(135, Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        engine.Step();
        Assert.All(engine.Scene.NativeDrawPointParticles!.Sparks, spark => Assert.Equal(0, spark.Status));
        Assert.Empty(engine.Scene.NativeDrawPointParticles!.Step(30, tables).Drawings);
    }

    [Fact]
    public void SetupChangesOnlyAnchorsAndStatusWhileLaunchOwnsTheExactNativeFlight()
    {
        NativeDrawPointParticleTables tables = Tables();
        NativeDrawPointParticles particles = NativeDrawPointParticles.Initialize(null, 17, tables);
        NativeDrawPointParticles reset = particles.ResetAnchors(120, -300, 4);
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(particles.Sparks[index] with { Anchor = new NativeSparkPoint(120, -300, 4), Status = 1 }, reset.Sparks[index]);
        }
        NativeDrawPointParticles launched = reset.Launch(500, 600, 200, 100, 17, tables);
        for (int index = 0; index < 8; index++)
        {
            NativeDrawPointSpark spark = launched.Sparks[index];
            Assert.Equal(24 + index * 2, spark.Duration);
            Assert.Equal(NativeSparkPoint.From(120 + 4 * tables.Random(17 + index) - 512, -300 + 4 * tables.Random(25 + index) - 512, 1004 + index * 128), spark.Control);
            Assert.Equal(new NativeSparkPoint(500, 600, 380), spark.Target);
            Assert.Equal(164, spark.Angle);
            Assert.Equal(36, spark.TargetAngle);
        }
        for (int callback = 1; callback <= 43; callback++)
        {
            launched = launched.Step(unchecked((byte)(17 + callback * 2 * 2191)), tables);
            Assert.Equal(callback < 43, launched.AcquisitionActive);
        }
        Assert.Contains(launched.Sparks, spark => spark.Status == 1);
    }

    [Fact]
    public void CurrentDisplayWordControlsIdleSparksAndAcquisitionIgnoresThatWord()
    {
        NativeDrawPointParticleTables tables = Tables();
        NativeDrawPointParticles seed = NativeDrawPointParticles.Initialize(null, 5, tables).ResetAnchors(0, 0, 0);
        NativeDrawPointParticles particles = NativeDrawPointParticles.Initialize(seed, 5, tables).ResetAnchors(0, 0, 0);
        for (int callback = 0; callback < 12; callback++)
        {
            particles = particles.Step(unchecked((byte)(5 + callback * 30)), tables);
        }
        FieldAssets field = new(100, "declared-drawpoint-particle-component", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitZ * -1, Vector3.UnitY, new Vector3(0, 0, 512), Vector2.Zero, 512), new FieldWalkmesh([]), []);
        FieldRenderer renderer = new(field);
        byte[] Draw(NativeDrawPointParticles current, ushort? mask) => renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, drawPointParticles: current, drawPointParticleTables: tables, drawPointDisplayMask: mask).ToArray();
        byte[] empty = Draw(particles, 0);
        byte[] visible = Draw(particles, 1);
        Assert.NotEqual(empty, visible);
        Assert.Equal(empty, Draw(particles, 255));
        Assert.Throws<NotSupportedException>(() => Draw(particles, null));
        NativeDrawPointParticles flight = particles.Launch(60, 10, 20, 64, 17, tables);
        for (int callback = 0; callback < 8; callback++)
        {
            flight = flight.Step((byte)callback, tables);
        }
        Assert.Equal(Draw(flight, 1), Draw(flight, 0));
        Assert.Equal(Draw(flight, 1), Draw(flight, null));
        string saved = JsonSerializer.Serialize(flight);
        NativeDrawPointParticles restored = JsonSerializer.Deserialize<NativeDrawPointParticles>(saved)!;
        Assert.Equal(Draw(flight, 1), Draw(restored, 1));
        Assert.Equal(JsonSerializer.Serialize(flight.Step(200, tables)), JsonSerializer.Serialize(restored.Step(200, tables)));
    }

    [Fact]
    public void ParticlePhaseUsesThePreservedProcessFractionAndRefusesUnknownHistory()
    {
        NativeExamTimerState clock = NativeExamTimerState.FromColdProcess().AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 71);
        Assert.Equal(unchecked((byte)(71 * 2 * 2191)), NativeDrawPointParticles.Phase(clock));
        Assert.Throws<NotSupportedException>(() => NativeDrawPointParticles.Phase(NativeExamTimerState.FromReachedSet(0)));
        Assert.Equal("JThunder", DialogueTextFormatter.Format("{Key 65}Thunder"));
    }

    [Fact]
    public void MissingActiveOwnerAndUnknownClockRefuseBeforeChangingTheCallback()
    {
        NativeDrawPointParticleTables tables = Tables();
        GameSession session = new(new GameState());
        EventEngine engine = new(session);
        engine.LoadField(148, Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 120), (Jsm.Opcode.PSHN_L, -300), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETDRAWPOINT, 0), (Jsm.Opcode.RET, 0)), []);
        engine.Step();
        Assert.Equal(1, engine.Scene.DrawPointEnabled);
        Assert.Null(engine.Scene.NativeDrawPointParticles);
        engine.DrawPointParticleTables = tables;
        JsonElement before = engine.Capture();
        Assert.Throws<NotSupportedException>(engine.Step);
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));

        GameSession unknownSession = new(new GameState());
        unknownSession.SetExamTimer(NativeExamTimerState.FromColdProcess());
        EventEngine unknown = new(unknownSession);
        unknown.LoadField(148, Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        unknown.DrawPointParticleTables = tables;
        unknown.RestartNativeDrawPointPresentationForMigration();
        unknownSession.SetExamTimer(NativeExamTimerState.FromReachedSet(0));
        before = unknown.Capture();
        Assert.Throws<NotSupportedException>(unknown.Step);
        Assert.True(JsonElement.DeepEquals(before, unknown.Capture()));
    }

    [Fact]
    public void ActualBalambAssetsPlaceVisibleSparksAtTheirOriginalScriptAnchor()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original field assets are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(102);
        Assert.Equal("bccent_1", field.Name);
        JsmProgram script = JsmProgram.Read(field.Jsm);
        Assert.Equal(Jsm.Opcode.SET3, script.Operations[2214].Opcode);
        Assert.Equal(55, script.Operations[2214].Parameter);
        Assert.Equal(new[] { -485, -698, 0 }, script.Operations.Skip(2211).Take(3).Select(operation => operation.Parameter));
        Assert.Equal(Jsm.Opcode.SETDRAWPOINT, script.Operations[2216].Opcode);
        Assert.Equal(1, script.Operations[2215].Parameter);
        Assert.Equal(7, script.Operations[2217].Parameter);
        NativeDrawPointParticleTables tables = Tables();
        NativeDrawPointParticles anchorOwner = NativeDrawPointParticles.Initialize(null, 0, tables).ResetAnchors(-485, -698, 0);
        NativeDrawPointParticles particles = NativeDrawPointParticles.Initialize(anchorOwner, 0, tables).ResetAnchors(-485, -698, 0);
        for (int callback = 1; callback <= 20; callback++)
        {
            particles = particles.Step(unchecked((byte)(callback * 2 * 2191)), tables);
        }
        FieldRenderer renderer = new(field);
        Dictionary<int, int> backgrounds = field.Background.Select(tile => tile.AnimationId).Distinct().ToDictionary(id => id, _ => 0);
        FieldNativeCameraResult camera = FieldNativeCameraProbe.Resolve(field.Camera, field.Information!, -485 << 12, -698 << 12, 0);
        Assert.True(camera.Supported, camera.UnsupportedReason);
        FieldNativeCameraCenter center = camera.Center!.Value;
        Vector2 actorOffset = new(-center.X, -center.Y);
        Dictionary<int, Vector2> layers = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => actorOffset + (layer > 0 ? field.Camera.Pan : Vector2.Zero));
        FieldNativeBackgroundWrapFrame? wrap = field.Information!.BackgroundWrapMask != 0 ? FieldNativeNormalBackgroundWrap.Prepare(field, 0, center) : null;
        byte[] plain = renderer.Render(backgrounds, [], TimeSpan.Zero, backgroundScrollOffsets: layers, actorScrollOffset: actorOffset, nativeWrapFrame: wrap, drawPointParticles: particles, drawPointParticleTables: tables, drawPointDisplayMask: 0).ToArray();
        byte[] visible = renderer.Render(backgrounds, [], TimeSpan.Zero, backgroundScrollOffsets: layers, actorScrollOffset: actorOffset, nativeWrapFrame: wrap, drawPointParticles: particles, drawPointParticleTables: tables, drawPointDisplayMask: 1).ToArray();
        Assert.NotEqual(plain, visible);
        int changedPixels = Enumerable.Range(0, plain.Length / 4).Count(index => !plain.AsSpan(index * 4, 4).SequenceEqual(visible.AsSpan(index * 4, 4)));
        Assert.True(changedPixels > 4, $"Native anchor produced only{changedPixels} changed pixels.");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_DRAWPOINT_PARTICLE_EVIDENCE");
        if (evidence is not null)
        {
            WritePng(TestResources.ArtifactFile("App.Tests.NativeDrawPointParticleTests.1.png"), visible);
            WritePng(TestResources.ArtifactFile("App.Tests.NativeDrawPointParticleTests.2.png"), plain);
            File.WriteAllText(TestResources.ArtifactFile("App.Tests.NativeDrawPointParticleTests.3.json"), JsonSerializer.Serialize(new { FieldId = 102, field.Name, ObjectId = 15, ScriptId = 135, Set3Operation = 2214, SetDrawPointOperation = 2216, RawAnchor = new[] { -485, -698, 0 }, Triangle = 55, DisplayMask = 1, PointIndex = 6, CameraIndex = 0, CameraTarget = "Original draw-point anchor with native INF camera height/projection/clamp", CameraCenter = center, BackgroundAnimationStates = backgrounds, ComponentPhaseAtStart = 0, ComponentCallbacks = 20, ChangedPixels = changedPixels, Classification = "Declared initialized renderer component; original assets and script anchor; no gameplay save or travel assertion" }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void WritePng(string path, byte[] rgba)
    {
        using SkiaSharp.SKBitmap bitmap = new(320, 240);
        for (int y = 0; y < 240; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, SkiaSharp.SKColor.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        bitmap.SavePng(path);
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }

    private static NativeDrawPointParticleTables Tables()
    {
        string executable = Path.Combine(TestResources.GameDirectory, "FF8_EN.exe");
        Assert.SkipUnless(File.Exists(executable), "Original executable tables are required.");
        return NativeDrawPointParticleTableReader.Read(File.ReadAllBytes(executable));
    }
}
