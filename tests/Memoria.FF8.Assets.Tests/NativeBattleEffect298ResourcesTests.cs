using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect298ResourcesTests
{
    [Fact]
    public void Original_declared_programme_table_meshes_and_sprites_have_complete_bounded_fronts()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEffect298Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect298Resources());
        var sources = new[] { resources.ProgrammeA, resources.ProgrammeB, resources.ProgrammeC, resources.ProgrammeD };
        int[] lengths = [2700, 4768, 3084, 3084];
        int[] nodes = [1, 3, 4, 4];
        int[] frames = [22, 22, 18, 18];
        int[] clears = [80, 304, 272, 272];
        HashSet<int> groups = [];
        int maximumMorph = 0;
        for (int index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            Assert.Equal(lengths[index], source.Data.Length);
            Assert.Equal(source.Address, Dword(resources.ProgrammeTable32, index * 8));
            Assert.Equal((uint)clears[index], Dword(resources.ProgrammeTable32, index * 8 + 4));
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal((uint)nodes[index], Dword(source.Data, table));
            Assert.Equal((uint)frames[index], Dword(source.Data, table + 4));
            for (int geometry = 0; geometry < Dword(source.Data, 0); geometry++)
            {
                int offset = checked((int)Dword(source.Data, 8 + geometry * 4));
                var mesh = resources.ReadGeometry(source.Address + (uint)offset);
                Assert.Equal(source.Data.AsSpan().Slice(offset, mesh.Bytes.Length).ToArray(), mesh.Bytes.ToArray());
                Assert.InRange(Dword(mesh.Bytes, 4), 1u, 64u);
                int cursor = checked((int)Dword(mesh.Bytes, 0));
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(mesh.Bytes, cursor);
                    if (count != 0)
                    {
                        groups.Add(group);
                    }
                    cursor = checked(cursor + 4 + (int)count * strides[group]);
                }
                Assert.Equal(mesh.Bytes.Length, cursor);
            }
            var workspace = new NativeBattleGfAnimationWorkspace(Enumerable.Repeat((byte)0xA5, 320).ToImmutableArray()).Initialize7016B0(source, clears[index]);
            for (int frame = 0; frame < frames[index]; frame++)
            {
                int callbacks = 0;
                workspace = workspace.Advance701970(source, 0, (retained, node) =>
                {
                    callbacks++;
                    if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                    {
                        int offset = checked((int)Dword(source.Data, 8 + node.GeometryIndex * 4));
                        maximumMorph = Math.Max(maximumMorph, checked((int)Dword(source.Data, offset + 4) * 8));
                    }
                    return retained;
                }).Workspace;
                Assert.Equal(nodes[index], callbacks);
                Assert.All(NativeBattleStorageSnapshots.Capture(workspace).Data.Skip(clears[index]), value => Assert.Equal((byte)0xA5, value));
            }
        }
        Assert.Equal(new[] { 2, 3, 4, 5, 7 }, groups.Order().ToArray());
        Assert.Equal(ScratchLength, maximumMorph);
        foreach (var mesh in resources.DebrisMeshes)
        {
            Assert.Equal(248, mesh.Bytes.Length);
            Assert.Equal(8u, Dword(mesh.Bytes, 4));
            Assert.Equal(12u, Dword(mesh.Bytes, 72));
        }
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(SpriteLengths[index], resources.Sprites[index].Bytes.Length);
            _ = resources.ReadSprite(SpriteAddresses[index]).ProjectedFrame571C80(SpriteFrames[index] - 1);
        }
    }

    [Fact]
    public void Original_script_counters_use_pristine_seed_then_retained_current_overlay_and_JSON()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEffect298Resources();
        int[] expected = [10, 5, 5, 5, 50, 1000, 30, 1000, 40, 1000];
        var cold = NativeBattleEffect298State.FromColdProcess();
        for (int index = 0; index < ScriptCounterAddresses.Length; index++)
        {
            uint address = ScriptCounterAddresses[index];
            Assert.Equal((ushort)0x10F0, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(address - 2, 2).AsSpan()));
            Assert.Equal((ushort)expected[index], BinaryPrimitives.ReadUInt16LittleEndian(cold.ReadStatic(resources, address, 2)));
        }
        Assert.Equal(500, resources.ReadScript(0x1056560).Length);
        Assert.Equal(148, resources.ReadScript(0x1056754).Length);
        Assert.Equal(148, resources.ReadScript(0x10567E8).Length);
        uint first = ScriptCounterAddresses[0];
        var current = cold.WriteWord(first, 0);
        var restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect298State>(NativeBattleSnapshotJson.Serialize(current))!;
        restored.Validate();
        Assert.Equal(new byte[2], restored.ReadStatic(resources, first, 2));
        Assert.Equal((ushort)10, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(first, 2).AsSpan()));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect298State.FromUnknown().ReadStatic(resources, first, 2));
        var resourceCopy = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect298Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        resourceCopy.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(resourceCopy));
        Assert.Equal(0x64230u, resourceCopy.Sound);
        Assert.Equal(304, resourceCopy.Vibration.Bank.Bytes.Length);
        Assert.Equal(new[] { 0, 256 }, resourceCopy.Vibration.Bank.Offsets.ToArray());
        Assert.Equal(360, resourceCopy.CompletionFile.FileId);
        Assert.Equal(33824, resourceCopy.CompletionFile.Data.Length);
        Assert.Equal(16384, resourceCopy.TrigPairs16384.Length);
        Assert.Equal(384, resourceCopy.NormalizationConsumed384.Length);
        _ = resourceCopy.ReadStatic(TrigAddress + 4095 * 4, 4);
        _ = resourceCopy.ReadNormalizationWord(64);
        _ = resourceCopy.ReadNormalizationWord(255);
        Assert.Throws<NotSupportedException>(() => resourceCopy.ReadNormalizationWord(63));
        Assert.Throws<NotSupportedException>(() => resourceCopy.ReadStatic(NormalizationAddress + NormalizationLength - 1, 2));
        Assert.Throws<NotSupportedException>(() => resourceCopy.ReadStatic(ArenaAddress + ScratchOffset, 8));
    }

    [Fact]
    public void Changed_programme_directory_faces_counter_reset_and_completion_fronts_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEffect298Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] source = resources.ProgrammeA.Data.ToArray();
        int offset = checked((int)Dword(resources.ProgrammeA.Data, 8));
        int polygon = checked(offset + (int)Dword(resources.ProgrammeA.Data, offset));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            uint count = Dword(resources.ProgrammeA.Data, polygon);
            if (count != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(polygon + 8), ushort.MaxValue);
                break;
            }
            polygon = checked(polygon + 4 + (int)count * strides[group]);
        }
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = source.ToImmutableArray() } }).Validate());
        byte[] scripts = resources.ScriptBytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(scripts.AsSpan(checked((int)(ScriptCounterAddresses[0] - ScriptAddress))), 11);
        Assert.Throws<InvalidDataException>(() => (resources with { ScriptBytes = scripts.ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeTable32 = resources.ProgrammeTable32.SetItem(0, 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprites = resources.Sprites.SetItem(0, resources.Sprites[0] with { Bytes = resources.Sprites[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { CompletionFile = resources.CompletionFile with { FileId = 361 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { NormalizationConsumed384 = resources.NormalizationConsumed384.RemoveAt(0) }).Validate());
        Assert.Throws<NotSupportedException>(() => resources.ReadScript(CameraAddress));
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect298 resources.");
        return new(directory!);
    }
}
