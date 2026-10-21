using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStageTileTests
{
    [Fact]
    public void OriginalStageTileAndSpriteSoftwareHandlersReachTheirCompleteReturnBodies()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeSoftwarePrimitiveTable table = NativeSoftwarePrimitiveTableReader.Read(executable);
        using PEReader reader = new(new MemoryStream(executable));
        for (int opcode = 0x60; opcode < 0x68; opcode++)
        {
            Assert.Equal(opcode < 0x64 ? 0x467010u : 0x467050u, table.Handler((byte)opcode));
        }
        Assert.Equal(new byte[] { 0xC3 }, Bytes(0x467010, 1));
        Assert.Equal(Convert.FromHexString("8B4424040FBF48120FBF501051520FBF480A0FBF5008515250E81200000083C414C3"), Bytes(0x467050, 34));
        byte[] wrapper = Bytes(0x467050, 34);
        Assert.Equal(0x467080u, unchecked(0x467050u + 25u + 5u + (uint)BinaryPrimitives.ReadInt32LittleEndian(wrapper.AsSpan(26))));
        Assert.Equal(new byte[] { 0xC3 }, Bytes(0x467080, 1));

        byte[] Bytes(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), length).ToArray();
        }
    }

    [Theory]
    [InlineData(0x62)]
    [InlineData(0x64)]
    [InlineData(0x66)]
    public void OriginalStageSoftwarePacketDispatchLeavesFramebufferAndRasterUnchanged(byte opcode)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeSoftwarePrimitiveTable table = assets.ReadNativeSoftwarePrimitiveTable();
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().Initialize500400();
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(1, 240, 1, 1, [0x34, 0x12]);
        NativeBattleSoftwarePrimitiveExecution execution = new(state, textures, table, true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        execution.ApplyEnvironment45B660(1);
        NativeSoftwarePolygonRasterState raster = execution.State.Raster;
        NativeSoftwareTextureState texture = execution.State.Texture;
        byte[] before = execution.Textures.ReadVram();
        uint[] words = opcode == 0x62 ? [(uint)opcode << 24 | 0x808080u, 1, 0x001000FF] : [(uint)opcode << 24 | 0x808080u, 0, 0, 0x00D80040];
        NativeBattleLogicalCommand command = new(new("stage", 0, 0), 1, opcode, System.Collections.Immutable.ImmutableArray.CreateRange(words), 0, [0, 0, 0, 0], null);
        execution.ExecuteSoftware(command);
        execution.FlushSoftwareImage();
        Assert.Same(raster, execution.State.Raster);
        Assert.Same(texture, execution.State.Texture);
        Assert.Equal(before, execution.Textures.ReadVram());
        NativeSoftwarePrimitiveTable crossed = table with { Handlers = table.Handlers.SetItem(opcode, 0x4667C0) };
        NativeBattleSoftwarePrimitiveExecution invalid = new(state, textures, crossed, true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        invalid.ApplyEnvironment45B660(1);
        Assert.Throws<NotSupportedException>(() => invalid.ExecuteSoftware(command));
    }
}
