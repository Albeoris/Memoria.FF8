using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSpectralCastingResourcesTests
{
    [Theory]
    [InlineData(2, 0x3B27)]
    [InlineData(3, 0x3C67)]
    public void Original_resources_run_the_whole_shared_module_cycle_and_emit_mixed_FT4_GT4_packets(int selector, ushort material)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        var meshes = game.ReadNativeEffectMeshes();
        NativeBattleStaticMeshResource mesh2 = meshes.Single(mesh => mesh.Id == 2);
        NativeBattleStaticMeshResource mesh3 = meshes.Single(mesh => mesh.Id == 3);
        Assert.Equal(0xC7F1D8u, mesh2.Address);
        Assert.Equal(1288, mesh2.Bytes.Length);
        Assert.Equal("59D0E7B837A8E6CC9E0C5C926625EF086BE83450A225CE75F6C9B5B6288A7C67", Convert.ToHexString(SHA256.HashData(mesh2.Bytes.AsSpan())));
        Assert.Equal(0xC7F6E0u, mesh3.Address);
        Assert.Equal(1432, mesh3.Bytes.Length);
        Assert.Equal("2DA304E49B7C9A0ED34DE81773781863E129EFFF347461973FB4C75FA31F3A77", Convert.ToHexString(SHA256.HashData(mesh3.Bytes.AsSpan())));
        var resources = game.ReadNativeBattleModuleResources();
        NativeBattleModuleResource sprite = resources.Single(resource => resource.Id == 6);
        Assert.Equal(0xC79344u, sprite.Address);
        Assert.Equal(320, sprite.Bytes.Length);
        Assert.Equal((ushort)11, BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)));
        Assert.Equal("717F10F8251B72A222E0CBB3DD27A1AD48C0F3B7B7E135F5B9D1444C3C4E4610", Convert.ToHexString(SHA256.HashData(sprite.Bytes.AsSpan())));
        NativeBattleSummonColors colors = Assert.IsType<NativeBattleSummonColors>(game.ReadNativeBattleGraphicsRules().SummonColors);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeCrtRandomState random = new(17);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, (byte)selector);
        int callbacks = 0;
        bool mixed = false;
        bool originalSprite = false;
        while (state.Head is not null && callbacks < 64)
        {
            NativeBattleModuleState clone = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleInputs inputs = new(0) { CrtRandom = random, ReadModelAnchor = _ => new(100, -700, 300, 0), ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, -500, 4096), Resources = resources };
            NativeBattleModuleStep step = state.Advance(inputs);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(clone.Advance(inputs)));
            foreach (NativeBattleModuleDraw draw in step.Draws)
            {
                if (draw.Kind == NativeBattleModuleDrawKind.Mesh)
                {
                    NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitMesh572200(draw, meshes.Single(mesh => mesh.Id == draw.MeshId), projection, 17, 2, colors);
                    Assert.NotNull(batch.CapturedWrites);
                    if (draw.MeshId == 3)
                    {
                        Assert.Equal(material, draw.MeshClut);
                        Assert.Contains(batch.Primitives, packet => (packet.Primitive.Words[1] >> 24 & 0xFC) == 0x2C);
                        Assert.Contains(batch.Primitives, packet => (packet.Primitive.Words[1] >> 24 & 0xFC) == 0x3C);
                        Assert.All(batch.Primitives, packet => Assert.Equal(material, unchecked((ushort)(packet.Primitive.Words[3] >> 16))));
                        mixed = true;
                    }
                }
                else if (draw.Kind == NativeBattleModuleDrawKind.Sprite)
                {
                    Assert.Same(sprite, draw.Resource);
                    Assert.Equal((ushort)0x210, draw.SpriteFlags);
                    Assert.Equal(new byte[] { 4, 0, 0, 0 }, draw.SpritePaletteOffsets);
                    NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2);
                    originalSprite |= !batch.Primitives.IsEmpty;
                }
            }
            state = step.State;
            random = step.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(31, callbacks);
        Assert.Null(state.Head);
        Assert.Equal(0, state.PublishedCount);
        Assert.True(mixed);
        Assert.True(originalSprite);
    }
}
