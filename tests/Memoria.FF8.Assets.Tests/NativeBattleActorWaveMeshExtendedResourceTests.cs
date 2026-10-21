using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleActorWaveMeshExtendedResourceTests
{
    [Theory]
    [InlineData(73)]
    [InlineData(78)]
    public void All_original_Mighty_meshes_preserve_Esuna_physical_packets_and_canonical_registers(ushort effect)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to validate addressed meshes.");
        using GameAssets game = new(directory!);
        NativeBattleMightyGuardResources resources = game.ReadNativeMightyGuardResources(effect);
        ImmutableArray<byte> source = resources.NodeSource.Data;
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 30000 };
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleEsunaMeshContext packetContext = new(0xF0, 0xC08040, 2048, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
        HashSet<int> reached = [];
        int accepted = 0;
        for (int index = 0; index < 4; index++)
        {
            int offset = checked((int)Read(0x84 + index * 4));
            int cursor = checked((int)Read(offset));
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int count = checked((int)Read(offset + cursor));
                if (count != 0)
                {
                    reached.Add(group);
                }
                cursor = checked(cursor + 4 + count * strides[group]);
            }
            ImmutableArray<byte> mesh = source.AsSpan().Slice(offset, cursor).ToArray().ToImmutableArray();
            uint address = checked(resources.NodeSource.ResourceAddress + (uint)offset);
            NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 104), address, 0, 2048, 0xC08040);
            NativeBattleActorWaveMeshStep result = NativeBattleActorWaveMesh.EmitBatch(mesh, address, matrix, projection, context, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), 0, 0, 0x100, constants, 0x1234, 0x155);
            NativeBattleGfGeometryBatch expected = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, packetContext, constants);
            Assert.Equal(JsonSerializer.Serialize(Payload(expected)), JsonSerializer.Serialize(Payload(result.Batch)));
            Assert.Equal(address + (uint)mesh.Length, BinaryPrimitives.ReadUInt32LittleEndian(result.Context104.Read(0x2C, 4)));
            for (int lane = 0; lane < 9; lane++)
            {
                ushort expectedLane = lane is 0 or 4 or 8 ? (ushort)4096 : (ushort)0;
                Assert.Equal((byte)expectedLane, result.Registers.Geometry.Rotation20[lane * 2]);
                Assert.Equal((byte)(expectedLane >> 8), result.Registers.Geometry.Rotation20[lane * 2 + 1]);
            }
            Assert.Equal((byte)0x34, result.Registers.Geometry.Rotation20[18]);
            Assert.Equal((byte)0x12, result.Registers.Geometry.Rotation20[19]);
            Assert.Equal(new int?[] { 0, 0, 30000 }, result.Registers.Geometry.Translation);
            Assert.Equal(0x400u, result.FarControls.FarRed);
            Assert.Equal(0x800u, result.FarControls.FarGreen);
            Assert.Equal(0xC00u, result.FarControls.FarBlue);
            foreach (NativeBattleGfGeometryAttempt attempt in result.Batch.Attempts)
            {
                if (attempt.Primitive is not null)
                {
                    accepted++;
                    Assert.True(attempt.Primitive.Bucket >= 17);
                }
            }
        }
        Assert.Equal(new[] { 3, 4, 5, 7 }, reached.Order().ToArray());
        Assert.True(accepted > 0);
        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(offset));
    }

    private static NativeBattleGfGeometryBatch Payload(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is null ? attempt : attempt with { Primitive = attempt.Primitive with { Bucket = 0 } }).ToImmutableArray() };
    }
}
