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
    public void Original_Full_life_F4_G4_mesh_renders_a_real_gradient_and_independent_frame_restores_match(int index)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleRestorationExecutableState bank = NativeBattleRestorationExecutableState.FromColdProcess(27).Initialize(game.ReadNativeRestorationResources(27).Executable!);
        uint address = NativeBattleRestorationProfile.FullLife.ExecutableAddress;
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(address + 0x84 + (uint)index * 4, 4));
        ImmutableArray<byte> mesh = bank.Read(pointer, checked(NativeBattleRestorationExecutableState.Length(27) - (int)(pointer - address))).ToImmutableArray();
        int groups = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan());
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(groups + 4)));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch884370(mesh, matrix, projection, new(0x30, null, null, null, null, null, 17, 2));
        Assert.Equal(60, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(26);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), batch);
        //This isolated original mesh has an explicit inherited additive material; it does not reconstruct a live cast's OT history.
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
        Assert.Equal(pixels, restoredPixels);
        NativeBattlePrimitiveFrame gouraud = first with { LastCommands = first.LastCommands.Where(command => command.Pass == 2 && command.Command.Opcode == 0x3A).ToImmutableArray() };
        NativeBattlePrimitiveFrame flat = first with { LastCommands = first.LastCommands.Where(command => command.Pass == 2 && command.Command.Opcode == 0x2A).ToImmutableArray() };
        Assert.Single(gouraud.LastCommands);
        Assert.Single(flat.LastCommands);
        byte[] gradient = renderer.RenderNativePrimitiveFrame(gouraud, null).ToArray();
        byte[] flatPixels = renderer.RenderNativePrimitiveFrame(flat, null).ToArray();
        int[] gradientColors = Enumerable.Range(0, 320 * 216).Select(pixel => gradient[pixel * 4] | gradient[pixel * 4 + 1] << 8 | gradient[pixel * 4 + 2] << 16).Where(rgb => rgb != 0).Distinct().ToArray();
        Assert.True(gradientColors.Length >= 4, "The original G4 should interpolate several visible colors, rather than render a flat replacement.");
        int Count(byte[] rendered) => Enumerable.Range(0, 320 * 216).Count(pixel => rendered[pixel * 4] != 0 || rendered[pixel * 4 + 1] != 0 || rendered[pixel * 4 + 2] != 0);
        Assert.True(Count(pixels) > Count(flatPixels));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }
}
