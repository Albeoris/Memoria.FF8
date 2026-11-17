using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeCamera))!.SetValue(session.Presentation!, NativeBattleCameraState.FromColdProcess() with { CurrentPose = source });
        NativeBattleGraphicsState graphics = new(new BattleActorModels([]), NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, OriginalEffects()) { OriginalEffect = new(1, 0, new(1, 0x8D6A00, 0), null, null) };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session.Presentation!, graphics);
        System.Reflection.PropertyInfo presentation = typeof(BattlePresentationProcess).GetProperty("EncounterPresentationCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        NativeBattleEffectMemory active = NativeBattleEffectMemory.FromColdProcess() with { Active = 1 };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, active);
        Assert.Equal(source, presentation.GetValue(session.Presentation!));
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeCamera))!.SetValue(session.Presentation!, session.Presentation!.Camera with { CapturedPose = source });
        Assert.Equal(EncounterCameraPresentation.Elevate(source, 12), presentation.GetValue(session.Presentation!));
        NativeBattleEffectMemory completed = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(active with { Active = 0 }));
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, completed);
        Assert.Equal(EncounterCameraPresentation.Elevate(source, 12), presentation.GetValue(session.Presentation!));
        Assert.Same(graphics.OriginalEffect, session.Presentation!.Graphics.OriginalEffect);
        Assert.Equal(source, session.Presentation!.Camera.CurrentPose);
    }

    [Fact]
    public void Hosted_framing_includes_unaliased_source_and_target_without_reading_unselected_geometry()
    {
        BattleModelOwner source = new BattleForeignPartyModel(new("added-source"));
        BattleModelOwner target = new BattleForeignEnemyModel(new("added-target"));
        BattleActorModels models = new(Enumerable.Range(0, 23).Select(index => new BattleModelEntry<BattleModelOwner>(new BattleForeignEnemyModel(new($"unselected-{index}")), new NativeBattleGraphicsModel(2))));
        models = models.Add(source, CameraModel(-6500)).Add(target, CameraModel(7400));
        NativeBattleEffectParameters parameters = new(source, 0, BattleModelTargets.From([target]), new BattleActionHeaderReference(0), 0, 0);
        NativeBattleGraphicsState graphics = new(models, NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
        BattleSession session = CreateFramingSession(graphics, parameters);
        NativeBattleCameraPose framed = PrepareCamera(session);
        Assert.Equal(ExpectedFraming(session.Presentation!.Camera, -6500, 7400), framed);

        // Body geometry for the unselected loaded owners is deliberately absent.
        BattleSession withoutFarTarget = CreateFramingSession(graphics with { ActorModels = models.Set(target, CameraModel(0)) }, parameters);
        Assert.NotEqual(framed.Eye, PrepareCamera(withoutFarTarget).Eye);

        BattleSession withWiderSource = CreateFramingSession(graphics with { ActorModels = models.Set(source, CameraModel(-8500)) }, parameters);
        NativeBattleCameraPose framedBySource = PrepareCamera(withWiderSource);
        Assert.Equal(ExpectedFraming(withWiderSource.Presentation!.Camera, -8500, 7400), framedBySource);
        BattleSession withoutFarSource = CreateFramingSession(graphics with { ActorModels = models.Set(source, CameraModel(0)) }, parameters);
        Assert.NotEqual(framedBySource.Eye, PrepareCamera(withoutFarSource).Eye);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hosted_framing_rejects_unbound_or_retired_owners_before_reading_model_geometry(bool retired)
    {
        BattleModelOwner source = new BattleForeignPartyModel(new("added-source"));
        BattleModelOwner target = new BattleForeignEnemyModel(new("added-target"));
        BattleActorModels models = new([new(source, new NativeBattleGraphicsModel(2))]);
        if (retired)
        {
            models = models.Add(target, new NativeBattleGraphicsModel(0)).Remove(target);
            models = models.Add(new BattleForeignEnemyModel(new("replacement")), new NativeBattleGraphicsModel(0));
        }
        NativeBattleEffectParameters parameters = new(source, 0, BattleModelTargets.From([target]), new BattleActionHeaderReference(0), 0, 0);
        NativeBattleGraphicsState graphics = new(models, NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
        BattleSession session = CreateFramingSession(graphics, parameters);
        NativeBattleCameraState? beforeCamera = session.Presentation?.Camera;
        NativeBattleGraphicsState? beforeGraphics = session.Presentation?.Graphics;
        NativeBattleEffectMemory? beforeMemory = session.Presentation?.Memory;
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => PrepareCamera(session));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(beforeCamera, session.Presentation?.Camera);
        Assert.Same(beforeGraphics, session.Presentation?.Graphics);
        Assert.Same(beforeMemory, session.Presentation?.Memory);
    }

    [Fact]
    public void Original_slot_framing_preserves_source_and_raw_mask_projection()
    {
        BattleModelOwner source = OriginalOwner(1);
        BattleModelOwner target = OriginalOwner(5);
        BattleModelOwner unloaded = OriginalOwner(6);
        NativeBattleGraphicsState graphics = OriginalGraphics([(1, source, CameraModel(-6500)), (5, target, CameraModel(7400)), (6, unloaded, new(0))]);
        NativeBattleEffectParameters parameters = new(source, 0, BattleModelTargets.From([target, unloaded]), new BattleActionHeaderReference(0), 0, 0);
        parameters = parameters.CaptureOriginalWindow(graphics.NativeBindings.CaptureAliases(graphics.ActorModels));
        Assert.Equal(new byte[] { (1 << 5) | (1 << 6), 0 }, NativeBattleCinematicParameterBytes.Read(parameters, 2, 2));

        BattleSession session = CreateFramingSession(graphics, parameters);
        NativeBattleCameraState camera = session.Presentation!.Camera;
        NativeBattleCameraPose expected = ExpectedFraming(camera, -6500, 7400);
        NativeBattleCameraPose actual = PrepareCamera(session);
        Assert.Equal(expected, actual);
        Assert.Equal(JsonSerializer.Serialize(camera.PublishProjection(4, presentationPose: expected).Projection), JsonSerializer.Serialize(camera.PublishProjection(4, presentationPose: actual).Projection));
    }

    private static BattleSession CreateFramingSession(NativeBattleGraphicsState graphics, NativeBattleEffectParameters parameters)
    {
        EncounterPatchComposition composition = Compose(Layer("camera", Edit("raise", "0", "12")));
        BattleDefinition definition = new() { Name = "Camera framing", Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true }, new() { Id = "original", Name = "Original" }] };
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("fixture.encounter", "original", 11, 11, [], true));
        BattleSession session = new(definition with { EncounterMod = compiled }, BattleAtbMode.TurnBased);
        NativeBattleCameraPose pose = new(new(0, -500, 5000), new(0, -500, 0), 512, 0);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = pose, FocalLength = 512, Projection = new(null, 512, 160, 108) };
        graphics = graphics with { OriginalEffect = new(1, 0, new(1, 0x8D6A00, 0), parameters, 0) };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeCamera))!.SetValue(session.Presentation!, camera);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session.Presentation!, graphics);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, NativeBattleEffectMemory.FromColdProcess() with { Active = 1 });
        return session;
    }

    private static NativeBattleCameraPose PrepareCamera(BattleSession session)
    {
        return Assert.IsType<NativeBattleCameraPose>(typeof(BattlePresentationProcess).GetMethod("PrepareEncounterCamera", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session.Presentation!, [session.Presentation?.Camera]));
    }

    private static NativeBattleGraphicsModel CameraModel(int x)
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
