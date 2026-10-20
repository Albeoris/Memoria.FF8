using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTheEnd85ResourcesTests
{
    [Fact]
    public void Original_sources_authenticate_the_complete_module_BSS_script_files_and_static_model_banks()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        NativeBattleTheEnd85Resources resources = game.ReadNativeTheEnd85Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([85])[84];
        Assert.Equal(0x7844A0u, binding.Entry);
        Assert.Equal(0x784480u, binding.Initializer);
        Assert.Same(resources, binding.TheEnd85Resources);
        NativeBattleTheEnd85State state = NativeBattleTheEnd85State.FromColdProcess();
        Assert.Equal(image.ReadLoader(NativeBattleTheEnd85State.GlobalAddress, NativeBattleTheEnd85State.GlobalLength), state.Read(NativeBattleTheEnd85State.GlobalAddress, NativeBattleTheEnd85State.GlobalLength));
        Assert.Equal(image.ReadLoader(NativeBattleEffectNodeContextMemory.Address, NativeBattleEffectNodeContextMemory.Length), NativeBattleEffectNodeContextMemory.Capture(NativeBattleEffectMemory.FromColdProcess()).Read(0, NativeBattleEffectNodeContextMemory.Length));
        Assert.Equal(0x25C1C60u, resources.DirectorAddress);
        Assert.Equal(image.Read(0x1571CBC, 0x1B4), resources.Camera.Data.ToArray());
        Assert.Equal(image.Read(0x1571E70, 80), resources.Script.ToArray());
        Assert.Equal(new short[] { 402, 403, 404 }, resources.Files.Select(file => file.FileId));
        Assert.All(resources.Files, file => Assert.Equal(0x8220, file.Data.Length));
        Assert.Equal(40, resources.Overlay.Commands.Length);
        Assert.Equal(0xFF000000u, resources.Overlay.Commands[^1]);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(image.Read(0xB8B7D8, 4)), resources.DefaultColor);
        foreach (NativeBattleGfAnimationSource bank in resources.Banks)
        {
            Assert.Equal(image.Read(bank.Address, bank.Data.Length), bank.Data.ToArray());
        }
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Program(0x17151EC).Data.AsSpan()));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Program(0x1715D28).Data.AsSpan()));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Program(0x1718EE4).Data.AsSpan()));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Program(0x171C42C).Data.AsSpan()));
    }
}
