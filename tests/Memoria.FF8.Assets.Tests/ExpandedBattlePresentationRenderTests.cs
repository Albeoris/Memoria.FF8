using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattlePresentationRenderTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(4, 5)]
    [InlineData(11, 14)]
    public void Installed_idle_native_frame_can_render_with_its_prepared_materials(int allyCount, int enemyCount)
    {
        Render(allyCount, enemyCount, fit: false, widen: false);
    }

    [Theory]
    [InlineData(4, 5, false)]
    [InlineData(11, 14, false)]
    [InlineData(11, 14, true)]
    public void Candidate_idle_native_projection_renders_on_the_next_callback(int allyCount, int enemyCount, bool widen)
    {
        Render(allyCount, enemyCount, fit: true, widen);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(35)]
    public void Elevated_idle_native_projection_preserves_roster_positions(int elevation)
    {
        Render(11, 14, fit: true, widen: false, elevation: elevation);
    }

    [Fact]
    public void Staggered_idle_native_formation_preserves_authored_enemy_positions()
    {
        Render(11, 14, fit: true, widen: false, stagger: true);
    }

    private void Render(int allyCount, int enemyCount, bool fit, bool widen, int elevation = 0, bool stagger = false)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOSTED_NATIVE_RENDER_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF8_HOSTED_NATIVE_RENDER_OUTPUT to opt in to the native frame render.");
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allyCount, enemyCount, authoredCount: 4, expanded: true);
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        string before = ExpandedBattlePresentationProcessEntryTests.LogicalState(logical);
        NativeBattleGraphicsRoster roster = logical.ReadExpandedPresentationRoster();
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> added = logicalDefinition.AddedEnemySources!;
        var combatants = logical.Combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        BattleEncounter encounter = assets.LoadEncounter(logicalDefinition.EncounterId!.Value);
        BattleStageCamera authored = assets.LoadStage(encounter.StageId).IntroductionCameras[encounter.PrimaryCamera & 0xF7];
        double shear = stagger ? (authored.NativeEye.X - authored.NativeTarget.X) / (double)(authored.NativeEye.Z - authored.NativeTarget.Z) : 0;
        if (stagger)
        {
            Assert.Equal(11, roster.PartyRows.Count());
            Assert.Equal(10, added.Count);
            Assert.True(double.IsFinite(shear));
            output.WriteLine(FormattableString.Invariant($"placement=stagger authoredEye={authored.NativeEye} authoredTarget={authored.NativeTarget} shear={shear:F8}"));
        }
        var partyPositions = logical.Combatants.Where(actor => actor.Slot is { Side: BattleSide.Allies }).ToDictionary(actor => actor.Id, actor => PartyPosition(actor.Slot!.Value.Position), StringComparer.Ordinal);
        var donorPositions = added.Keys.Select((slot, index) => (Slot: slot, Position: DonorPosition(slot, index))).ToDictionary(pair => pair.Slot, pair => pair.Position);
        NativeBattleGraphicsDefinition graphics = selected.NativeGraphics!;
        ImmutableArray<NativeBattleGraphicsBinding> bindings = graphics.Bindings.Select(binding => partyPositions.TryGetValue(binding.ActorId, out NativeBattleVector position)
            ? binding with { InitialPosition = position }
            : combatants[binding.ActorId].SceneSlot is int slot && donorPositions.TryGetValue(slot, out position)
                ? binding with { InitialPosition = position }
                : binding).ToImmutableArray();
        selected = selected with { NativeGraphics = graphics with { Bindings = bindings } };
        ExpandedBattlePresentationProcess process = new(logical, selected);
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            process.Advance(logical);
        }
        Assert.True(process.EntryReady, $"Entry stopped at lifecycle {process.Graphics.EntryLifecycle}, callback {process.Graphics.Callback}.");
        for (int callback = 0; callback < 30; callback++)
        {
            process.Advance(logical);
        }

        NativeBattlePreparedPresentation prepared = Assert.IsType<NativeBattlePreparedPresentation>(process.PreparedPresentation);
        Assert.Equal(process.Graphics.Callback, prepared.Callback);
        Assert.Equal(allyCount + enemyCount, prepared.Actors.Length);
        foreach (int row in roster.PartyRows.Concat(roster.EnemyRows))
        {
            NativeBattleGraphicsActor actor = roster[row];
            NativeBattleVector? expected;
            if (actor.IsParty)
            {
                expected = partyPositions[actor.ActorId];
            }
            else
            {
                int sceneSlot = combatants[actor.ActorId].SceneSlot!.Value;
                expected = donorPositions.TryGetValue(sceneSlot, out NativeBattleVector donor)
                    ? donor
                    : encounter.Slots.Single(slot => slot.Slot == sceneSlot).NativePosition;
            }
            Assert.Equal(expected, process.Graphics.Models[row].Transform!.Initial20);
        }
        Assert.NotEmpty(prepared.Primitives.LastCommands);
        NativeBattleCameraPose pose = Assert.IsType<NativeBattleCameraPose>(prepared.Camera);
        short focal = Assert.IsType<short>(pose.FocalLength);
        if (stagger)
        {
            Assert.Equal(authored.NativeEye, new Vector3(pose.Eye.X, pose.Eye.Y, pose.Eye.Z));
            Assert.Equal(authored.NativeTarget, new Vector3(pose.Target.X, pose.Target.Y, pose.Target.Z));
        }
        BattleCamera camera = BattleCamera.FromNative(new(new(pose.Eye.X, pose.Eye.Y, pose.Eye.Z), new(pose.Target.X, pose.Target.Y, pose.Target.Z), focal), pose.Roll);
        const int width = 1280;
        const int height = 720;
        PixelBounds viewport = ViewportLayout.Fit(320, 240, width, height);
        BattleRenderer renderer = new(width, height, viewport);
        byte[] Draw(NativeBattlePreparedPresentation frame) => renderer.RenderNativePrimitiveFrame(frame.Primitives, Assert.IsType<NativeBattleEffectTextureState>(frame.Textures), camera, selected.NativeGraphics!.PrimitiveCapabilities, frame.TextureWindows, uiFrame: frame.UiFrame, uiTexture: assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
        byte[] pixels = Draw(prepared);
        Vector4 safe = new(viewport.X + 8f / 320 * viewport.Width, 8f / 240 * height, viewport.X + 312f / 320 * viewport.Width, (allyCount == 4 ? 516f : 408f) / 672 * height);
        if (fit)
        {
            NativeBattleCameraState raw = process.Camera;
            NativeBattleCameraProjection original = Assert.IsType<NativeBattleCameraProjection>(prepared.PilotProjection);
            AssertProjection(original, raw.Projection);
            BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
            PropertyInfo cameraProperty = typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!;
            NativeBattlePreparedPresentation AdvanceProjection(NativeBattleCameraProjection projection)
            {
                cameraProperty.SetValue(child, raw with { Projection = projection });
                process.Advance(logical);
                NativeBattlePreparedPresentation frame = Assert.IsType<NativeBattlePreparedPresentation>(process.PreparedPresentation);
                AssertProjection(projection, frame.PilotProjection);
                Assert.Equal(raw.FocalLength, process.Camera.FocalLength);
                Assert.Equal(raw.CurrentPose, process.Camera.CurrentPose);
                AssertProjection(original, process.Camera.Projection);
                return frame;
            }
            if (elevation != 0)
            {
                NativeBattleCameraPose elevated = EncounterCameraPresentation.Elevate(pose, elevation);
                output.WriteLine($"elevation={elevation} derivedEye={elevated.Eye} target={elevated.Target} roll={elevated.Roll}");
                prepared = AdvanceProjection(original with { View = NativeBattlePoseMatrices.CameraView(elevated) });
                _ = Draw(prepared);
            }
            Vector4 measured = ReadUnion(renderer, prepared);
            NativeBattleCameraProjection projectionToFit = Assert.IsType<NativeBattleCameraProjection>(prepared.PilotProjection);
            NativeBattleCameraProjection candidate = FitProjection(projectionToFit, prepared, viewport, measured, safe);
            output.WriteLine($"variant={(widen ? "widen" : "preserve")} elevation={elevation} roster={allyCount}x{enemyCount} initialBounds={measured} safe={safe} candidateH={candidate.FocalLength} candidateY={candidate.OffsetY}");
            prepared = AdvanceProjection(candidate);
            pixels = Draw(prepared);
            output.WriteLine($"fittedBounds={ReadUnion(renderer, prepared)} drawnH={prepared.PilotProjection!.FocalLength} drawnY={prepared.PilotProjection.OffsetY} retainedH={process.Camera.Projection?.FocalLength} retainedY={process.Camera.Projection?.OffsetY}");
        }
        Assert.Equal(width * height * 4, pixels.Length);
        Assert.True(HasColoredPixel(pixels));
        Assert.Equal(before, ExpandedBattlePresentationProcessEntryTests.LogicalState(logical));

        output.WriteLine($"{allyCount}x{enemyCount} stage={prepared.StageResourceId} callback={prepared.Callback} eye={pose.Eye} target={pose.Target} roll={pose.Roll} focal={pose.FocalLength} projection={process.Camera.Projection} viewport={viewport} commands={prepared.Primitives.LastCommands.Length} ui={prepared.UiFrame is not null}");
        foreach (NativeBattlePreparedActor actor in prepared.Actors)
        {
            int commands = prepared.Primitives.LastCommands.Count(command => command.Command.PresentationActorId == actor.Id);
            bool hasBounds = renderer.TryGetActorBounds(actor.Id, out Vector4 bounds);
            output.WriteLine(FormattableString.Invariant($"row={logical.ReadExpandedPresentationRow(actor.Id)} actor={actor.Id} model={actor.ModelId} commands={commands} bounds={(hasBounds ? $"{bounds.X:F1},{bounds.Y:F1},{bounds.Z:F1},{bounds.W:F1}" : "none")}"));
        }
        Directory.CreateDirectory(directory!);
        PixelImage image = new(width, height, MemoryMarshal.Cast<byte, Rgba32>(pixels).ToArray());
        string suffix = stagger ? "-stagger-fit" : elevation != 0 ? $"-elevation-{elevation}-fit" : fit ? widen ? "-widen-fit" : "-fit" : "";
        File.WriteAllBytes(Path.Combine(directory!, $"native-{allyCount}x{enemyCount}{suffix}.png"), LocalizationArtworkExporter.EncodePng(image));

        NativeBattleVector PartyPosition(int index)
        {
            NativeBattleVector position = ExpandedBattleFormation.Position(index, allyCount, isAlly: true);
            if (!stagger)
            {
                return Widen(position, widen);
            }
            return index < 6 ? StaggerRow(position, index, 6, 1800, 1800) : StaggerRow(position, index - 6, 5, 2700, 1800);
        }

        NativeBattleVector DonorPosition(int slot, int index)
        {
            NativeBattleVector position = ExpandedBattleFormation.AddedEnemyPosition(slot, added.Count);
            if (!stagger)
            {
                return Widen(position, widen);
            }
            return StaggerRow(position, index % 5, 5, index < 5 ? (short)-2700 : (short)-3600, -1800);
        }

        NativeBattleVector StaggerRow(NativeBattleVector position, int index, int capacity, short z, int referenceZ)
        {
            double x = (2 * index - capacity + 1) * 600 + (z - referenceZ) * shear;
            return position with { X = checked((short)Math.Round(x)), Z = z };
        }
    }

    private static NativeBattleVector Widen(NativeBattleVector position, bool widen)
    {
        return widen ? position with { X = checked((short)(position.X * 3 / 2)) } : position;
    }

    private static void AssertProjection(NativeBattleCameraProjection expected, NativeBattleCameraProjection? actual)
    {
        NativeBattleCameraProjection projection = Assert.IsType<NativeBattleCameraProjection>(actual);
        Assert.Equal(expected with { View = null }, projection with { View = null });
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleActionMatrix expectedView = expected.View!;
        Assert.Equal((expectedView.X, expectedView.Y, expectedView.Z, expectedView.Scale), (view.X, view.Y, view.Z, view.Scale));
        Assert.Equal(expectedView.Rotation.ToArray(), view.Rotation.ToArray());
    }

    private static Vector4 ReadUnion(BattleRenderer renderer, NativeBattlePreparedPresentation prepared)
    {
        Vector4 union = new(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (NativeBattlePreparedActor actor in prepared.Actors)
        {
            Assert.True(renderer.TryGetActorBounds(actor.Id, out Vector4 bounds), $"No rendered bounds for {actor.Id}.");
            union = new(Math.Min(union.X, bounds.X), Math.Min(union.Y, bounds.Y), Math.Max(union.Z, bounds.Z), Math.Max(union.W, bounds.W));
        }
        return union;
    }

    private static NativeBattleCameraProjection FitProjection(NativeBattleCameraProjection original, NativeBattlePreparedPresentation prepared, PixelBounds viewport, Vector4 measured, Vector4 safe)
    {
        var mapping = prepared.Primitives.LastCommands.Where(command => command.Command.PresentationActorId is not null && command.Command.Opcode is >= 0x20 and <= 0x3F)
            .Select(command => command.DrawMode.NormalViewport is { } derived && command.DrawMode.ViewportInputs is { } inputs
                ? (XUnit: (double)viewport.Width * derived.ScaleX / inputs.Width, YUnit: (double)viewport.Height * derived.ScaleY / inputs.Height,
                    XOrigin: viewport.X + (derived.NormalOffsetX + 0.5) * viewport.Width / inputs.Width, YOrigin: viewport.Y + (derived.NormalOffsetY + 0.5) * viewport.Height / inputs.Height)
                : (XUnit: viewport.Width / 320d, YUnit: viewport.Height / 216d, XOrigin: (double)viewport.X, YOrigin: (double)viewport.Y)).Distinct().ToArray();
        var (xUnit, yUnit, xOrigin, yOrigin) = mapping[0];
        Assert.All(mapping, current =>
        {
            Assert.Equal(xUnit, current.XUnit, 6);
            Assert.Equal(yUnit, current.YUnit, 6);
            Assert.Equal(xOrigin, current.XOrigin, 6);
            Assert.Equal(yOrigin, current.YOrigin, 6);
        });
        double centerX = xOrigin + original.OffsetX!.Value * xUnit;
        double centerY = yOrigin + original.OffsetY!.Value * yUnit;
        int authoredH = original.FocalLength!.Value;
        int authoredY = original.OffsetY.Value;
        for (int h = authoredH; h >= 1; h--)
        {
            double scale = (double)h / authoredH;
            if (centerX + (measured.X - centerX) * scale < safe.X + 0.5 || centerX + (measured.Z - centerX) * scale > safe.Z - 0.5)
            {
                continue;
            }
            int minY = checked(authoredY + (int)Math.Ceiling((safe.Y + 0.5 - centerY - (measured.Y - centerY) * scale) / yUnit));
            int maxY = checked(authoredY + (int)Math.Floor((safe.W - 0.5 - centerY - (measured.W - centerY) * scale) / yUnit));
            if (minY <= maxY)
            {
                return original with { FocalLength = checked((ushort)h), OffsetY = checked((short)Math.Clamp(authoredY, minY, maxY)) };
            }
        }
        throw new InvalidDataException("No integer projection fit was found for the measured frame.");
    }

    private static bool HasColoredPixel(byte[] pixels)
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
