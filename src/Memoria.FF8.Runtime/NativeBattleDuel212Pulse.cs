using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel212PulseDrawing
{
    public static NativeBattleDuel212HierarchyStep Draw6A1660(NativeBattleDuel212Frame frame, int phase, NativeBattleActionMatrix bone18, NativeBattleActionMatrix world, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if ((uint)phase >= 12)
        {
            throw new InvalidDataException("Native Burning Rave pulse requires its twelve original phases.");
        }
        NativeBattleDuel212Resources resources = frame.Memory.Duel212?.Resources ?? throw new NotSupportedException("Native Burning Rave pulse requires original entered geometry.");
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 140);
        uint address = temporary.Address;
        NativeBattleFileStaging staging = temporary.Staging.Write(address, new byte[20]);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(phase << 8));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(phase << 8));
        short depthScale = unchecked((short)(2 * (6144 - NativeBattleCameraMath.Cosine((phase << 12) / 48))));
        NativeBattleActionMatrix local = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, depthScale], 0, 0, -4096, 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(bone18, local);
        matrix = NativeBattleGfMatrices.Compose56C2F0(world, matrix);
        matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
        for (int index = 0; index < 9; index++)
        {
            staging = Word(staging, address + (uint)index * 2, matrix.Rotation[index]);
        }
        staging = Dword(Dword(Dword(staging, address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
        staging = Dword(Dword(Dword(staging, address + 32, 0x11826E0), address + 56, unchecked((uint)-128)), address + 60, phase == 4 ? 0x30u : 0xF0u);
        int? fade = phase == 4 ? null : phase < 4 ? (4 - phase) << 10 : phase << 9;
        if (fade is int amount)
        {
            staging = Dword(Dword(staging, address + 40, 0), address + 44, unchecked((uint)amount));
        }
        staging = Word(Word(staging, address + 64, 0), address + 66, 32 * (phase & 3));
        staging = Word(Word(Word(Word(staging, address + 68, 0), address + 70, 0), address + 72, 256), address + 74, 256);
        staging = Word(Word(Word(Word(staging, address + 76, 0), address + 78, 128), address + 80, 256), address + 82, 128);
        ImmutableArray<byte> mesh = resources.Meshes.Single(mesh => mesh.Address == 0x11826E0).Bytes;
        NativeBattleEsunaMeshContext context = new(phase == 4 ? 0x30u : 0xF0u, fade is null ? null : 0, fade, -128, 0, checked((ushort)(32 * (phase & 3))), new(0, 0, 256, 256), new(0, 128, 256, 128), 17, 2);
        NativeBattleWindowedMesh701DD0Result result = NativeBattleWindowedMesh701DD0.Emit(staging, address + 32, 0x11826E0, mesh, matrix, projection, context, colors, offset =>
        {
            if (offset < 0 || offset % 8 != 0 || offset / 8 >= resources.Dword(0x11826E4))
            {
                throw new InvalidDataException("Native Burning Rave pulse selects a vertex outside its original mesh.");
            }
            ImmutableArray<byte> point = resources.Read(checked(0x11826E8u + (uint)offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan().Slice(4)));
        });
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(result.Staging, 140) }, result.Batch);
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, int value) => staging.Write(address, BitConverter.GetBytes(unchecked((ushort)value)));
}
