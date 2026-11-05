using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelEchoResourceTests
{
    private const uint SourceAddress = NativeBattleFileStaging.Address + 0x10000;
    private const uint EchoAddress = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
    private const uint Cache = 0x196B018 - 32;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Theory]
    [InlineData(212, 1)]
    [InlineData(212, 2)]
    [InlineData(212, 3)]
    [InlineData(212, 4)]
    [InlineData(216, 1)]
    [InlineData(216, 2)]
    [InlineData(216, 3)]
    [InlineData(216, 4)]
    [InlineData(274, 1)]
    [InlineData(274, 2)]
    [InlineData(274, 3)]
    [InlineData(274, 4)]
    [InlineData(212, 5)]
    [InlineData(212, 6)]
    [InlineData(216, 5)]
    [InlineData(216, 6)]
    [InlineData(274, 5)]
    [InlineData(274, 6)]
    public void Typed_echo_preserves_numeric_packets_current_shared_frame_and_pinned_context_cursor_after_restore(int family, int domain)
    {
        ImmutableArray<byte> geometry = Geometry();
        bool shared = domain == 1;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        NativeBattleResourcePointer pointer;
        NativeBattleModelGeometrySource section;
        if (shared)
        {
            pointer = new(null, SourceAddress, geometry.Length);
            section = NativeBattleModelGeometrySource.OpenShared(pointer, (_, _) => throw new InvalidOperationException("Shared source must reopen around the current private draw frame."));
        }
        else
        {
            NativeBattlePartyCpuLayout layout = domain == 4 ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
            cpu = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
            int origin = domain >= 3 ? layout.BankLength + 4 : 4;
            NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(origin, geometry, 0, geometry.Length);
            cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
            pointer = new NativeBattleResourcePointer(new(0, origin) { ResourceOnly = domain >= 3, Layout = layout }, null, geometry.Length).Pin(cpu);
            section = NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), pointer, null!);
        }
        if (domain is 5 or 6)
        {
            NativeBattlePartyCpuLayout layout = domain == 6 ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
            NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown(geometry.Length + 4).BindResource(4, geometry, 0, geometry.Length);
            cpu = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout), EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, bank) };
            pointer = new NativeBattleResourcePointer(new(3, 1024) { Layout = layout, EnemyResourceId = 7, ResourceOffset = 4 }, null, geometry.Length).Pin(cpu);
            section = NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), pointer, null!);
        }
        Frame expected = Draw(Fixture(), null, family, shared);
        Frame actual = Draw(Fixture(), section, family, shared);
        Assert.Equal(expected.Writes, actual.Writes);
        Assert.Equal(expected.Staging.Read(Cache, 24), actual.Staging.Read(Cache, 24));
        NativeBattleGfGeometryBatch expectedBatch = Assert.Single(expected.Batches);
        NativeBattleGfGeometryBatch actualBatch = Assert.Single(actual.Batches);
        Assert.Equal(expectedBatch.AllocationBytes, actualBatch.AllocationBytes);
        Assert.Equal(JsonSerializer.Serialize(expectedBatch), JsonSerializer.Serialize(actualBatch));
        Assert.NotNull(Assert.Single(actualBatch.Attempts).Primitive);
        NativeBattleResourcePointer cursor = actual.Staging.ReadResourcePointer(NativeBattleTemporaryMemory.Address + 32)!;
        if (shared)
        {
            Assert.Equal(SourceAddress + 52, cursor.StagingAddress);
        }
        else
        {
            Assert.Same(pointer.CpuPublication, cursor.CpuPublication);
            Assert.Equal(pointer.Cpu!.Offset + 52, cursor.Cpu!.Offset);
            Assert.Equal(pointer.Cpu.EnemyResourceId, cursor.Cpu.EnemyResourceId);
            if (pointer.Cpu.EnemyResourceId is not null)
            {
                Assert.Equal(pointer.Cpu.ResourceOffset + 52, cursor.Cpu.ResourceOffset);
            }
            if (domain == 2)
            {
                Assert.Equal(pointer.Cpu.PhysicalAddress + 52, actual.Staging.ReadUInt32(NativeBattleTemporaryMemory.Address + 32));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => actual.Staging.ReadUInt32(NativeBattleTemporaryMemory.Address + 32));
            }
            NativeBattleModelCpuResources changed = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(cpu.Cpu.EffectiveLayout), EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty };
            NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(actual.Staging));
            NativeBattleResourcePointer held = restored.ReadResourcePointer(NativeBattleTemporaryMemory.Address + 32)!;
            Assert.Equal(geometry.AsSpan(52, 16).ToArray(), NativeBattleModelResourceView.Open(changed, restored, held, 16).Read(0, 16));
        }

        Frame Fixture()
        {
            NativeBattleModulePoolTask echo = NativeBattleDuelEcho.Initialize6A1230(new(0, 0, null, 0, new byte[4096].ToImmutableArray()), 12, [0]).WriteByte(13, 1);
            echo = echo.WriteWord(148, 4096).WriteWord(156, 4096).WriteWord(164, 4096);
            return new(NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).BindResource(SourceAddress, geometry), echo, 0, []);
        }
        Frame Draw(Frame frame, NativeBattleModelGeometrySource? selected, int effect, bool mutable)
        {
            NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = Identity, SequenceState = new() { MeshMask124 = 1 }, Color40 = 0x808080 };
            Func<Frame, int, int, byte[]> read = (current, offset, length) =>
            {
                if (offset >= 52)
                {
                    Assert.True(current.Writes > 0);
                }
                return current.Staging.Read(SourceAddress + (uint)offset, length);
            };
            Frame Write(Frame current, uint address, ImmutableArray<byte> bytes)
            {
                current.Staging.Write(address, bytes.AsSpan());
                if (mutable)
                {
                    current.Staging.WriteByte(SourceAddress + 60, 0xA5);
                }
                return current with { Writes = current.Writes + 1 };
            }
            Frame Submit(Frame current, NativeBattleGfGeometryBatch batch) => current with { Batches = current.Batches.Add(batch) };
            if (selected is { } resource)
            {
                read = shared ? read : (_, _, _) => throw new InvalidOperationException("CPU geometry must borrow its selected publication.");
                return effect == 216 ? NativeBattleDuelEchoDrawing.Draw697520(frame, model, EchoAddress, 12, current => current.Echo, (current, written, _) => current with { Echo = written }, resource, read, 4, Identity, new(Identity, 256, 160, 108), Write, (current, address, length) => current.Staging.ReadSnapshot(address, length), Submit, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging }) : NativeBattleDuelEchoDrawing.Draw6A2760(frame, model, EchoAddress, 12, current => current.Echo, (current, written, _) => current with { Echo = written }, resource, read, 4, Identity, new(Identity, 256, 160, 108), Write, (current, address, length) => current.Staging.ReadSnapshot(address, length), Submit, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging });
            }
            return effect == 216 ? NativeBattleDuelEchoDrawing.Draw697520(frame, model, EchoAddress, 12, current => current.Echo, (current, written, _) => current with { Echo = written }, SourceAddress, read, 4, Identity, new(Identity, 256, 160, 108), Write, (current, address, length) => current.Staging.ReadSnapshot(address, length), Submit, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging }) : NativeBattleDuelEchoDrawing.Draw6A2760(frame, model, EchoAddress, 12, current => current.Echo, (current, written, _) => current with { Echo = written }, SourceAddress, read, 4, Identity, new(Identity, 256, 160, 108), Write, (current, address, length) => current.Staging.ReadSnapshot(address, length), Submit, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging });
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0x7FFFFFFFu)]
    [InlineData(0x80000000u)]
    [InlineData(uint.MaxValue)]
    public void Presentation_identity_preserves_old_uint_shift_and_unchecked_boundaries(uint resource)
    {
        NativeBattleModulePoolTask task = new(0, 0, null, 0, new byte[64].ToImmutableArray());
        NativeBattleDuel28Presentation.Allocated(task, 12);
        Assert.Equal(unchecked(((long)resource << 32) ^ 1L), NativeBattleDuel28Presentation.Row(task, 12, resource));
        Assert.Equal((long)resource, NativeBattleDuel28Presentation.Identity(212, task, resource).ResourceGeneration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Copied_context_pointer_rejects_mismatch_partial_knownness_or_numeric_symbolic_owner(int corruption)
    {
        NativeBattleResourcePointer pointer = corruption == 3 ? new(null, null) { CpuRow = 2 } : new(new(0, 4) { ResourceOnly = corruption == 2 }, null, 4);
        uint numeric = pointer.Cpu?.TryPhysicalAddress() ?? 0x196B01Cu;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteUInt32(NativeBattleTemporaryMemory.Address, corruption == 0 ? numeric + 1 : numeric);
        if (corruption == 1)
        {
            staging.MarkUnknown(NativeBattleTemporaryMemory.Address + 1, 1);
        }
        staging.SetResourcePointerMetadata(NativeBattleTemporaryMemory.Address, pointer);
        Assert.Throws<InvalidDataException>(() => NativeBattleTestSnapshots.Json(staging));
    }

    [Fact]
    public void Old_symbolic_guest_context_receipt_remains_readable_without_fabricating_new_numeric_bytes()
    {
        NativeBattleResourcePointer pointer = new(new(0, 4), null, 4);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(NativeBattleTemporaryMemory.Address, pointer);
        Assert.Throws<NotSupportedException>(() => staging.ReadUInt32(NativeBattleTemporaryMemory.Address));
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging));
        Assert.Equal(pointer.Cpu, restored.ReadResourcePointer(NativeBattleTemporaryMemory.Address)!.Cpu);
        Assert.Throws<NotSupportedException>(() => restored.ReadUInt32(NativeBattleTemporaryMemory.Address));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Duel274_entry_keeps_typed_context_and_exact_legacy_identity_overflow(bool logical)
    {
        ImmutableArray<byte> geometry = Geometry();
        NativeBattlePartyCpuLayout layout = NativeBattlePartyCpuLayout.Remastered;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        int origin = logical ? layout.BankLength + 4 : 4;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(origin, geometry, 0, geometry.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(new(0, origin) { Layout = layout, ResourceOnly = logical }, null, geometry.Length).Pin(cpu);
        NativeBattleModelGeometrySource source = NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), pointer, null!);
        NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0);
        NativeBattleDuel274Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), new(1)) { PilotIdentity = new(274, 1, 1, 1, 1) { ResourceGeneration = long.MaxValue } };
        frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, EchoAddress, [0]);
        byte[] matrix = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(matrix, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(16), 4096);
        frame = NativeBattleDuel274ArenaEcho.Capture68ADF0(frame, EchoAddress, 1, _ => (matrix, Enumerable.Repeat(true, 32).ToArray()));
        int emitted = 0;
        NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = Identity, SequenceState = new() { MeshMask124 = 1 }, Color40 = 0x808080 };
        frame = NativeBattleDuel274Echo.Draw68A7B0(frame, model, EchoAddress, source, (_, _, _) => throw new InvalidOperationException("Selected CPU source is immutable."), 4, Identity, new(Identity, 256, 160, 108), (current, batch) =>
        {
            NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
            Assert.NotNull(primitive);
            Assert.Equal(unchecked(long.MaxValue * 397 ^ source.PresentationIdentity), primitive.PilotGeometry!.Identity.ResourceGeneration);
            emitted++;
            return current;
        }, 0x155, 0x100);
        Assert.Equal(1, emitted);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(frame.Staging));
        Assert.True(NativeBattleModelCpuBank.SamePublication(bank, restored.ReadResourcePointer(NativeBattleTemporaryMemory.Address + 32)!.CpuPublication));
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[68];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 3);
        short[] vertices = [0, 0, 400, 100, 0, 400, 0, 100, 400];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22 + index * 2), vertices[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), 2);
        return bytes.ToImmutableArray();
    }

    private sealed record Frame(NativeBattleFileStaging Staging, NativeBattleModulePoolTask Echo, int Writes, ImmutableArray<NativeBattleGfGeometryBatch> Batches);
}
