using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePrimitiveOrderingAssetsTests
{
    [Fact]
    public void Installed_command_lengths_and_both_render_passes_preserve_the_actual_inherited_blend()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_GAME_DIR to the installed game directory.");
        byte[] executable = File.ReadAllBytes(Path.Combine(directory!, "FF8_EN.exe"));
        ImmutableArray<byte> lengths = NativeBattleEffectStaticResourcesReader.Read(executable, 0xB7CC28, 256);
        ImmutableArray<uint> opaqueHandlers = Handlers(executable, 0xB7CF08);
        ImmutableArray<uint> transparentHandlers = Handlers(executable, 0xB7D308);
        Assert.Equal(3, lengths[0x62]);
        Assert.Equal(6, lengths[0x32]);
        Assert.Equal(8, lengths[0x3A]);
        Assert.Equal(1, lengths[0xE1]);
        Assert.Equal(0x4617F0u, opaqueHandlers[0x24]);
        Assert.Equal(0u, opaqueHandlers[0x62]);
        Assert.Equal(0x462720u, transparentHandlers[0x62]);
        Assert.Equal(0u, transparentHandlers[0x24]);
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(0);
        state = state.Prepend(17, new(0x24CBD6C, [0x07000000, 0x24000000, 0, 0, 0, 0x00400000, 0, 0]));
        state = state.Prepend(17, new(0x24CBD94, [0x03000000, 0x62000000, 0, 0]));
        ImmutableArray<NativeBattlePrimitiveCommand> commands = state.Traverse(lengths);
        NativeBattlePrimitivePassResult first = new NativeBattlePrimitiveDrawMode(null).ExecutePass(commands, opaqueHandlers);
        NativeBattlePrimitivePassResult second = first.DrawMode.ExecutePass(commands, transparentHandlers);
        Assert.Equal(0x24, Assert.Single(first.Commands).Command.Opcode);
        Assert.Equal(0x62, Assert.Single(second.Commands).Command.Opcode);
        Assert.Equal(2, second.Commands[0].DrawMode.BlendMode);
    }

    private static ImmutableArray<uint> Handlers(byte[] executable, uint address)
    {
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(executable, address, 1024);
        ImmutableArray<uint>.Builder result = ImmutableArray.CreateBuilder<uint>(256);
        for (int index = 0; index < 256; index++)
        {
            result.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4, 4)));
        }
        return result.MoveToImmutable();
    }
}
