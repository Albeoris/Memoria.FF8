using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleActorWaveMeshResourceTests
{
    [Theory]
    [InlineData(81, 6)]
    [InlineData(82, 5)]
    public void Original_actor_wave_meshes_reuse_the_exact_restoration_packets_after_bank_restore(ushort effect, int expectedMeshes)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleActorDirectorResources resources = game.ReadNativeActorDirectorResources(effect);
        NativeBattleCuragaExecutableState original = NativeBattleCuragaExecutableState.FromColdProcess(effect).Initialize87EA20(resources.NodeSource);
        NativeBattleCuragaExecutableState bank = NativeBattleSnapshotJson.Deserialize<NativeBattleCuragaExecutableState>(NativeBattleSnapshotJson.Serialize(original))!;
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(null, 216, 160, 108);
        NativeBattleSummonColors constants = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleEsunaMeshContext expectedContext = new(0x30, null, null, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
        int meshes = 0;
        for (int index = 0; index < 16; index++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(bank.ResourceAddress + 4 + 128 + (uint)index * 4, 4));
            if (address == 0)
            {
                continue;
            }
            meshes++;
            ImmutableArray<byte> mesh = bank.Read(address, checked(bank.ByteLength - (int)(address - bank.ResourceAddress))).ToImmutableArray();
            NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 104), address, 0, 0, null);
            string contextJson = NativeBattleSnapshotJson.Serialize(context);
            NativeBattleActorWaveMeshStep actual = NativeBattleActorWaveMesh.EmitBatch(mesh, address, matrix, projection, context, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(0, 0, 0, 0, 0), 0, 0, 0x100, constants, 0);
            NativeBattleGfGeometryBatch expected = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, expectedContext, constants);
            Assert.NotEmpty(expected.Attempts);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(expected), NativeBattleSnapshotJson.Serialize(actual.Batch));
            NativeBattleActorWaveMeshStep restored = NativeBattleActorWaveMesh.EmitBatch(mesh, address, matrix, projection, NativeBattleSnapshotJson.Deserialize<NativeBattleEffectArena>(contextJson)!, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(0, 0, 0, 0, 0), 0, 0, 0x100, constants, 0);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(actual), NativeBattleSnapshotJson.Serialize(restored));
        }
        Assert.Equal(expectedMeshes, meshes);
    }
}
