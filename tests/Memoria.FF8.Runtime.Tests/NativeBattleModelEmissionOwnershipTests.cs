using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelEmissionOwnershipTests
{
    [Fact]
    public void Mixed_model_emissions_keep_their_metadata_across_packet_and_pilot_pages()
    {
        NativeBattleModelTextureOwner material = new("party-10");
        NativeBattleSceneCapture capture = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.Image);
        NativeBattlePilotVisualIdentity first = NativeBattlePilotVisualIdentity.ForModel(0, 0x502D40, new BattlePartyActorModel(new("party-10")), 41) with { ResourceGeneration = 17 };
        NativeBattlePilotVisualIdentity second = new(84, 0x20DFAB8, 8, 10, 52) { ResourceGeneration = 19 };
        NativeBattlePilotGeometryReference[] retained = BuildMixedEmissions();
        Assert.Equal(first with { Primitive = 200 }, retained[0].Identity);
        Assert.True(retained[0].Continuous);
        Assert.False(retained[0].ScreenSpace);
        Assert.Equal(second with { Primitive = 801 }, retained[1].Identity);
        Assert.Equal(second with { Primitive = 807 }, retained[2].Identity);
        Assert.False(retained[2].Continuous);
        Assert.True(retained[2].ScreenSpace);
        Assert.Equal(new Vector3(9, 10, 11), retained[2].Corner(3));

        NativeBattlePilotGeometryReference[] BuildMixedEmissions()
        {
            NativeBattleModelPrimitiveBatch.Builder builder = new();
            int model = builder.BeginEmission(new("party-10", material, null, first, true, false));
            for (int index = 0; index < 63; index++)
            {
                Append(model, 138 + index);
            }
            int captured = builder.BeginEmission(new("enemy-9", null, capture, second, false, true));
            Append(captured, 801);
            Append(captured, 807);
            int withoutPilot = builder.BeginEmission(new("enemy-10", null, null, default, false, false));
            builder.AddRaw(27, [0x04000000, 0x200000FF, 0, 8, 8u << 16], [100, 100, 100, 0], [], [], default, withoutPilot);

            NativeBattleModelPrimitiveBatch batch = builder.Freeze();
            Assert.Equal(66, batch.Count);
            Assert.Same(material, batch.ReadModelTextureOwner(62));
            Assert.Null(batch.ReadSceneCapture(62));
            Assert.Equal("party-10", batch.ReadPresentationActorId(62));
            for (int index = 63; index < 65; index++)
            {
                Assert.Same(capture, batch.ReadSceneCapture(index));
                Assert.Null(batch.ReadModelTextureOwner(index));
                Assert.Equal("enemy-9", batch.ReadPresentationActorId(index));
            }
            Assert.Equal("enemy-10", batch.ReadPresentationActorId(65));
            Assert.True(batch.ReadPilot(65).IsEmpty);
            Assert.Throws<InvalidOperationException>(() => builder.BeginEmission(default));
            return [batch.ReadPilot(62), batch.ReadPilot(63), batch.ReadPilot(64)];

            void Append(int emission, int primitive)
            {
                NativeBattlePilotGeometryReference pilot = builder.AddPilot(new(new(0, 1, 2), new(3, 4, 5), new(6, 7, 8), new(9, 10, 11), primitive, emission, 4));
                builder.AddRaw(23, [0x04000000, 0x200000FF, 0, 8, 8u << 16], [100, 100, 100, 0], [], [], pilot, emission);
            }
        }
    }
}
