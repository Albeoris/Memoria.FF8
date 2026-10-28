using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect79ResourceTests
{
    [Fact]
    public void Original_files_programmes_music_and_node_banks_close_the_selected_resource_fronts()
    {
        using GameAssets game = Open();
        NativeBattleEffect79Resources resources = game.ReadNativeEffect79Resources();
        resources.Validate();
        Assert.Equal(new[] { 5292, 4604, 4544, 6092, 33312, 136136 }, Enumerable.Range(396, 6).Select(index => resources.ReadFile(index).Data.Length).ToArray());
        Assert.Equal(new[] { 31512, 6916, 7176 }, new uint[] { 0x1762E40, 0x176A958, 0x176C45C }.Select(address => resources.ReadNodeBank(address).Length).ToArray());
        Assert.Equal(168992, resources.MainTim.Data.Length);
        Assert.Equal(804, resources.Camera.Data.Length);
        Assert.Equal(304, resources.Vibration.Data.Length);
        Assert.Equal((short)56, resources.PointSelectors.Max());
        int[] nodes = [3, 3, 3, 6];
        int[] frames = [15, 40, 40, 40];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleGfAnimationSource source = resources.ReadProgramme(ProgrammeAddresses[index]);
            int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
            Assert.Equal(nodes[index], BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)));
            Assert.Equal(frames[index], BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)));
        }
        NativeBattleMusicLoadSource music = NativeBattleMusicLoadSource.Read501A20(resources.ReadFile(401).Data);
        Assert.Equal(131504, music.SoundBank.Length);
        Assert.Equal(4616, music.Music.Length);
        Assert.Equal((byte)77, music.ReadIdentity().EncodedAkaoId);
    }

    [Fact]
    public void Node_and_script_binding_is_lazy_and_retains_current_writes_between_entries()
    {
        using GameAssets game = Open();
        NativeBattleEffect79Resources resources = game.ReadNativeEffect79Resources();
        NativeBattleEffect79State state = NativeBattleEffect79State.FromColdProcess().BindNodeSource(0x176A958, resources).BindProgram(resources);
        Assert.Single(state.NodeBanks);
        Assert.Throws<NotSupportedException>(() => state.ReadNodeBank(0x1762E40));
        state = state.WriteDword(0x176A958, 1).WriteDword(ScriptAddress + 28, 0x19F1018);
        Assert.Same(state, state.BindProgram(resources));
        Assert.Same(state, state.BindNodeSource(resources, 0x176A958));
        Assert.Equal(0x19F1018u, state.ReadDword(ScriptAddress + 28));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadNodeBank(0x176A958).AsSpan()));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79State.FromUnknown().BindProgram(resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79State.FromUnknown().BindNodeSource(0x176A958, resources));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffect79State>(json)));
    }

    [Fact]
    public void Resource_roundtrip_rejects_unselected_files_missing_banks_and_changed_music_identity()
    {
        using GameAssets game = Open();
        NativeBattleEffect79Resources resources = game.ReadNativeEffect79Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleEffect79Resources restored = JsonSerializer.Deserialize<NativeBattleEffect79Resources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(402));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(0x19F1019));
        Assert.Throws<InvalidDataException>(() => (resources with { NodeBanks = resources.NodeBanks.Remove(0x176A958) }).Validate());
        NativeBattleEffectFileAsset music = resources.ReadFile(401);
        Assert.Throws<InvalidDataException>(() => (resources with { Files = resources.Files.SetItem(401, music with { Data = music.Data.SetItem(131524, 0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { IoScript = resources.IoScript with { Data = resources.IoScript.Data.RemoveAt(0) } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect79 resources.");
        return new(directory!);
    }
}
