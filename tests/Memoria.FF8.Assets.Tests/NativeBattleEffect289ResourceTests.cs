using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect289ResourceTests
{
    [Fact]
    public void Original_programmes_keep_ACT_headers_cache_extents_morph_and_complete_durations()
    {
        using GameAssets game = Open();
        NativeBattleEffect289Resources resources = game.ReadNativeEffect289Resources();
        resources.Validate();
        NativeBattleGfAnimationSource[] sources = [resources.MainProgramme, resources.ChildProgramme];
        int[] clears = [388, 64];
        int[] frames = [20, 15];
        int[] nodes = [4, 1];
        int maximumMorph = 0;
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            NativeBattleGfAnimationSource source = sources[sourceIndex];
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                NativeBattleStaticMeshResource geometry = resources.ReadGeometry(source.Address + Dword(source.Data, 8 + index * 4));
                uint vertices = Dword(geometry.Bytes, 4);
                uint shapes = Dword(geometry.Bytes, 8);
                Assert.Equal(12 + vertices * shapes * 8, Dword(geometry.Bytes, 0));
                if (shapes > 1)
                {
                    maximumMorph = Math.Max(maximumMorph, checked((int)vertices * 8));
                }
            }
            NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, clears[sourceIndex] + 16).ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, clears[sourceIndex]);
            for (int frame = 0; frame < frames[sourceIndex]; frame++)
            {
                int callbacks = 0;
                workspace = workspace.Advance701970(source, 0, (retained, node) =>
                {
                    callbacks++;
                    return retained;
                }).Workspace;
                Assert.Equal(nodes[sourceIndex], callbacks);
                Assert.Equal(frame + 1, workspace.Frame);
                Assert.All(workspace.Data.Skip(clears[sourceIndex]), value => Assert.Equal((byte)0xA5, value));
            }
        }
        Assert.Equal(544, maximumMorph);
        Assert.Equal(new short[] { 1129, 0, -933, 9 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.CasterPoint8.AsSpan().Slice(index * 2))).ToArray());
        for (int sprite = 0; sprite < 3; sprite++)
        {
            for (int frame = 0; frame < (sprite == 2 ? 16 : 12); frame++)
            {
                Assert.Equal(20, resources.Sprites[sprite].ProjectedFrame571C80(frame).Quads.Length);
            }
        }
    }

    [Fact]
    public void Actual_W_writes_preserve_source_XYZ_faces_and_the_exact_inclusive_lookup_domain()
    {
        using GameAssets game = Open();
        NativeBattleEffect289Resources resources = game.ReadNativeEffect289Resources();
        NativeBattleEffect289State state = NativeBattleEffect289State.FromUnknown();
        for (int meshIndex = 0; meshIndex < 2; meshIndex++)
        {
            NativeBattleStaticMeshResource original = resources.ReadMesh(MeshAddresses[meshIndex]);
            Assert.Equal((uint)MeshVertexCount, Dword(original.Bytes, 4));
            Assert.Equal(new short[] { 0, 119 }, Enumerable.Range(0, MeshVertexCount).Select(vertex => BinaryPrimitives.ReadInt16LittleEndian(original.Bytes.AsSpan().Slice(14 + vertex * 8))).Distinct().Order().ToArray());
            Assert.Throws<NotSupportedException>(() => resources.ReadMesh(state, original.Address));
            for (int vertex = 0; vertex < MeshVertexCount; vertex++)
            {
                short z = BinaryPrimitives.ReadInt16LittleEndian(original.Bytes.AsSpan().Slice(12 + vertex * 8));
                state = state.WriteMeshW(original.Address, vertex, unchecked((short)-z));
            }
            NativeBattleStaticMeshResource current = resources.ReadMesh(state, original.Address);
            for (int vertex = 0; vertex < MeshVertexCount; vertex++)
            {
                Assert.Equal(original.Bytes.AsSpan().Slice(8 + vertex * 8, 6).ToArray(), current.Bytes.AsSpan().Slice(8 + vertex * 8, 6).ToArray());
                short w = BinaryPrimitives.ReadInt16LittleEndian(current.Bytes.AsSpan().Slice(14 + vertex * 8));
                Assert.InRange(w, (short)0, (short)4080);
                _ = resources.ReadLookup(w);
                _ = resources.ReadLookup(w >> 1);
            }
            int polygons = checked((int)Dword(original.Bytes, 0));
            Assert.Equal(original.Bytes.AsSpan().Slice(polygons).ToArray(), current.Bytes.AsSpan().Slice(polygons).ToArray());
        }
        Assert.Equal(8162, resources.Lookup8162.Length);
        Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(resources.Lookup8162.AsSpan().Slice(8160)), resources.ReadLookup(4080));
        Assert.Throws<InvalidDataException>(() => resources.ReadLookup(-1));
        Assert.Throws<InvalidDataException>(() => resources.ReadLookup(4081));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(LookupAddress + LookupLength, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(TextureNameAddress, 1));
    }

    [Fact]
    public void Resource_and_current_overlay_JSON_preserve_owners_and_reject_corruption()
    {
        using GameAssets game = Open();
        NativeBattleEffect289Resources resources = game.ReadNativeEffect289Resources();
        NativeBattleEffect289Resources restored = JsonSerializer.Deserialize<NativeBattleEffect289Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Equal(0x61B85u, resources.Sound);
        Assert.Throws<InvalidDataException>(() => (resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = resources.Meshes[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MainProgramme = resources.MainProgramme with { Data = resources.MainProgramme.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Lookup8162 = resources.Lookup8162.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { CasterPoint8 = resources.CasterPoint8.SetItem(6, 8) }).Validate());
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(MeshAddresses[0]));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SoundAddress + 4, 4));
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect289 resources.");
        return new(directory!);
    }
}
