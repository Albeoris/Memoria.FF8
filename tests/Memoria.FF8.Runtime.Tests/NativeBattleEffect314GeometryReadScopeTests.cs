using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314GeometryReadScopeTests
{
    [Fact]
    public void Ordered_marker_writes_update_overlapping_views_and_preserve_detached_and_pinned_publications()
    {
        NativeBattleModelCpuResources cpu = Cpu(Enumerable.Range(0, 32).Select(index => (byte)index).ToArray());
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = new(new(0, 0), null, 32);
        NativeBattleResourcePointer alias = new(new(0, 3), null, 12);
        NativeBattleResourcePointer pinned = pointer.Pin(cpu);
        NativeBattleModelCpuResources original = cpu;
        NativeBattleEffect314GeometryReadScope scope = new();
        byte[] detached = scope.Read(cpu, staging, pointer, 32, 0, 32);
        Assert.Equal(new byte[] { 3, 4, 5, 6 }, scope.Read(cpu, staging, alias, 12, 0, 4));
        foreach ((int offset, byte[] bytes) in new[] { (5, new byte[] { 0x85 }), (14, new byte[] { 0x8E, 0x8F }), (5, new byte[] { 5 }) })
        {
            NativeBattleModelCpuResources before = cpu;
            cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, offset, bytes);
            scope.ObserveWrite(before, cpu, pointer, offset, bytes);
            Assert.Equal(NativeBattleModelResourceView.Open(cpu, staging, pointer, 32).Read(0, 32), scope.Read(cpu, staging, pointer, 32, 0, 32));
            Assert.Equal(NativeBattleModelResourceView.Open(cpu, staging, alias, 12).Read(0, 12), scope.Read(cpu, staging, alias, 12, 0, 12));
        }
        Assert.Equal(Enumerable.Range(0, 32).Select(index => (byte)index).ToArray(), detached);
        Assert.Equal(detached, scope.Read(cpu, staging, pinned, 32, 0, 32));
        Assert.Equal(detached, NativeBattleModelResourceView.Open(original, staging, pointer, 32).Read(0, 32));
        byte[] exposed = scope.Read(cpu, staging, pointer, 32, 0, 1);
        exposed[0] = 99;
        Assert.Equal((byte)0, scope.Read(cpu, staging, pointer, 32, 0, 1)[0]);
    }

    [Fact]
    public void Unknown_unconsumed_bytes_and_external_reload_keep_the_original_read_and_extent_contracts()
    {
        NativeBattleModelCpuResources cpu = Cpu([1, 2, 3]);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = new(new(0, 0), null, 12);
        NativeBattleEffect314GeometryReadScope scope = new();
        Assert.Equal(new byte[] { 1, 2, 3 }, scope.Read(cpu, staging, pointer, 12, 0, 3));
        Assert.Throws<NotSupportedException>(() => scope.Read(cpu, staging, pointer, 12, 3, 1));
        NativeBattleModelCpuResources before = cpu;
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 4, [9]);
        scope.ObserveWrite(before, cpu, pointer, 4, [9]);
        Assert.Equal(new byte[] { 9 }, scope.Read(cpu, staging, pointer, 12, 4, 1));
        Assert.Throws<NotSupportedException>(() => scope.Read(cpu, staging, pointer, 12, 3, 2));
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 0, [7]);
        Assert.Equal(new byte[] { 7, 2, 3 }, scope.Read(cpu, staging, pointer, 12, 0, 3));
        Assert.Throws<InvalidDataException>(() => scope.Read(cpu, staging, pointer with { Length = 2 }, 12, 0, 1));
        Assert.Throws<InvalidDataException>(() => scope.Read(cpu, staging, pointer, 12, 11, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 12, [1]));
        Assert.Equal(new byte[] { 7 }, scope.Read(cpu, staging, pointer, 12, 0, 1));
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(cpu));
        Assert.Equal(new byte[] { 7, 2, 3 }, scope.Read(restored, staging, pointer, 12, 0, 3));
        Assert.Throws<NotSupportedException>(() => scope.Read(restored, staging, pointer, 12, 3, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Morph_packets_and_registers_match_live_resource_reads_before_consumption_and_after_marker_clear(bool quad)
    {
        byte[] bytes = NativeBattleEffect301BodyTests.Geometry(quad);
        NativeBattleModelCpuResources cpu = Cpu(bytes);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = new(new(0, 0), null, bytes.Length);
        NativeBattleEffect314GeometryReadScope scope = new();
        int face = ((14 + (quad ? 4 : 3) * 6 + 3) & ~3) + 12;
        foreach (byte marker in new[] { bytes[face + 15], (byte)(bytes[face + 15] | 0x80), bytes[face + 15] })
        {
            NativeBattleModelCpuResources before = cpu;
            cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, face + 15, [marker]);
            scope.ObserveWrite(before, cpu, pointer, face + 15, [marker]);
            NativeBattleModelGeometrySource live = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, pointer, (offset, length) => NativeBattleModelResourceView.Open(cpu, staging, pointer, bytes.Length).Read(offset, length));
            byte[] ReadScoped(int offset, int length) => scope.Read(cpu, staging, pointer, bytes.Length, offset, length);
            NativeBattleModelGeometrySource scoped = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, pointer, ReadScoped, (offset, length) => NativeBattleEffect314TemporaryReads.Known(ReadScoped(offset, length)));
            NativeBattleEffect314MorphBodyStep expected = Draw(live);
            NativeBattleEffect314MorphBodyStep actual = Draw(scoped);
            Assert.Equal(NativeBattleTestSnapshots.Json(expected.Batch), NativeBattleTestSnapshots.Json(actual.Batch));
            Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(expected.Memory)), NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(actual.Memory)));
            Assert.Equal(expected.Temporary156.ReadSnapshot(0, 156).Data, actual.Temporary156.ReadSnapshot(0, 156).Data);
            Assert.Equal(expected.Temporary156.ReadSnapshot(0, 156).Known, actual.Temporary156.ReadSnapshot(0, 156).Known);
            Assert.Equal(expected.VertexScratch.ReadSnapshot(0, 32).Data, actual.VertexScratch.ReadSnapshot(0, 32).Data);
            Assert.Equal(expected.VertexScratch.ReadSnapshot(0, 32).Known, actual.VertexScratch.ReadSnapshot(0, 32).Known);
            Assert.Equal((marker & 0x80) == 0 ? 1 : 0, actual.Batch.Attempts.Length);
            Assert.Equal(NativeBattleModelResourceView.Open(cpu, staging, pointer, bytes.Length).Read(0, bytes.Length), scope.Read(cpu, staging, pointer, bytes.Length, 0, bytes.Length));
        }
    }

    private static NativeBattleEffect314MorphBodyStep Draw(NativeBattleModelGeometrySource geometry)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(0x1000u)).Write(0x1C, BitConverter.GetBytes(1u)).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x14, BitConverter.GetBytes(0x123456u));
        return NativeBattleEffect314Morph.DrawBodyWithPresentation(memory, temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), geometry, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)], NativeBattleEffectArena.Clear(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), 0x1000, 32, pilotIdentity: new(314, 0x123400, 1, 11, 0) { ResourceGeneration = geometry.PresentationIdentity });
    }

    private static NativeBattleModelCpuResources Cpu(byte[] bytes)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        return cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, cpu.Cpu.Banks[0].Write(0, bytes)) } };
    }
}
