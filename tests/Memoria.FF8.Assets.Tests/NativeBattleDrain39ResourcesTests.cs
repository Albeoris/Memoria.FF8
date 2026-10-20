using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDrain39ResourcesTests
{
    [Fact]
    public void Original_Drain_keeps_its_backward_absolute_directory_camera_sound_and_both_flash_programs()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDrain39Resources resources = game.ReadNativeDrain39Resources();
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        Assert.Equal(image.Read(NativeBattleDrain39Nodes.BankAddress, NativeBattleDrain39Nodes.BankLength), resources.NodeBank.ToArray());
        Assert.Equal(image.ReadLoader(NativeBattleDrain39Resources.ChannelsAddress, NativeBattleDrain39Resources.ChannelsLength), resources.Channels!.Value.ToArray());
        Assert.All(resources.Channels.Value, value => Assert.Equal((byte)0, value));
        byte[] emitter = resources.NodeBank.AsSpan().Slice(checked((int)(0x15D4BC8u - NativeBattleDrain39Nodes.BankAddress)), 0x158).ToArray();
        NativeBattleAnimatedAnchorChannels channels = NativeBattleEffectAnimatedAnchor.ReadChannels(emitter, 0, ReadSource);
        Assert.Equal(new NativeBattleAnimatedAnchorChannels(0, 1, new(0, 0, 0), 1, 1, new(0, 0, 0), 0), channels);
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(NativeBattleDrain39Resources.ChannelsAddress - 1, 1));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(NativeBattleDrain39Resources.ChannelsAddress + NativeBattleDrain39Resources.ChannelsLength, 1));
        Assert.Equal(0x738, resources.Animation.Data.Length);
        Assert.Equal(image.Read(0x15D5B94, 0x738), resources.Animation.Data.ToArray());
        Assert.Equal(0x49433u, resources.Sound);
        Assert.Equal(new uint[] { 0x007F7F7F, 0x003F3F3F, 0xFF000000 }, resources.FirstFlash);
        Assert.Equal(new uint[] { 0x00280A32, 0x00200828, 0x0018061E, 0x00100414, 0x000C030F, 0x0008020A, 0x00040105, 0xFF000000 }, resources.SecondFlash);
        int directory = checked((int)(NativeBattleDrain39Nodes.DirectoryAddress - NativeBattleDrain39Nodes.BankAddress));
        Assert.Equal(1u, Read(directory));
        Assert.NotEqual(1u, Read(0));
        for (int index = 0; index < 16; index++)
        {
            Assert.Equal(index < 13 ? 0x15D4918u + (uint)index * 0x158 : 0u, Read(directory + 4 + index * 4));
        }
        for (int source = 0; source < 13; source++)
        {
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint pointer = Read(checked((int)(0x15D4918u - NativeBattleDrain39Nodes.BankAddress)) + source * 0x158 + field);
                Assert.True(pointer >= NativeBattleDrain39Nodes.BankAddress && pointer < NativeBattleDrain39Nodes.DirectoryAddress || pointer >= NativeBattleDrain39Resources.ChannelsAddress && pointer < NativeBattleDrain39Resources.ChannelsAddress + NativeBattleDrain39Resources.ChannelsLength);
                Assert.Single(ReadSource(pointer, 1));
            }
        }
        for (int index = 1; index < 65; index++)
        {
            uint pointer = Read(directory + index * 4);
            Assert.True(pointer == 0 || pointer >= NativeBattleDrain39Nodes.BankAddress && pointer < NativeBattleDrain39Nodes.DirectoryAddress);
        }
        byte[] ReadSource(uint address, int count)
        {
            if (address >= NativeBattleDrain39Nodes.BankAddress && address - NativeBattleDrain39Nodes.BankAddress <= resources.NodeBank.Length - count)
            {
                return resources.NodeBank.AsSpan().Slice(checked((int)(address - NativeBattleDrain39Nodes.BankAddress)), count).ToArray();
            }
            return resources.ReadChannels(address, count);
        }
        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeBank.AsSpan().Slice(offset));
    }
}
