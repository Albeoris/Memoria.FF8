using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePinnedModelResourceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retained_sources_preserve_publication_domain_cursor_transport_and_restore_across_same_bank_replacement(bool remastered, bool logical)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        int offset = logical ? layout.BankLength + 3 : 3;
        ImmutableArray<byte> data = Enumerable.Range(0, 32).Select(value => (byte)value).ToImmutableArray();
        NativeBattleModelCpuBank original = cpu.Cpu.Banks[0].BindPartyResource(offset, data, 0, data.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, original) } };
        NativeBattleResourcePointer live = new(new(0, offset) { Layout = layout, ResourceOnly = logical }, null, data.Length);
        NativeBattleResourcePointer held = live.Pin(cpu);
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, held, data.Length);
        Assert.Equal(data.AsMemory(), view.ReadMemory(0, data.Length));
        NativeBattleModelGeometrySource source = NativeBattleModelGeometrySource.Open(cpu, staging, held, null!);
        NativeBattleEffectArena cursor = new NativeBattleModelGeometryCursor(source, 7).Write(NativeBattleEffectArena.CreateUnknown(8), 0);
        Assert.Same(original, cursor.ReadResourcePointer(0)!.CpuPublication);
        if (!logical)
        {
            Assert.Equal(live.Cpu!.PhysicalAddress + 7, cursor.ReadUInt32(0));
        }
        cursor = NativeBattleEffectArena.CreateUnknown(8).CopyResourceSnapshot(cursor, 0, 4, 4);
        staging = NativeBattleTemporaryMemory.PublishResourceView(staging, NativeBattleTemporaryMemory.Address, cursor, 8);
        if (!logical)
        {
            Assert.Equal(live.Cpu!.PhysicalAddress + 7, staging.ReadUInt32(NativeBattleTemporaryMemory.Address + 4));
        }
        NativeBattlePrimitiveProcess renderer = NativeBattlePrimitiveProcess.FromColdProcess().PublishModelSource50FDF0(123, source.Cursor(7));
        Assert.Same(original, renderer.ModelRendererRegisters!.ReadSourceCursor()!.CpuPublication);
        NativeBattleModelCpuBank replacement = original.BindPartyResource(offset, Enumerable.Repeat((byte)99, 32).ToImmutableArray(), 0, 32);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, replacement) } };
        Assert.Equal(new byte[] { 0, 1, 2 }, view.Read(0, 3));
        Assert.Equal(new byte[] { 0, 1, 2 }, NativeBattleModelResourceView.Open(cpu, staging, held, 32).Read(0, 3));
        Assert.Equal(new byte[] { 99, 99, 99 }, NativeBattleModelResourceView.Open(cpu, staging, live, 32).Read(0, 3));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.WritePartySection(cpu, staging, held, 0, [9]));
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelResourceEditor(cpu, staging, held));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, held, 33));
        Saved restored = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(new Saved(cpu, staging, held, cursor, renderer)));
        NativeBattleModelGeometrySource restoredSource = NativeBattleModelGeometrySource.Open(restored.Cpu, restored.Staging, restored.Pointer, null!);
        Assert.Equal(logical ? NativeBattlePilotVisualLifetimes.EpochFor(restored.Pointer.CpuPublication!) : live.Cpu!.PhysicalAddress, restoredSource.PresentationIdentity);
        Assert.Equal(7, new NativeBattleModelGeometryCursor(restoredSource).ReadCursor(restored.Cursor, 4).Offset);
        Assert.True(NativeBattleModelCpuBank.SamePublication(restored.Pointer.CpuPublication, restored.Staging.ReadResourcePointer(NativeBattleTemporaryMemory.Address + 4)!.CpuPublication));
        Assert.Equal(new byte[] { 0, 1, 2 }, restoredSource.Read(0, 3));
        NativeBattleModelGeometrySource newer = NativeBattleModelGeometrySource.Open(cpu, staging, live.Pin(cpu), null!);
        NativeBattleEffectArena wrong = new NativeBattleModelGeometryCursor(newer).Write(NativeBattleEffectArena.CreateUnknown(4), 0);
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelGeometryCursor(source).ReadCursor(wrong, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Snapshot_and_skeleton_padding_write_observe_only_the_selected_domain_and_preserve_history(bool logical)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[6] = 0x11;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindResource(0, skeleton.ToImmutableArray(), 0, 50).BindResource(52, skeleton.ToImmutableArray(), 52, 60);
        skeleton[6] = 0x22;
        int logicalOffset = bank.Capacity + 8;
        bank = bank.BindPartyResource(logicalOffset, skeleton.ToImmutableArray(), 0, 50).BindPartyResource(logicalOffset + 52, skeleton.ToImmutableArray(), 52, 60);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        int offset = logical ? logicalOffset : 0;
        NativeBattleModelCpuReference reference = new(0, offset) { ResourceOnly = logical };
        NativeBattleResourcePointer pointer = new(reference, null, skeleton.Length);
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(12, reference)).SetItem(1, cpu.Rows[1].Set(12, reference)) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelResourceView before = NativeBattleModelResourceView.Open(cpu, staging, pointer, skeleton.Length);
        var snapshot = before.ReadSnapshot(0, skeleton.Length);
        Assert.Equal(logical ? (byte)0x22 : (byte)0x11, snapshot.Data[6]);
        Assert.False(snapshot.Known[50]);
        Assert.False(snapshot.Known[51]);
        Assert.Equal(110, snapshot.Known.Count(value => value));
        NativeBattleResourcePointer held = pointer.Pin(cpu);
        NativeBattleModelCpuResources changed = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 50, [0, 0]);
        foreach (NativeBattleModelCpuResourceRow row in changed.Rows.Take(2))
        {
            Assert.Equal(new byte[] { 0, 0 }, NativeBattleModelResourceView.Open(changed, staging, row.Require(12), skeleton.Length).Read(50, 2));
        }
        Assert.Throws<NotSupportedException>(() => before.Read(50, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceView.Open(changed, staging, held, skeleton.Length).Read(50, 2));
        NativeBattleResourcePointer opposite = new(new(0, logical ? 0 : logicalOffset) { ResourceOnly = !logical }, null, skeleton.Length);
        Assert.Equal(new[] { false, false }, NativeBattleModelResourceView.Open(changed, staging, opposite, skeleton.Length).ReadSnapshot(50, 2).Known);
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(changed));
        Assert.Equal(new byte[] { 0, 0 }, NativeBattleModelResourceView.Open(restored, staging, pointer, skeleton.Length).Read(50, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, skeleton.Length - 1, [1, 2]));
    }

    [Fact]
    public void Publication_comparison_preserves_segmented_COW_knownness_after_diagnostic_materialization()
    {
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown().BindResource(0, ImmutableArray.Create<byte>(1, 2, 3, 4), 0, 4).Write(2, [9]);
        NativeBattleModelCpuBank restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuBank>(NativeBattleTestSnapshots.Json(bank));
        Assert.True(NativeBattleModelCpuBank.SamePublication(bank, restored));
        Assert.False(NativeBattleModelCpuBank.SamePublication(bank, restored.Write(2, [8])));
        Assert.False(NativeBattleModelCpuBank.SamePublication(bank, NativeBattleModelCpuBank.Unknown().Write(0, [1, 2, 9])));
    }

    private sealed record Saved(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleResourcePointer Pointer, NativeBattleEffectArena Cursor, NativeBattlePrimitiveProcess Renderer);
}
