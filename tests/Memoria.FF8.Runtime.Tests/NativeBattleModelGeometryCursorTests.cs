using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelGeometryCursorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Alignment_and_retained_cursor_reads_preserve_real_origin_residue_and_owner(int residue)
    {
        byte[] bytes = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        uint basis = checked(0x1000u + (uint)residue);
        NativeBattleModelGeometrySource numeric = NativeBattleModelGeometrySource.FromNumericArena(basis, (address, length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan(checked((int)(address - basis)), length)));
        NativeBattleModelGeometryCursor expected = new NativeBattleModelGeometryCursor(numeric, 3).Align4();
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        int offset = cpu.Cpu.Banks[0].Capacity + residue;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(offset, bytes.ToImmutableArray(), 0, bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleModelGeometrySource logical = NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), new(new(0, offset) { ResourceOnly = true }, null, bytes.Length), null!);
        NativeBattleModelGeometryCursor actual = new NativeBattleModelGeometryCursor(logical, 3).Align4();
        Assert.Equal(expected.Offset, actual.Offset);
        Assert.Equal(expected.Read(2).Read(0, 2), actual.Read(2).Read(0, 2));
        NativeBattleEffectArena retained = actual.Write(NativeBattleEffectArena.CreateUnknown(4), 0);
        retained = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(retained));
        Assert.Equal(actual.Offset, new NativeBattleModelGeometryCursor(logical).ReadCursor(retained, 0).Offset);
        Assert.Null(actual.NumericAddress);
        Assert.Equal(basis + (uint)expected.Offset, expected.NumericAddress);
        NativeBattleModelGeometryCursor outside = actual + 1000;
        Assert.True(outside.Write(NativeBattleEffectArena.CreateUnknown(4), 0).ReadResourcePointer(0)!.Cpu!.ResourceOnly);
        Assert.Throws<InvalidDataException>(() => outside.Read(1));
        NativeBattleEffectArena wrongOwner = NativeBattleEffectArena.CreateUnknown(4).WriteResourcePointer(0, new(new(2, 0) { ResourceOnly = true }, null));
        Assert.Throws<InvalidDataException>(() => actual.ReadCursor(wrongOwner, 0));
        NativeBattleEffectArena bareNumber = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(basis));
        Assert.Throws<NotSupportedException>(() => actual.ReadCursor(bareNumber, 0));
    }
}
