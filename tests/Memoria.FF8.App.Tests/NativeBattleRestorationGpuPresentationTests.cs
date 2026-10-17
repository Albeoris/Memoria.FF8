using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRestorationGpuPresentationTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void Original_Full_life_flat_leaf_renders_nonblack_pixels_and_independent_frame_restores_match(int index)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleRestorationExecutableState bank = NativeBattleRestorationExecutableState.FromColdProcess(27).Initialize(game.ReadNativeRestorationResources(27).Executable!);
        uint address = NativeBattleRestorationProfile.FullLife.ExecutableAddress;
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(address + 0x84 + (uint)index * 4, 4));
        ImmutableArray<byte> mesh = bank.Read(pointer, checked(NativeBattleRestorationExecutableState.Length(27) - (int)(pointer - address))).ToImmutableArray();
        int groups = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan());
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(groups + 4)));
        byte[] face = mesh.AsSpan().Slice(groups + 8, 12).ToArray();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[4];
        for (int corner = 0; corner < 4; corner++)
        {
            int offset = 8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
            ReadOnlySpan<byte> xyz = mesh.AsSpan().Slice(offset, 6);
            vertices[corner] = NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..])), projection);
        }
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitF4_8846C0(face, corner => vertices[corner], new(0x30, null, null, null, null, null, 17, 2));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(26);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), batch);
        //This isolated original face has an explicit inherited additive material; it does not reconstruct a live cast's OT history.
        var (_, commands, _) = NativeBattleLogicalPasses.Execute(written.Ordering, assets.ReadNativePrimitiveTables(), new(0x20), 1);
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), commands, [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        string json = JsonSerializer.Serialize(frame);
        NativeBattlePrimitiveFrame first = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(json)!;
        NativeBattlePrimitiveFrame second = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(json)!;
        BattleRenderer renderer = new(320, 216);
        BattleRenderer restoredRenderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(first, null).ToArray();
        byte[] restoredPixels = restoredRenderer.RenderNativePrimitiveFrame(second, null).ToArray();
        Assert.True(renderer.DrawnTriangles > 0);
        Assert.Contains(Enumerable.Range(0, 320 * 216), pixel => pixels[pixel * 4] != 0 || pixels[pixel * 4 + 1] != 0 || pixels[pixel * 4 + 2] != 0);
        Assert.Equal(pixels, restoredPixels);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }
}
