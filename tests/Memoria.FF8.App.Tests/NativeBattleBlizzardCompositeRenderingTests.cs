using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBlizzardCompositeRenderingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void Composite_original_command_tables_select_the_partner_window_and_add_visible_pixels_after_json(int verticalOffset)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original primitive command tables are required.");
        using BattleAssets assets = new(directory);
        ImmutableArray<byte> face = Face();
        ImmutableArray<NativeBattleModuleProjectedVertex> corners = [new(Point(80, 80), 1024, 0), new(Point(240, 80), 1024, 0), new(Point(80, 240), 1024, 0)];
        NativeBattleBlizzardFacePacketContext context = new(0x24808080, 0, null, null, verticalOffset, new(0, 128, 128, 128), new(0, 0, 0, 0), 2, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzardFacePackets.EmitFace61B9A0(face, face, corners, 1, 1, context);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), batch);
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null);
        var (mode, commands, selectedCache) = NativeBattleLogicalPasses.Execute(written.Ordering, assets.ReadNativePrimitiveTables(), new(0x20) { TextureWindowBits = 0 }, 1, cache);
        NativeBattleLogicalResolvedCommand partner = Assert.Single(commands, command => command.Pass == 2 && command.Command.Opcode == 0x26);
        Assert.Equal(new int[4], partner.Command.VertexPayload4.ToArray());
        Assert.Equal(128, partner.DrawMode.TextureSelection!.Y);
        Assert.Equal(128, partner.DrawMode.TextureSelection.Height);
        Assert.Equal(0x2080210u & 0xFFFFFFu, partner.DrawMode.TextureWindowBits);
        Assert.Equal(0u, mode.TextureWindowBits);
        Assert.Equal(1, partner.DrawMode.BlendMode);
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), commands, [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        NativeBattleEffectTextureState textures = Texture();
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, textures, textureWindowCache: selectedCache).ToArray();
        NativeBattlePrimitiveFrame primaryOnly = frame with { LastCommands = commands.Where(command => command.Command.Packet.ByteOffset != 32).ToImmutableArray() };
        byte[] withoutPartner = renderer.RenderNativePrimitiveFrame(primaryOnly, textures, textureWindowCache: selectedCache).ToArray();
        Assert.Contains(Enumerable.Range(0, 320 * 216), pixel => pixels[pixel * 4 + 1] > 0);
        Assert.All(Enumerable.Range(0, 320 * 216), pixel => Assert.Equal((byte)0, withoutPartner[pixel * 4 + 1]));
        Assert.NotEqual(pixels, withoutPartner);
        NativeBattlePrimitiveFrame first = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!;
        NativeBattlePrimitiveFrame second = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!;
        byte[] restored = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(second, textures, textureWindowCache: selectedCache).ToArray();
        Assert.Equal(pixels, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(first, textures, textureWindowCache: selectedCache).ToArray());
        Assert.Equal(pixels, restored);
    }

    private static ImmutableArray<byte> Face()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x40000000);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00200010);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x1000);
        return face.ToImmutableArray();
    }

    private static NativeBattleEffectTextureState Texture()
    {
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder regions = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        for (int row = 0; row < 256; row++)
        {
            //Explicit synthetic VRAM operands distinguish the full-page primary from the native Y128 partner crop.
            regions.Add(new(row * 2048, Enumerable.Repeat(row < 128 ? (byte)0x11 : (byte)0x22, 128).ToImmutableArray()));
        }
        byte[] palette = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(4), 0x03E0);
        regions.Add(new(256 * 2048, palette.ToImmutableArray()));
        return NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = regions.ToImmutable() };
    }

    private static uint Point(int x, int y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
