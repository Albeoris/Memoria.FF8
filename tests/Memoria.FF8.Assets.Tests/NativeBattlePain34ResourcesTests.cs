using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePain34ResourcesTests
{
    [Fact]
    public void Original_players_have_equal_forty_frame_lifetimes_and_actual_workspace_sizes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattlePain34Resources resources = game.ReadNativePain34Resources();
        Assert.Same(resources, game.ReadNativePain34Resources());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([34])[33];
        Assert.Equal(0x865600u, binding.Entry);
        Assert.Equal(0x8655E0u, binding.Initializer);
        Assert.Same(resources, binding.Pain34Resources);
        Assert.Equal(0x15E7040u, resources.Glow.Address);
        Assert.Equal(0x15EA2E8u, resources.Model.Address);
        Assert.Equal(0x32A8, resources.Glow.Data.Length);
        Assert.Equal(0xFAC, resources.Model.Data.Length);
        foreach ((NativeBattleGfAnimationSource source, int nodes, int size) in new[] { (resources.Glow, 25, 2056), (resources.Model, 9, 640) })
        {
            int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
            Assert.Equal(nodes, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)));
            Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)));
            NativeBattleGfAnimationWorkspace workspace = new(System.Collections.Immutable.ImmutableArray.CreateRange(new byte[size]));
            workspace = workspace.Initialize7016B0(source, size);
            Assert.Equal(source.Address, workspace.ResourceAddress);
            Assert.Equal(0, workspace.Frame);
        }
    }

    [Fact]
    public void Original_camera_all_twenty_four_tracks_end_within_its_exact_consumed_extent()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattlePain34Resources resources = game.ReadNativePain34Resources();
        ReadOnlySpan<byte> camera = resources.Camera.Data.AsSpan();
        Assert.Equal(0xAC8, camera.Length);
        int bankOffset = BinaryPrimitives.ReadUInt16LittleEndian(camera[4..]);
        Assert.Equal(0x144, bankOffset);
        NativeBattleCameraBank bank = new(resources.Camera.Data.Slice(bankOffset, resources.Camera.Data.Length - bankOffset));
        int maxEnd = 0;
        for (int group = 0; group < 3; group++)
        {
            for (int channel = 0; channel < 8; channel++)
            {
                int cursor = bankOffset + bank.TrackOffset(checked((byte)(group * 16 + channel)))!.Value;
                while (true)
                {
                    ushort flags = ReadWord();
                    if (flags == ushort.MaxValue)
                    {
                        break;
                    }
                    foreach (int shift in new[] { 6, 8 })
                    {
                        int mode = flags >> shift & 3;
                        cursor += mode == 2 ? 2 : mode == 3 ? 4 : 0;
                    }
                    while (unchecked((short)ReadWord()) >= 0)
                    {
                        cursor += 16;
                    }
                }
                maxEnd = Math.Max(maxEnd, cursor);

                ushort ReadWord()
                {
                    ushort value = BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(cursor));
                    cursor += 2;
                    return value;
                }
            }
        }
        Assert.Equal(resources.Camera.Data.Length, maxEnd);
    }

    [Fact]
    public void Loader_cold_module_bytes_match_the_declared_runtime_process_owner()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        byte[] executable = game.ReadNativeExecutable();
        NativeExecutableMemory memory = new(executable);
        NativeBattlePain34State actual = NativeBattlePain34ResourcesReader.ReadColdProcess(executable);
        Assert.Equal(memory.ReadLoader(NativeBattlePain34State.Address, NativeBattlePain34State.Length), actual.Read(NativeBattlePain34State.Address, NativeBattlePain34State.Length));
        Assert.Equal(actual.Read(NativeBattlePain34State.Address, NativeBattlePain34State.Length), NativeBattlePain34State.FromColdProcess().Read(NativeBattlePain34State.Address, NativeBattlePain34State.Length));
    }
}
