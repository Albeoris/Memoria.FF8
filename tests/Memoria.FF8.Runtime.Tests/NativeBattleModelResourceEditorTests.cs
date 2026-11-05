using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceEditorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Cleanup_updates_only_the_selected_domain_and_preserves_history_aliases_and_restore(bool remastered, bool logical)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 0xF234);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        int logicalOffset = layout.BankLength + 8;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(0, bytes.ToImmutableArray(), 0, bytes.Length).BindPartyResource(logicalOffset, bytes.ToImmutableArray(), 0, bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer target = new(new(0, logical ? logicalOffset : 0) { Layout = layout, ResourceOnly = logical }, null, bytes.Length);
        NativeBattleResourcePointer opposite = new(new(0, logical ? 0 : logicalOffset) { Layout = layout, ResourceOnly = !logical }, null, bytes.Length);
        NativeBattleModelResourceView history = NativeBattleModelResourceView.Open(cpu, staging, target, bytes.Length);
        NativeBattleModelResourceEditor editor = new(cpu, staging, target);
        List<int> writes = [];
        NativeBattleEffectArena temporary = NativeBattleEffect64GeometryCleanup.Clear(editor.Geometry, (offset, value) =>
        {
            writes.Add(offset);
            editor.Write(offset, BitConverter.GetBytes(value));
            Assert.Equal((ushort)0x7234, BinaryPrimitives.ReadUInt16LittleEndian(editor.Read(42, 2)));
            Assert.Equal((ushort)0xF234, BinaryPrimitives.ReadUInt16LittleEndian(history.Read(42, 2)));
        }, NativeBattleEffectArena.CreateUnknown(8));
        Assert.Equal(new[] { 42, 42 }, writes);
        Assert.Equal((ushort)0x7234, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(editor.Current, staging, target, bytes.Length).Read(42, 2)));
        Assert.Equal((ushort)0xF234, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(editor.Current, staging, opposite, bytes.Length).Read(42, 2)));
        Assert.Equal((ushort)0xF234, BinaryPrimitives.ReadUInt16LittleEndian(history.Read(42, 2)));
        Assert.Throws<InvalidDataException>(() => editor.Write(bytes.Length, [1]));
        NativeBattleTemporaryMemory.PublishResourceView(staging, NativeBattleTemporaryMemory.Address, temporary, 8);
        State restored = NativeBattleTestSnapshots.RestoreJson<State>(NativeBattleTestSnapshots.Json(new State(editor.Current, staging)));
        Assert.Equal((ushort)0x7234, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(restored.Cpu, restored.Staging, target, bytes.Length).Read(42, 2)));
        if (logical)
        {
            Assert.True(restored.Staging.ReadResourcePointer(NativeBattleTemporaryMemory.Address)!.Cpu!.ResourceOnly);
        }
        else
        {
            Assert.Equal(target.Cpu!.PhysicalAddress + 28, restored.Staging.ReadUInt32(NativeBattleTemporaryMemory.Address));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conservative_extent_metadata_does_not_reject_a_valid_checked_read(bool logical)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        int offset = logical ? cpu.Cpu.Banks[0].Capacity + 8 : 8;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(offset, ImmutableArray.Create<byte>(1, 2, 3, 4), 0, 4);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleResourcePointer pointer = new(new(0, offset) { ResourceOnly = logical }, null, bank.ResourceCapacity);
        NativeBattleModelGeometrySource source = NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), pointer, null!);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(0, 4));
        Assert.True(source.Owner.Length <= (logical ? bank.ResourceCapacity : bank.Capacity) - offset);
    }

    private sealed record State(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging);
}
