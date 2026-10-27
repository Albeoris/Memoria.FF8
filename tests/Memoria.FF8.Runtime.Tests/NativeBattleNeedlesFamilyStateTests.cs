using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNeedlesFamilyStateTests
{
    [Theory]
    [InlineData((ushort)177)]
    [InlineData((ushort)178)]
    public void Fresh_BSS_and_imported_unknown_have_distinct_physical_knowledge(ushort effect)
    {
        NativeBattleNeedlesFamilyState cold = NativeBattleNeedlesFamilyState.FromColdProcess(effect);
        NativeBattleNeedlesFamilyProfile p = cold.Profile;
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(p.NeedleBank));
        Assert.Equal(0u, cold.ReadDword(p.SparkBank + 24 * 299 + 20));
        Assert.Equal(new byte[32], cold.Read(p.MatrixAddress, 32));
        Assert.Throws<NotSupportedException>(() => NativeBattleNeedlesFamilyState.FromUnknown(effect).ReadDword(p.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => NativeBattleNeedlesFamilyState.FromUnknown(effect).ReadPacket(p.PacketBase, 4));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(p.RootBank));
        Assert.Throws<InvalidDataException>(() => cold.WriteDword(p.RootDescriptor, 1));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(p.ChildDescriptor));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(p.ChildBank));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(p.PacketBase));
        Assert.Throws<InvalidDataException>(() => cold.ReadPacket(p.CursorGlobal, 1));
    }
    [Theory]
    [InlineData((ushort)177)]
    [InlineData((ushort)178)]
    public void Partial_live_records_matrix_and_packets_continue_independently_after_JSON(ushort effect)
    {
        NativeBattleNeedlesFamilyState state = NativeBattleNeedlesFamilyState.FromUnknown(effect);
        NativeBattleNeedlesFamilyProfile p = state.Profile;
        state = state.WriteDword(p.NeedleBank, 1).WriteDword(p.NeedleBank + 4, 0xABCD1234).WriteDword(p.CursorGlobal, p.PacketBase + 0x1001C).WritePacket(p.PacketBase + 0x10000, new byte[] { 1, 2, 3, 4 });
        NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, BitConverter.GetBytes((short)4096));
        state = state.WriteView(p.MatrixAddress, matrix, 32);
        NativeBattleNeedlesFamilyState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleNeedlesFamilyState>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(0xABCD1234u, restored.ReadDword(p.NeedleBank + 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, restored.ReadPacket(p.PacketBase + 0x10000, 4));
        Assert.Equal((short)4096, BitConverter.ToInt16(restored.Read(p.MatrixAddress, 2)));
        Assert.Throws<NotSupportedException>(() => restored.Read(p.MatrixAddress + 18, 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(p.SparkBank));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.WriteDword(p.NeedleBank, 0)), NativeBattleTestSnapshots.Json(restored.WriteDword(p.NeedleBank, 0)));
        Assert.Equal(0xABCD1234u, restored.WriteDword(p.NeedleBank, 0).ReadDword(p.NeedleBank + 4));
    }
    [Fact]
    public void Original_modules_are_separate_owners_and_reject_other_effects()
    {
        Assert.Equal(0x234855Cu, NativeBattleNeedlesFamilyProfile.ForEffect(177).PacketBase);
        Assert.Equal(0x2321924u, NativeBattleNeedlesFamilyProfile.ForEffect(178).PacketBase);
        Assert.Throws<NotSupportedException>(() => NativeBattleNeedlesFamilyState.FromColdProcess(176));
        NativeBattleEffectMemory memory = new(null, 0, 0, [], 0, 0, 0, 0) { ThousandNeedles177 = NativeBattleNeedlesFamilyState.FromColdProcess(177), TenThousandNeedles178 = NativeBattleNeedlesFamilyState.FromUnknown(178) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(0u, restored.ThousandNeedles177!.ReadDword(0x236855C));
        Assert.Throws<NotSupportedException>(() => restored.TenThousandNeedles178!.ReadDword(0x2341924));
    }
}
