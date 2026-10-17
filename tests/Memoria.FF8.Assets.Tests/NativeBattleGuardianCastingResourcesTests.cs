using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGuardianCastingResourcesTests
{
    [Fact]
    public void Selector1_uses_installed_resource4_quads_and_restores_its_physical_particles_and_trails()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleModuleResource> resources = game.ReadNativeBattleModuleResources();
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(resources[4].Bytes.AsSpan().Slice(8)));
        NativeCrtRandomState random = new(7);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 1);
        int callbacks = 0;
        bool sawOriginalQuads = false;
        bool checkedRestore = false;
        while (state.Head is not null && callbacks < 50)
        {
            NativeBattleModuleInputs inputs = new(0) { CrtRandom = random, Resources = resources, ReadModelAnchor = _ => new(10, 20, 30, 40), ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096) };
            NativeBattleModuleStep step = state.Advance(inputs);
            foreach (NativeBattleModuleDraw sprite in step.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite))
            {
                Assert.Equal((byte)4, sprite.Resource!.Id);
                Assert.InRange(sprite.Phase, (ushort)0, (ushort)7);
                Assert.NotNull(sprite.SpriteFrame);
                sawOriginalQuads |= !sprite.SpriteFrame!.Quads.IsEmpty;
            }
            if (callbacks == 22)
            {
                NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
                Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored.Advance(inputs)));
                checkedRestore = true;
            }
            state = step.State;
            random = step.CrtRandomAfter!;
            callbacks++;
        }
        Assert.True(sawOriginalQuads);
        Assert.True(checkedRestore);
        Assert.Equal(38, callbacks);
        Assert.Null(state.Head);
        Assert.Equal(0, state.PublishedCount);
    }
}
