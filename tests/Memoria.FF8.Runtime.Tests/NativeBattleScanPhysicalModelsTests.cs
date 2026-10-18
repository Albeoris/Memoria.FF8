using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanPhysicalModelsTests
{
    [Fact]
    public void Physical_view_reuses_current_flags_rings_yaw_and_position_padding_without_filling_unknown_resource_fields()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray();
        models = models.SetItem(4, new NativeBattleGraphicsModel(4, 0xA123)
        {
            ModelId4 = 0x36,
            Rotation14 = -200,
            Transform = new(new(1, 2, 3), new(-4, -5, -6), 0x1234, 0x2345, 0x3456),
            Color40 = 0xAB123456
        }.WriteLinkedModelIndex140(5));
        ImmutableArray<NativeBattleScanByteOwner> physical = NativeBattleScanPhysicalModels.Capture(models);
        Assert.Equal((ushort)0xA123, physical[4].ReadWord(0));
        Assert.Equal((byte)0x36, physical[4].ReadByte(4));
        Assert.Equal((short)-200, unchecked((short)physical[4].ReadWord(0x0E)));
        Assert.Equal(0x1234FFFAu, physical[4].ReadDword(0x20));
        Assert.Equal(0x1D972C0u + 5 * 156, physical[4].ReadDword(0x8C));
        Assert.Equal(0xAB123456u, physical[4].ReadDword(0x28));
        Assert.Throws<NotSupportedException>(() => physical[4].ReadDword(0x74));
        Assert.Throws<NotSupportedException>(() => physical[4].ReadDword(0x7C));
        Assert.Throws<NotSupportedException>(() => physical[0].ReadByte(4));
        Assert.Throws<NotSupportedException>(() => physical[0].ReadDword(0x8C));
        Assert.Equal((ushort)0, physical[0].ReadWord(0));
        Assert.Equal(0xAB123456u, models[4].Color40);
    }
}
