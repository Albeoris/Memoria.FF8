using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGravija214EnvironmentTests
{
    [Fact]
    public void Original_clear_is_immediate_and_preserves_neighboring_VRAM_words_and_pending_uploads()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(575, 255, 322, 1, Enumerable.Repeat((byte)0xA5, 644).ToArray()).Upload45BD30(575, 256, 322, 256, Enumerable.Repeat((byte)0xA5, 644 * 256).ToArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Textures = textures, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() };
        NativeBattleEffectMemory cleared = NativeBattleGravija214Environment.Clear45BEE0(memory, new(576, 256, 320, 256), 0, 0, 0);
        NativeBattleEffectArena image = new(NativeBattleStorageSnapshots.Capture(cleared.Textures!).VramRegions);
        for (int row = 256; row < 512; row++)
        {
            Assert.Equal(0xA5A5, Word(575, row));
            Assert.Equal(0xA5A5, Word(896, row));
            Assert.All(image.Read(2 * (row * 1024 + 576), 640), value => Assert.Equal((byte)0, value));
        }
        Assert.All(image.Read(2 * (255 * 1024 + 575), 644), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(textures.Count, cleared.Textures!.Count);
        Assert.Equal(textures.Slots, cleared.Textures.Slots);
        Assert.Same(memory.PrimitiveProcess!.Software, cleared.PrimitiveProcess!.Software);
        Assert.Contains(cleared.PrimitiveProcess.TextureWindowCache!.Rows, entry => entry.DirtyMask != 0);

        ushort Word(int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(image.Read(2 * (y * 1024 + x), 2));
    }

    [Fact]
    public void Odd_width_native_clear_packs_red_high_and_blue_low_without_bit15()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Textures = NativeBattleEffectTextureState.FromColdProcess(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() };
        NativeBattleEffectMemory cleared = NativeBattleGravija214Environment.Clear45BEE0(memory, new(576, 256, 3, 2), 248, 16, 8);
        NativeBattleEffectArena image = new(NativeBattleStorageSnapshots.Capture(cleared.Textures!).VramRegions);
        for (int y = 256; y < 258; y++)
        {
            Assert.Equal(new byte[] { 0x41, 0x7C, 0x41, 0x7C, 0x41, 0x7C }, image.Read(2 * (y * 1024 + 576), 6));
        }
    }

    [Fact]
    public void Origin_retains_derived_environment_until_its_actual_publication()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess();
        NativeSoftwareDrawingEnvironmentState environment = process.Software!.Environment with { Debug = 4 };
        process = process with { Software = process.Software with { Environment = environment } };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = process };
        NativeBattleEffectMemory updated = NativeBattleGravija214Environment.SetOrigin45B5E0(memory, 576, 256);
        Assert.Equal(environment with { OriginX = 576, OriginY = 256, Debug = 5 }, updated.PrimitiveProcess!.Software!.Environment);
        Assert.Same(memory.Textures, updated.Textures);
        Assert.Same(process.TextureWindowCache, updated.PrimitiveProcess.TextureWindowCache);
    }
}
