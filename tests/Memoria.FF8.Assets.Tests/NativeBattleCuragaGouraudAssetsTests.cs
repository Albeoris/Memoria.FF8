using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuragaGouraudAssetsTests
{
    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    public void Two_actual_frame_twelve_meshes_emit_their_original_two_G3_and_four_G4_faces(int index)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCuragaExecutableState bank = NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(game.ReadNativeCuragaResources().Executable!);
        uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(NativeBattleCuragaExecutableState.Address + 0x84 + (uint)index * 4, 4));
        ImmutableArray<byte> bytes = bank.Read(mesh, checked(NativeBattleCuragaExecutableState.Length - (int)(mesh - NativeBattleCuragaExecutableState.Address))).ToImmutableArray();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(bytes, matrix, projection, new(0x30, null, null, null, null, null, 17, 2));
        Assert.Equal(6, batch.Attempts.Length);
        Assert.Equal(200, batch.AllocationBytes);
        Assert.Equal(new[] { 28, 28, 36, 36, 36, 36 }, batch.Attempts.Select(attempt => attempt.PacketLength));
        Assert.All(batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.Equal(new byte[] { 0x30, 0x30, 0x38, 0x38, 0x38, 0x38 }, batch.Attempts.Select(attempt => (byte)(attempt.Primitive!.Words[1] >> 24 & 0xFC)).ToArray());
    }
}
