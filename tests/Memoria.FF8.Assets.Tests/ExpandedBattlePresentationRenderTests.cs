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

    private void Render(int allyCount, int enemyCount, bool fit, bool widen)
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
        var partyPositions = logical.Combatants.Where(actor => actor.Slot is { Side: BattleSide.Allies }).ToDictionary(actor => actor.Id, actor => Widen(ExpandedBattleFormation.Position(actor.Slot!.Value.Position, allyCount, isAlly: true), widen), StringComparer.Ordinal);
        NativeBattleGraphicsDefinition graphics = selected.NativeGraphics!;
        ImmutableArray<NativeBattleGraphicsBinding> bindings = graphics.Bindings.Select(binding => partyPositions.TryGetValue(binding.ActorId, out NativeBattleVector position)
            ? binding with { InitialPosition = position }
            : widen && combatants[binding.ActorId].SceneSlot is int slot && added.ContainsKey(slot)
                ? binding with { InitialPosition = Widen(binding.InitialPosition!.Value, true) }
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
        BattleEncounter encounter = assets.LoadEncounter(logicalDefinition.EncounterId!.Value);
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
                expected = added.ContainsKey(sceneSlot)
                    ? Widen(ExpandedBattleFormation.AddedEnemyPosition(sceneSlot, added.Count), widen)
                    : encounter.Slots.Single(slot => slot.Slot == sceneSlot).NativePosition;
            }
            Assert.Equal(expected, process.Graphics.Models[row].Transform!.Initial20);
        }
        Assert.NotEmpty(prepared.Primitives.LastCommands);
        NativeBattleCameraPose pose = Assert.IsType<NativeBattleCameraPose>(prepared.Camera);
        short focal = Assert.IsType<short>(pose.FocalLength);
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
            Vector4 measured = ReadUnion(renderer, prepared);
            NativeBattleCameraState raw = process.Camera;
            NativeBattleCameraProjection original = Assert.IsType<NativeBattleCameraProjection>(prepared.PilotProjection);
            AssertProjection(original, raw.Projection);
            NativeBattleCameraProjection candidate = FitProjection(original, prepared, viewport, measured, safe);
            output.WriteLine($"variant={(widen ? "widen" : "preserve")} roster={allyCount}x{enemyCount} initialBounds={measured} safe={safe} candidateH={candidate.FocalLength} candidateY={candidate.OffsetY}");
            BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
            typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(child, raw with { Projection = candidate });
            process.Advance(logical);
            prepared = Assert.IsType<NativeBattlePreparedPresentation>(process.PreparedPresentation);
            AssertProjection(candidate, prepared.PilotProjection);
            Assert.Equal(raw.FocalLength, process.Camera.FocalLength);
            Assert.Equal(raw.CurrentPose, process.Camera.CurrentPose);
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
        string suffix = fit ? widen ? "-widen-fit" : "-fit" : "";
        File.WriteAllBytes(Path.Combine(directory!, $"native-{allyCount}x{enemyCount}{suffix}.png"), LocalizationArtworkExporter.EncodePng(image));
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
