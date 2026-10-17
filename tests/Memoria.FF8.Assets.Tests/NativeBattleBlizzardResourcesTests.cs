using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBlizzardResourcesTests
{
    [Fact]
    public void OriginalBlizzardAction144BindsOnlyRequestedInitializerAndDirectMeshes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 4);
        Assert.Equal("Blizzard", action.Name);
        Assert.Equal((ushort)144, action.NativeAction!.MagicId);
        var catalog = game.ReadNativeEffectCatalogForEffects([144]);
        NativeBattleEffectBinding binding = catalog[143];
        Assert.Equal(0x61ABA0u, binding.Entry);
        Assert.Equal(0x61AB80u, binding.Initializer);
        Assert.Equal("mag143.tim", binding.InitializerAsset!.Name);
        NativeBattleBlizzardResources resources = binding.BlizzardResources!;
        Assert.Equal(0x9D8, resources.Animation.Data.Length);
        Assert.Equal(300012u, resources.Sound);
        Assert.Equal(5, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite5.Bytes.AsSpan().Slice(8)));
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite16.Bytes.AsSpan().Slice(8)));
        Assert.Equal(new uint[] { 0xDD8590, 0xDD9280, 0xDD8D48 }, new[] { resources.PrimaryMesh.Address, resources.PartnerMesh.Address, resources.BurstMesh.Address });
        Assert.Equal(new[] { 1976, 1976, 1336 }, new[] { resources.PrimaryMesh.Bytes.Length, resources.PartnerMesh.Bytes.Length, resources.BurstMesh.Bytes.Length });
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(resources.PrimaryMesh.Bytes.AsSpan().Slice(4)));
        Assert.Equal(54, BinaryPrimitives.ReadInt32LittleEndian(resources.BurstMesh.Bytes.AsSpan().Slice(4)));
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), NativeBattleOriginalEffects.Select(144, catalog));
        Assert.Equal(0x24BECB8u, Assert.Single(initialized.Effect.ResourcePointers).GlobalAddress);
        NativeBattleEffectTextureState textures = initialized.Memory.Textures!.Register5099A0(resources.Animation).EnqueueArenaTim505E30(initialized.Memory.Arena!, 0).State;
        Assert.Equal(2, textures.Drain505D20(initialized.Memory.Arena).Uploads.Length);
    }

    [Fact]
    public void Original80FaceInitializationAndActualPartnerRecordUseCurrentPacketCore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBlizzardResources resources = game.ReadNativeBlizzardResources();
        var initialized = NativeBattleBlizzardFaceMotion.Initialize61B190(null, 0, resources.PrimaryMesh, new(1));
        Assert.Equal(2560, initialized.Faces.Read(0).Length);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 80; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, initialized.Random);
        int primary = BinaryPrimitives.ReadInt32LittleEndian(resources.PrimaryMesh.Bytes.AsSpan()) + 12;
        int partner = BinaryPrimitives.ReadInt32LittleEndian(resources.PartnerMesh.Bytes.AsSpan()) + 12;
        NativeBattleBlizzardFacePacketContext context = new(0x24808080, 0, null, null, 8, new(0, 128, 128, 128), new(0, 0, 0, 0), 17, 2);
        ImmutableArray<NativeBattleModuleProjectedVertex> projected = [new(0x01000100, 4000, 0), new(0x01000180, 4000, 0), new(0x01800100, 4000, 0)];
        NativeBattleGfGeometryBatch packets = NativeBattleBlizzardFacePackets.EmitFace61B9A0(resources.PrimaryMesh.Bytes.AsSpan().Slice(primary, 20).ToArray().ToImmutableArray(), resources.PartnerMesh.Bytes.AsSpan().Slice(partner, 20).ToArray().ToImmutableArray(), projected, 12, 12, context);
        Assert.Equal(72, packets.AllocationBytes);
        Assert.Equal(2, packets.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Equal(0x09000000u, packets.Attempts[^1].Primitive!.Words[0]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, packets.Attempts[^1].Primitive!.VertexPayload4);
    }
}
