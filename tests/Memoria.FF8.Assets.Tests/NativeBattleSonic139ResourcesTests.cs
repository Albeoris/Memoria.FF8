using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSonic139ResourcesTests
{
    [Fact]
    public void Original_fronts_preserve_exact_copy_ACT_workspace_camera_and_adjacent_palette_bytes()
    {
        using GameAssets game = Open();
        NativeBattleSonic139Resources resources = game.ReadNativeSonic139Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeSonic139Resources());
        Assert.Equal(0x9080, resources.EntryCopySeed.Data.Length);
        Assert.Equal(0x12F0, resources.FramesSeed.Data.Length);
        Assert.Equal(0x26AC, resources.MainAct.Data.Length);
        Assert.Equal(0x1F80, resources.TargetAct.Data.Length);
        Assert.Equal(0x11C, resources.Camera.Data.Length);
        Assert.Equal(0x3167616Du, resources.Colors[3]);
        Assert.Equal(0x641C8u, resources.Sound);
        Assert.Equal(4096, resources.SineWords4096.Length);
        Assert.Throws<NotSupportedException>(() => resources.Read(MutableFramesAddress, 4));
        NativeBattleSonic139Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleSonic139Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restored));
    }

    [Fact]
    public void Cold_frame_publication_and_warm_copy_preserve_actual_original_preprocessing_bytes()
    {
        using GameAssets game = Open();
        NativeBattleSonic139Resources resources = game.ReadNativeSonic139Resources();
        NativeBattleSonic139State state = NativeBattleSonic139State.FromColdProcess().CopyEntrySnapshot6A8860(resources);
        uint originalFrameWord = state.ReadDword(MutableFramesAddress + 12);
        NativeBattleSonic139Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), new(139, 138, new(139, Entry, Initializer), null, 0), new(0), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleSonic139Frame after = NativeBattleSonic139Preprocessing.Initialize(frame, resources);
        Assert.NotNull(after.Memory.GeometryRegisters!.Lzcs);
        Assert.NotNull(after.Memory.GeometryRegisters.Lzcr);
        Assert.NotEqual(originalFrameWord, after.State.ReadDword(MutableFramesAddress + 12));
        NativeBattleSonic139State warm = after.State.CopyEntrySnapshot6A8860(resources);
        Assert.Equal(after.State.Read(MutableFramesAddress, MutableFramesLength), warm.Read(CopiedMeshAddress + MutableFramesAddress - SourceMeshAddress, MutableFramesLength));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(after.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(0x2517B5Cu, BitConverter.ToUInt32(after.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.Equal(0x2517B5Cu + 0x2420 - 12, BitConverter.ToUInt32(after.Staging.Read(NativeBattleTemporaryMemory.Address + 32, 4)));
    }

    [Fact]
    public void Changed_copy_extent_frame_seed_ACT_and_palette_fail_fast()
    {
        using GameAssets game = Open();
        NativeBattleSonic139Resources resources = game.ReadNativeSonic139Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MainAct = resources.MainAct with { Data = resources.MainAct.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors.RemoveAt(3) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { FramesSeed = resources.FramesSeed with { Data = resources.FramesSeed.Data.SetItem(12, (byte)(resources.FramesSeed.Data[12] ^ 1)) } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Sonic Wave resources.");
        return new(directory!);
    }
}
