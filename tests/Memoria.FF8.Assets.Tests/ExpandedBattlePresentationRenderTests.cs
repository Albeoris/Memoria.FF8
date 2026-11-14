using System.Collections.Immutable;
using System.Numerics;
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
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOSTED_NATIVE_RENDER_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF8_HOSTED_NATIVE_RENDER_OUTPUT to opt in to the native frame render.");
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allyCount, enemyCount, authoredCount: 4, expanded: true);
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        string before = ExpandedBattlePresentationProcessEntryTests.LogicalState(logical);
        NativeBattleGraphicsRoster roster = logical.ReadExpandedPresentationRoster();
        var partyPositions = logical.Combatants.Where(actor => actor.Slot is { Side: BattleSide.Allies }).ToDictionary(actor => actor.Id, actor => ExpandedBattleFormation.Position(actor.Slot!.Value.Position, allyCount, isAlly: true), StringComparer.Ordinal);
        NativeBattleGraphicsDefinition graphics = selected.NativeGraphics!;
        ImmutableArray<NativeBattleGraphicsBinding> bindings = graphics.Bindings.Select(binding => partyPositions.TryGetValue(binding.ActorId, out NativeBattleVector position)
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
        BattleEncounter encounter = assets.LoadEncounter(logicalDefinition.EncounterId!.Value);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> added = logicalDefinition.AddedEnemySources!;
        var combatants = logical.Combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
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
                    ? ExpandedBattleFormation.AddedEnemyPosition(sceneSlot, added.Count)
                    : encounter.Slots.Single(slot => slot.Slot == sceneSlot).NativePosition;
            }
            Assert.Equal(expected, process.Graphics.Models[row].Transform!.Initial20);
        }
        Assert.NotEmpty(prepared.Primitives.LastCommands);
        NativeBattleEffectTextureState textures = Assert.IsType<NativeBattleEffectTextureState>(prepared.Textures);
        NativeBattleCameraPose pose = Assert.IsType<NativeBattleCameraPose>(prepared.Camera);
        short focal = Assert.IsType<short>(pose.FocalLength);
        BattleCamera camera = BattleCamera.FromNative(new(new(pose.Eye.X, pose.Eye.Y, pose.Eye.Z), new(pose.Target.X, pose.Target.Y, pose.Target.Z), focal), pose.Roll);
        const int width = 1280;
        const int height = 720;
        PixelBounds viewport = ViewportLayout.Fit(320, 240, width, height);
        BattleRenderer renderer = new(width, height, viewport);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(prepared.Primitives, textures, camera, selected.NativeGraphics!.PrimitiveCapabilities, prepared.TextureWindows, uiFrame: prepared.UiFrame, uiTexture: assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
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
        File.WriteAllBytes(Path.Combine(directory!, $"native-{allyCount}x{enemyCount}.png"), LocalizationArtworkExporter.EncodePng(image));
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
