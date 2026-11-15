using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterCameraPatchTests
{
    [Theory]
    [InlineData(-45)]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(45)]
    public void CameraElevationCompilesAndSurvivesSnapshotSerialization(double degrees)
    {
        EncounterPatchComposition composition = Compose(Layer("camera", Edit("raise", "0", degrees.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Equal(degrees, composition.CameraProperties["elevationDegrees"]);
        EncounterPatchOrigin origin = Assert.Single(composition.Origins);
        Assert.Equal("camera", origin.Target);
        Assert.Equal("elevationDegrees", origin.Property);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, new() { Combatants = [new() { Id = "original", Name = "Original" }] }, new("fixture.encounter", "original", 11, 11, [], true));
        Assert.Equal(degrees, compiled.Camera!.ElevationDegrees);
        Assert.Equal(degrees, JsonSerializer.Deserialize<EncounterCompiledDefinition>(JsonSerializer.Serialize(compiled))!.Camera!.ElevationDegrees);
    }

    [Fact]
    public void MissingCameraPropertyPreservesDefaultForOlderSnapshots()
    {
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), []);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, new() { Combatants = [new() { Id = "original", Name = "Original" }] }, new("fixture.encounter", "original", 11, 11, [], true));
        Assert.Empty(composition.CameraProperties);
        Assert.Null(compiled.Camera);
        Assert.Null(JsonSerializer.Deserialize<EncounterCompiledDefinition>(JsonSerializer.Serialize(compiled))!.Camera);
    }

    [Fact]
    public void CompletedEffectRestoresAuthoredCameraWhileRetainingItsLoadedResource()
    {
        EncounterPatchComposition composition = Compose(Layer("camera", Edit("raise", "0", "12")));
        BattleDefinition definition = new() { Name = "Camera completion", Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "original", Name = "Original", MaximumHp = 100, CurrentHp = 100 }] };
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("fixture.encounter", "original", 11, 11, [], true));
        BattleSession session = new(definition with { EncounterMod = compiled }, BattleAtbMode.TurnBased);
        NativeBattleCameraPose source = new(new(3000, -1600, 6000), new(0, -500, -2000), 512, 0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, NativeBattleCameraState.FromColdProcess() with { CurrentPose = source });
        NativeBattleGraphicsState graphics = new(new BattleActorModels([]), NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, OriginalEffects()) { OriginalEffect = new(1, 0, new(1, 0x8D6A00, 0), null, null) };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
        System.Reflection.PropertyInfo presentation = typeof(BattleSession).GetProperty("EncounterPresentationCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        NativeBattleEffectMemory active = NativeBattleEffectMemory.FromColdProcess() with { Active = 1 };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, active);
        Assert.Equal(source, presentation.GetValue(session));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, session.NativeCamera! with { CapturedPose = source });
        Assert.Equal(EncounterCameraPresentation.Elevate(source, 12), presentation.GetValue(session));
        NativeBattleEffectMemory completed = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(active with { Active = 0 }));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, completed);
        Assert.Equal(EncounterCameraPresentation.Elevate(source, 12), presentation.GetValue(session));
        Assert.Same(graphics.OriginalEffect, session.NativeGraphics!.OriginalEffect);
        Assert.Equal(source, session.NativeCamera!.CurrentPose);
    }

    [Fact]
    public void Hosted_framing_includes_high_source_and_target_without_reading_unselected_low_geometry()
    {
        BattleModelRoster roster = CameraRoster(25, 0, 16, 24);
        ImmutableArray<NativeBattleGraphicsModel> models = CameraModels(25).SetItem(0, new(2))
            .SetItem(16, CameraModel(16, -6500)).SetItem(24, CameraModel(24, 7400));
        NativeBattleEffectParameters parameters = new(16, 0, 0, new BattleActionHeaderReference(0), 0, 0) { PresentationTargets = default(BattleModelTargets).Add(24) };
        BattleSession session = CreateFramingSession(models, parameters, roster);
        NativeBattleCameraPose framed = PrepareCamera(session);
        Assert.Equal(ExpectedFraming(session.NativeCamera!, -6500, 7400), framed);

        // Body geometry for the unselected loaded row is deliberately absent.
        BattleSession withoutFarTarget = CreateFramingSession(models.SetItem(24, CameraModel(24, 0)), parameters, roster);
        Assert.NotEqual(framed.Eye, PrepareCamera(withoutFarTarget).Eye);

        BattleSession withWiderSource = CreateFramingSession(models.SetItem(16, CameraModel(16, -8500)), parameters, roster);
        NativeBattleCameraPose framedBySource = PrepareCamera(withWiderSource);
        Assert.Equal(ExpectedFraming(withWiderSource.NativeCamera!, -8500, 7400), framedBySource);
        BattleSession withoutFarSource = CreateFramingSession(models.SetItem(16, CameraModel(16, 0)), parameters, roster);
        Assert.NotEqual(framedBySource.Eye, PrepareCamera(withoutFarSource).Eye);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    public void Hosted_framing_rejects_unbound_or_out_of_bank_rows_before_reading_model_geometry(int target)
    {
        BattleModelRoster roster = CameraRoster(20, 0, 18);
        NativeBattleEffectParameters parameters = new(18, 0, 0, new BattleActionHeaderReference(0), 0, 0) { PresentationTargets = default(BattleModelTargets).Add(target) };
        BattleSession session = CreateFramingSession(CameraModels(20).SetItem(18, new(2)), parameters, roster);
        NativeBattleCameraState? beforeCamera = session.NativeCamera;
        NativeBattleGraphicsState? beforeGraphics = session.NativeGraphics;
        NativeBattleEffectMemory? beforeMemory = session.NativeEffectMemory;
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => PrepareCamera(session));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(beforeCamera, session.NativeCamera);
        Assert.Same(beforeGraphics, session.NativeGraphics);
        Assert.Same(beforeMemory, session.NativeEffectMemory);
    }

    [Fact]
    public void Native_seven_row_framing_preserves_source_and_raw_mask_projection()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = CameraModels(7).SetItem(1, CameraModel(1, -6500)).SetItem(5, CameraModel(5, 7400)).SetItem(6, new(0));
        NativeBattleEffectParameters parameters = new(OriginalOwner(1), 0, (1 << 5) | (1 << 6), new BattleActionHeaderReference(0), 0, 0);
        BattleSession session = CreateFramingSession(models, parameters);
        NativeBattleCameraState camera = session.NativeCamera!;
        NativeBattleCameraPose expected = ExpectedFraming(camera, -6500, 7400);
        NativeBattleCameraPose actual = PrepareCamera(session);
        Assert.Equal(expected, actual);
        Assert.Equal(JsonSerializer.Serialize(camera.PublishProjection(4, presentationPose: expected).Projection), JsonSerializer.Serialize(camera.PublishProjection(4, presentationPose: actual).Projection));
    }

    private static BattleSession CreateFramingSession(ImmutableArray<NativeBattleGraphicsModel> models, NativeBattleEffectParameters parameters, BattleModelRoster? roster = null)
    {
        EncounterPatchComposition composition = Compose(Layer("camera", Edit("raise", "0", "12")));
        BattleDefinition definition = new() { Name = "Camera framing", Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true }, new() { Id = "original", Name = "Original" }] };
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("fixture.encounter", "original", 11, 11, [], true));
        BattleSession session = new(definition with { EncounterMod = compiled }, BattleAtbMode.TurnBased);
        NativeBattleCameraPose pose = new(new(0, -500, 5000), new(0, -500, 0), 512, 0);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = pose, FocalLength = 512, Projection = new(null, 512, 160, 108) };
        NativeBattleGraphicsState graphics = new(models, roster?.Rows.Select(row => row?.ActorId).ToImmutableArray() ?? ImmutableArray.CreateRange(new string?[models.Length]), 0, 0)
        {
            PresentationRoster = roster,
            OriginalEffect = new(1, 0, new(1, 0x8D6A00, 0), parameters, 0)
        };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, camera);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess() with { Active = 1 });
        return session;
    }

    private static NativeBattleCameraPose PrepareCamera(BattleSession session)
    {
        return Assert.IsType<NativeBattleCameraPose>(typeof(BattleSession).GetMethod("PrepareEncounterCamera", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [session.NativeCamera]));
    }

    private static BattleModelRoster CameraRoster(int count, params int[] bound)
    {
        return new(Enumerable.Range(0, count).Select(row => bound.Contains(row) ? new BattleModelOwner($"actor-{row}", row == 16) : null).ToImmutableArray());
    }

    private static ImmutableArray<NativeBattleGraphicsModel> CameraModels(int count)
    {
        return Enumerable.Range(0, count).Select(row => new NativeBattleGraphicsModel(0)).ToImmutableArray();
    }

    private static NativeBattleGraphicsModel CameraModel(int row, int x)
    {
        return new(2) { GeometryBounds52 = new(new(0, 0, 0), new(0, 0, 0)), GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, -500, 0, 4096) };
    }

    private static NativeBattleCameraPose ExpectedFraming(NativeBattleCameraState camera, short sourceX, short targetX)
    {
        return EncounterCameraPresentation.FrameTargets(camera.CurrentPose!, [new(sourceX, -500, 0), new(targetX, -500, 0)], camera.Projection!.FocalLength!.Value, camera.Projection.OffsetX!.Value, camera.Projection.OffsetY!.Value);
    }

    [Fact]
    public void GuardedCameraRefinementRequiresActualPreviousWriter()
    {
        EncounterPatchLayer first = Layer("first", Edit("raise", "0", "12"));
        EncounterPatchLayer second = Layer("second", Edit("refine", "12", "18", "first", "raise"));
        EncounterPatchLayer third = Layer("third", Edit("final", "18", "15", "second", "refine"));
        Assert.Equal(15, Compose(third, second, first).CameraProperties["elevationDegrees"]);
        EncounterPatchLayer stale = Layer("stale", Edit("stale", "18", "15", "first", "raise"));
        Assert.Contains("Conflicting write", Assert.Throws<InvalidDataException>(() => Compose(first, second, stale)).Message);
    }

    [Fact]
    public void CameraConflictsAndWrongGuardsRejectWholeCandidate()
    {
        EncounterPatchLayer first = Layer("first", Edit("raise", "0", "12"));
        EncounterPatchComposition accepted = Compose(first);
        InvalidDataException conflict = Assert.Throws<InvalidDataException>(() => Compose(first, Layer("other", Edit("raise", "12", "18"))));
        Assert.Contains("Conflicting write", conflict.Message);
        Assert.Contains("first", conflict.Message);
        Assert.Contains("atomically", conflict.Message);
        Assert.Contains("Guard failed", Assert.Throws<InvalidDataException>(() => Compose(Layer("wrong", Edit("raise", "1", "12")))).Message);
        Assert.Equal(12, accepted.CameraProperties["elevationDegrees"]);
    }

    [Theory]
    [InlineData("-46")]
    [InlineData("46")]
    [InlineData("1e309")]
    [InlineData("null")]
    [InlineData("\"12\"")]
    public void CameraRejectsUnboundedOrUntypedValues(string value)
    {
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", "0", value)));
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", value, "0")));
    }

    [Fact]
    public void CameraRejectsUnknownPropertiesAndActorFields()
    {
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", "0", "12").Replace("elevationDegrees", "eyeY", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => Layer("invalid", Edit("raise", "0", "12").Replace("\"property\"", "\"actor\":\"original\",\"property\"", StringComparison.Ordinal)));
    }

    private static EncounterPatchComposition Compose(params EncounterPatchLayer[] layers)
    {
        return EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), layers);
    }

    private static EncounterPatchLayer Layer(string document, string operation)
    {
        return new(document, "gameplay/encounters/camera.json", EncounterPatchDocument.Parse("{\"schemaVersion\":1,\"documentId\":\"" + document + "\",\"encounter\":\"fixture.encounter\",\"atomic\":true,\"operations\":[" + operation + "]}"));
    }

    private static string Edit(string id, string expected, string value, string? producerDocument = null, string? producerOperation = null)
    {
        string producer = producerDocument is null ? "" : ",\"producer\":{\"documentId\":\"" + producerDocument + "\",\"operationId\":\"" + producerOperation + "\"}";
        return "{\"id\":\"" + id + "\",\"kind\":\"SetCameraProperty\",\"property\":\"elevationDegrees\",\"expectedValue\":" + expected + ",\"value\":" + value + producer + "}";
    }
}
