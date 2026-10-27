using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317PointProjectionTests
{
    [Fact]
    public void Projection_follows_actual_links_and_stores_quarter_depth_DWORD_with_retained_neighbors()
    {
        NativeBattleAcid317Frame frame = Frame();
        uint head = NativeBattleAcid317Profile.ArenaAddress + NativeBattleAcid317Profile.PointsOffset;
        int first = NativeBattleAcid317Profile.PointsOffset;
        int second = first + 80;
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(first, new byte[40]).Write(second, new byte[40]);
        arena = arena.Write(first + 6, BitConverter.GetBytes((short)9)).Write(first + 0x14, [0xCC, 0xCC, 0xCC, 0xCC]).Write(first + 0x18, [1, 2, 3, 4]).Write(first + 0x24, BitConverter.GetBytes(head + 80)).Write(second + 4, BitConverter.GetBytes((short)400));
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleAcid317Frame result = Project(frame, head, 2);
        Assert.Equal(500u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena!.Read(first + 0x14, 4)));
        Assert.Equal(600u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena.Read(second + 0x14, 4)));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.Arena.Read(first + 0x18, 4));
        Assert.Equal((short)9, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(first + 6, 2)));
        Assert.Equal(2400u, result.Memory.GeometryRegisters!.Sz[3]);
        Assert.Equal(500u, result.Memory.GeometryRegisters.Sz[2] >> 2);
        NativeBattleAcid317Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAcid317Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Project(restored, head, 2)));
    }

    [Fact]
    public void Unobserved_or_cyclic_point_owners_fail_without_an_alternate_chain()
    {
        NativeBattleAcid317Frame frame = Frame();
        uint head = NativeBattleAcid317Profile.ArenaAddress + NativeBattleAcid317Profile.PointsOffset;
        Assert.Throws<InvalidDataException>(() => Project(frame, head + 1, 1));
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(NativeBattleAcid317Profile.PointsOffset, new byte[40]).Write(NativeBattleAcid317Profile.PointsOffset + 0x24, BitConverter.GetBytes(head));
        Assert.Throws<InvalidDataException>(() => Project(frame with { Memory = frame.Memory with { Arena = arena } }, head, 1));
    }

    private static NativeBattleAcid317Frame Project(NativeBattleAcid317Frame frame, uint head, short count) => NativeBattleAcid317PointProjection.Project(frame, head, count, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), new(null, 100, 160, 108));

    private static NativeBattleAcid317Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, new(317, 316, new(317, NativeBattleAcid317Profile.Entry, NativeBattleAcid317Profile.Initializer), null, null), new(1), NativeBattleAcid317State.FromColdProcess().WriteDword(NativeBattleAcid317Profile.WorkingGlobal, NativeBattleAcid317Profile.ArenaAddress));
    }
}
