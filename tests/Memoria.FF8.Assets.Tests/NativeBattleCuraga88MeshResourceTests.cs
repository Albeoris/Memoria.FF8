using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuraga88MeshResourceTests
{
    [Fact]
    public void All_original_DrinkMagic_meshes_preserve_Curaga_packets_and_restore_full_register_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to validate addressed meshes.");
        using GameAssets game = new(directory!);
        NativeBattleDrinkMagic37Resources resources = game.ReadNativeDrinkMagic37Resources();
        ImmutableArray<byte> source = resources.Executable.Data;
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 30000 };
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleCuragaMeshContext packetContext = new(0xF0, 0xC08040, 2048, null, null, null, 17, 2);
        HashSet<int> reached = [];
        int accepted = 0;
        foreach (int index in new[] { 4, 6, 7, 8 })
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
            uint address = checked(resources.Executable.ResourceAddress + (uint)offset);
            NativeBattleEffectArena context = NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), address, 0, 2048, 0xC08040);
            NativeBattleCuraga88MeshStep result = NativeBattleCuraga88Mesh.EmitBatch(mesh, address, matrix, projection, context, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), 0, 0, 0x100, constants, 0x1234, 0x155);
            NativeBattleGfGeometryBatch expected = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh, matrix, projection, packetContext, colors: constants);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(expected), NativeBattleSnapshotJson.Serialize(result.Batch));
            NativeBattleCuraga88MeshStep restored = NativeBattleSnapshotJson.Deserialize<NativeBattleCuraga88MeshStep>(NativeBattleSnapshotJson.Serialize(result))!;
            NativeBattleCuraga88MeshStep Continue(NativeBattleCuraga88MeshStep prior)
            {
                return NativeBattleCuraga88Mesh.EmitBatch(mesh, address, matrix, projection, prior.Context88, prior.Registers, prior.FarControls, 0, 0, 0x100, constants, 0x1234, 0x155);
            }
            Assert.Equal(NativeBattleSnapshotJson.Serialize(Continue(result)), NativeBattleSnapshotJson.Serialize(Continue(restored)));
            Assert.Equal(address + (uint)mesh.Length, BinaryPrimitives.ReadUInt32LittleEndian(result.Context88.Read(0x20, 4)));
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
        Assert.Equal(new[] { 4, 5, 7 }, reached.Order().ToArray());
        Assert.True(accepted > 0);
        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(offset));
    }

}
