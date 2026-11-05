using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

internal sealed class NativeBattleLogicalGeometryFixture
{
    internal byte[] Bytes { get; }
    internal NativeBattleModelGeometrySource Source { get; }
    private readonly NativeBattleModelCpuResources cpu;
    private readonly NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();

    internal NativeBattleLogicalGeometryFixture(byte[] original, bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        int first = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(4));
        Bytes = new byte[layout.BankLength + original.Length];
        original.AsSpan(0, first).CopyTo(Bytes);
        BinaryPrimitives.WriteInt32LittleEndian(Bytes.AsSpan(4), layout.BankLength + first);
        original.AsSpan(first).CopyTo(Bytes.AsSpan(layout.BankLength + first));
        cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        int offset = layout.BankLength + 8;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[1].BindPartyResource(offset, Bytes.ToImmutableArray(), 0, Bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, bank) } };
        Source = NativeBattleModelGeometrySource.Open(cpu, staging, new(new(1, offset) { Layout = layout, ResourceOnly = true }, null, Bytes.Length), null!);
    }

    internal void AssertTemporary(NativeBattleEffectArena numeric, NativeBattleEffectArena logical, int length, params int[] fields)
    {
        var expected = numeric.ReadSnapshot(0, length);
        var actual = logical.ReadSnapshot(0, length);
        foreach (int field in fields)
        {
            Array.Clear(expected.Data, field, 4);
            Array.Clear(actual.Data, field, 4);
            Array.Clear(expected.Known, field, 4);
            Array.Clear(actual.Known, field, 4);
            Assert.True(logical.ReadResourcePointer(field)!.Cpu!.ResourceOnly);
        }
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.Known, actual.Known);
        NativeBattleTemporaryMemory.PublishResourceView(staging, NativeBattleTemporaryMemory.Address, logical, length);
        State restored = NativeBattleTestSnapshots.RestoreJson<State>(NativeBattleTestSnapshots.Json(new State(cpu, staging)));
        foreach (int field in fields)
        {
            NativeBattleResourcePointer pointer = restored.Staging.ReadResourcePointer(NativeBattleTemporaryMemory.Address + (uint)field)!;
            Assert.Equal(logical.ReadResourcePointer(field), pointer);
            int relative = pointer.Cpu!.Offset - Source.Owner.Cpu!.Offset;
            int count = Math.Min(2, pointer.Length ?? 0);
            Assert.Equal(Bytes.AsSpan(relative, count).ToArray(), NativeBattleModelResourceView.Open(restored.Cpu, restored.Staging, pointer, count).Read(0, count));
        }
    }

    private sealed record State(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging);
}
