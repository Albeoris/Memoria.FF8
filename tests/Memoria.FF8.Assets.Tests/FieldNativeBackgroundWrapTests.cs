using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeBackgroundWrapTests
{
    private const string Original135ProgramHash = "4E838898400B0B44D1FFAD537A77FDD685111A7C25DF34366CD77CACE8A65344";

    [Fact]
    public void Every_original_lava_tile_matches_native_placement_for_all_352_phases()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(135);
        Assert.Equal(Original135ProgramHash, Convert.ToHexString(SHA256.HashData(field.Jsm)));
        FieldInformation information = field.Information!;
        Assert.Equal(2, information.BackgroundWrapMask);
        Assert.Equal(new FieldBounds(-48, 48, 176, -176), information.CameraBounds[1]);
        BackgroundTile[] lava = field.Background.Where(tile => tile.Layer == 1).ToArray();
        Assert.Equal(110, lava.Length);
        for (int phase = 0; phase < 352; phase++)
        {
            FieldNativeBackgroundWrapFrame frame = Frame((short)-phase);
            foreach (BackgroundTile tile in lava)
            {
                FieldNativeBackgroundTileOrigin? expected = NativePlacement(tile, phase, information);
                FieldNativeBackgroundTileOrigin? actual = FieldNativeBackgroundTileProbe.Relocate((short)tile.X, (short)tile.Y, frame.Layers[0], frame);
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void Relocation_is_one_signed_word_step_followed_by_strict_origin_culling()
    {
        FieldNativeBackgroundWrapFrame frame = Frame(0);
        FieldNativeBackgroundWrapLayer layer = frame.Layers[0];
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(-192, 0, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(176, 0, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(0, -144, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(0, 128, layer, frame));
        Assert.Equal(new FieldNativeBackgroundTileOrigin(159, -49), FieldNativeBackgroundTileProbe.Relocate(-193, -145, layer, frame));
        Assert.Equal(new FieldNativeBackgroundTileOrigin(-175, 33), FieldNativeBackgroundTileProbe.Relocate(177, 129, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(-1000, 0, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(1000, 0, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(0, -1000, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(0, 1000, layer, frame));
        FieldNativeBackgroundWrapLayer overflow = layer with { OffsetX = 3000 };
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(30000, 0, overflow, frame));
    }

    [Fact]
    public void Rendering_all_lava_phases_matches_independently_relocated_original_tiles()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets original = assets.LoadField(135);
        FieldAssets lava = original with { Background = original.Background.Where(tile => tile.Layer == 1).ToArray() };
        FieldRenderer renderer = new(lava);
        Dictionary<int, int> states = Original135BackgroundStates();
        for (int phase = 0; phase < 352; phase++)
        {
            List<BackgroundTile> expectedTiles = [];
            foreach (BackgroundTile tile in lava.Background)
            {
                if (NativePlacement(tile, phase, original.Information!) is FieldNativeBackgroundTileOrigin placed)
                {
                    expectedTiles.Add(tile with { X = placed.X, Y = placed.Y });
                }
            }
            FieldRenderer reference = new(lava with { Background = expectedTiles });
            byte[] expected = reference.Render(states, [], TimeSpan.Zero);
            byte[] actual = renderer.Render(states, [], TimeSpan.Zero, nativeWrapFrame: Frame((short)-phase));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Default_pixels_cache_reset_and_source_bound_host_scope_are_preserved()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(135);
        foreach ((int width, int height) in new[] { (320, 240), (640, 480) })
        {
            FieldRenderer renderer = new(field, width, height);
            Dictionary<int, int> states = Original135BackgroundStates();
            byte[] baseline = renderer.Render(states, [], TimeSpan.Zero).ToArray();
            Assert.Equal(baseline, renderer.Render(states, [], TimeSpan.Zero, nativeWrapFrame: Frame(0)));
            Assert.Equal(baseline, renderer.Render(states, [], TimeSpan.Zero));
            byte[] wrapped = renderer.Render(states, [], TimeSpan.Zero, nativeWrapFrame: Frame(-81)).ToArray();
            Assert.NotEqual(baseline, wrapped);
            Assert.Equal(wrapped, renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(999, 999) }, nativeWrapFrame: Frame(-81)));
            Assert.Equal(baseline, renderer.Render(states, [], TimeSpan.Zero));
            foreach (FieldNativeBackgroundWrapFrame invalid in new[] { Frame(-352), Frame(-1) with { FieldId = 134 }, Frame(-1) with { ProgramHash = new string('0', 64) }, Frame(-1) with { Left = -193 }, Frame(-1) with { Layers = ImmutableArray.Create(new FieldNativeBackgroundWrapLayer(1, -1, 1, 352, 96)) } })
            {
                Assert.Throws<NotSupportedException>(() => renderer.Render(states, [], TimeSpan.Zero, nativeWrapFrame: invalid));
                Assert.Equal(baseline, renderer.Render(states, [], TimeSpan.Zero));
            }
        }
    }

    private static FieldNativeBackgroundWrapFrame Frame(short offsetX)
    {
        return new FieldNativeBackgroundWrapFrame(135, Original135ProgramHash, -192, 176, -144, 128, ImmutableArray.Create(new FieldNativeBackgroundWrapLayer(1, offsetX, 0, 352, 96)));
    }

    private static FieldNativeBackgroundTileOrigin? NativePlacement(BackgroundTile tile, int phase, FieldInformation information)
    {
        // Independent native475480 reference, deriving the viewport and periods
        // from original INF rather than consuming the production wrap frame.
        FieldBounds screen = information.ScreenBounds[0];
        int left = -(screen.Right - screen.Left) / 2 - 32;
        int right = (screen.Right - screen.Left) / 2 + 16;
        int top = -(screen.Bottom - screen.Top) / 2 - 32;
        int bottom = (screen.Bottom - screen.Top) / 2 + 16;
        int periodX = (short)(2 * information.CameraBounds[1].Right);
        int periodY = (short)(2 * information.CameraBounds[1].Bottom);
        int x = (short)(tile.X - phase);
        int y = (short)tile.Y;
        int correctionX = x < left ? periodX : x > right ? -periodX : 0;
        int correctionY = y < top ? periodY : y > bottom ? -periodY : 0;
        x = (short)(x + correctionX);
        y = (short)(y + correctionY);
        return x > left && x < right && y > top && y < bottom ? new FieldNativeBackgroundTileOrigin((short)x, (short)y) : null;
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        return new GameAssets(directory);
    }

    private static Dictionary<int, int> Original135BackgroundStates()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        using JsonDocument checkpoint = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement state = checkpoint.RootElement.GetProperty("EventState");
        Assert.Equal((135, 2L), (state.GetProperty("FieldId").GetInt32(), state.GetProperty("Frame").GetInt64()));
        return state.GetProperty("BackgroundStates").EnumerateObject().ToDictionary(pair => int.Parse(pair.Name), pair => pair.Value.GetInt32());
    }
}
