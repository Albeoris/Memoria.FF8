using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModelDatSourceTests
{
    [Theory]
    [InlineData(0, false, 310, "d0c000.dat")]
    [InlineData(0, true, 312, "d0w000.dat")]
    [InlineData(1, false, 320, "d1c003.dat")]
    [InlineData(1, true, 322, "d1w008.dat")]
    public void Original_tables_select_actual_DAT_and_loader_prepares_and_copies_it_without_model_initialization(byte model, bool linked, short fileId, string name)
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleModelDatSource source = assets.ReadNativeModelDatSource(model, linked, 0);
        Assert.Equal(fileId, source.FileId);
        Assert.Equal(name, source.Name);
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080((ushort)(model | (linked ? 0x1000 : 0)), 0, 0, 0, 0);
        NativeBattleModelLoadStep request = loader.Poll507070(-1, [source]);
        Assert.Equal(fileId, request.FileRequest!.FileId);
        Assert.Equal(1, request.State.Poll507070(-1, [source]).Remaining);
        NativeBattleModelLoadStep prepared = request.State.Poll507070(source.Bytes.Length, [source]);
        Assert.NotEmpty(prepared.TextureUpload!.TimOffsets);
        NativeBattleModelLoadStep complete = prepared.State.Poll507070(source.Bytes.Length, [source]);
        Assert.NotNull(complete.Bind);
        Assert.Equal(0, complete.Remaining);
        Assert.Equal(source.Bytes.Length, complete.Bind.PreparedDat.Bytes.Length);
        Assert.Equal(linked ? (ushort)0x1000 : (ushort)3, prepared.TextureUpload.AllocatedSlots);
    }
}
