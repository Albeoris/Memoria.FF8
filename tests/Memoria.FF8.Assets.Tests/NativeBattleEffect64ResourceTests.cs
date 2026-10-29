using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect64ResourceTests
{
    [Fact]
    public void Original_fronts_preserve_inline_selectors_and_exclude_current_script_counters()
    {
        using GameAssets game = Open();
        NativeBattleEffect64Resources resources = game.ReadNativeEffect64Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(18, resources.OriginalScripts.Count);
        Assert.Equal(new byte[] { 0x30, 0, 0, 0 }, resources.ReadStatic(ProgrammeDirectoryAddress + 8, 4).ToArray());
        Assert.Equal(new byte[] { 0x30, 0, 0, 0 }, resources.ReadStatic(ProgrammeDirectoryAddress + 20, 4).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ScriptAddresses[0], 2));
        NativeBattleEffect64State state = NativeBattleEffect64State.FromColdProcess().BindScriptSource(resources, ScriptAddresses[0]);
        Assert.Equal((ushort)39, state.ReadWord(ScriptAddresses[0]));
        state = state.WriteWord(ScriptAddresses[0] + 4, 3).BindScriptSource(resources, ScriptAddresses[0]);
        Assert.Equal((ushort)3, state.ReadWord(ScriptAddresses[0] + 4));
    }

    [Fact]
    public void All_original_ACT_node_frames_publish_equal_zero_shape_words()
    {
        using GameAssets game = Open();
        NativeBattleEffect64Resources resources = game.ReadNativeEffect64Resources();
        int emissions = 0;
        for (int index = 0; index < 2; index++)
        {
            NativeBattleGfAnimationSource source = resources.Act(ProgrammeAddresses[index]);
            int clear = ProgrammeWorkspaceLengths[index];
            NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, clear + 8).ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, clear);
            Assert.All(NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan().Slice(clear).ToArray(), value => Assert.Equal((byte)0xA5, value));
            int frames = index == 0 ? 12 : 60;
            for (int frame = 0; frame < frames; frame++)
            {
                NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
                {
                    Assert.Equal((short)0, node.Word40);
                    Assert.Equal((short)0, node.Word42);
                    Assert.Equal(0u, node.AnimationFlags & 0x2000);
                    uint address = source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                    Assert.Equal(address, resources.ReadGeometry(address).Address);
                    emissions++;
                    return current;
                });
                Assert.Equal(frames - frame - 1, advanced.Remaining);
                workspace = advanced.Workspace;
            }
        }
        Assert.Equal(1104, emissions);
    }

    [Fact]
    public void Selected_catalog_and_json_keep_original_inputs_distinct_from_current_script_owner()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([64])[63];
        NativeBattleEffect64Resources resources = game.ReadNativeEffect64Resources();
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Same(resources, binding.Effect64Resources);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag063.tim", InitializerGlobal), binding.InitializerResource);
        NativeBattleEffect64Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect64Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect64State.FromUnknown().BindScriptSource(restored, ScriptAddresses[0]));
        Assert.Throws<InvalidDataException>(() => (restored with { ProgrammeDescriptors = restored.ProgrammeDescriptors.SetItem(8, (byte)'1') }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect64 resources.");
        return new(directory!);
    }
}
