using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelGeometrySourceTests
{
    [Theory]
    [InlineData(false, 70)]
    [InlineData(true, 70)]
    [InlineData(false, 260)]
    [InlineData(true, 260)]
    public void Logical_context_draw_preserves_numeric_math_and_retains_body_linked_cursors_after_snapshot(bool remastered, int effect)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        ImmutableArray<byte> bytes = Geometry();
        int bodyOffset = layout.BankLength + 8;
        int linkedOffset = bodyOffset + bytes.Length;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[1].BindPartyResource(bodyOffset, bytes, 0, bytes.Length).BindPartyResource(linkedOffset, bytes, 0, bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, bank) } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        foreach (int offset in new[] { bodyOffset, linkedOffset })
        {
            NativeBattleResourcePointer pointer = new(new(1, offset) { Layout = layout, ResourceOnly = true }, null, bytes.Length);
            NativeBattleModelGeometrySource geometry = NativeBattleModelGeometrySource.Open(cpu, staging, pointer, (_, _) => throw new InvalidOperationException("CPU geometry must use its retained CPU owner."));
            NativeBattleModelContext44Step logical = Draw(geometry, effect);
            NativeBattleModelContext44Step numeric = Draw(NativeBattleModelGeometrySource.FromNumeric(0x1939C30, (first, length) => bytes.AsSpan(first, length).ToArray()), effect);
            Assert.Equal(NativeBattleTestSnapshots.Json(numeric.Batch), NativeBattleTestSnapshots.Json(logical.Batch));
            Assert.Equal(NativeBattleTestSnapshots.Json(numeric.ProjectedScratch), NativeBattleTestSnapshots.Json(logical.ProjectedScratch));
            Assert.Equal(NativeBattleTestSnapshots.Json(numeric.Memory.GeometryRegisters), NativeBattleTestSnapshots.Json(logical.Memory.GeometryRegisters));
            Assert.Equal(NativeBattleTestSnapshots.Json(numeric.Memory.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(logical.Memory.ScriptedColorRegisters));
            Assert.Equal(numeric.Context44.ReadSnapshot(4, 40).Data, logical.Context44.ReadSnapshot(4, 40).Data);
            Assert.Equal(numeric.Context44.ReadSnapshot(4, 40).Known, logical.Context44.ReadSnapshot(4, 40).Known);
            Assert.Throws<NotSupportedException>(() => logical.Context44.ReadUInt32(0));
            Assert.Null(logical.Memory.PrimitiveProcess!.ModelRendererRegisters!.SourceCursor);
            NativeBattleResourcePointer cursor = logical.Memory.PrimitiveProcess.ModelRendererRegisters.ReadSourceCursor()!;
            Assert.True(cursor.Cpu!.ResourceOnly);
            Assert.Equal(offset + bytes.Length, cursor.Cpu.Offset);
            Assert.Equal(0, cursor.Length);
            NativeBattleEffectArena temporary = NativeBattleModelContext44.CopyContext(logical.Context44, 0, NativeBattleEffectArena.CreateUnknown(92), 48);
            NativeBattleModelContext44.PublishTemporary(staging, NativeBattleTemporaryMemory.Address, temporary);
            Pilot restored = NativeBattleTestSnapshots.RestoreJson<Pilot>(NativeBattleTestSnapshots.Json(new Pilot(cpu, staging, temporary, logical.Memory.PrimitiveProcess)));
            NativeBattleResourcePointer saved = restored.Staging.ReadResourcePointer(NativeBattleTemporaryMemory.Address + 48)!;
            Assert.True(saved.Cpu!.ResourceOnly);
            Assert.Equal(offset + 52, saved.Cpu.Offset);
            NativeBattleModelGeometrySource rebound = NativeBattleModelGeometrySource.Open(restored.Cpu, restored.Staging, pointer, (_, _) => throw new InvalidOperationException());
            Assert.Equal(bytes, rebound.Read(0, bytes.Length));
            Assert.Equal(cursor, restored.Process.ModelRendererRegisters!.ReadSourceCursor());
            Assert.Equal(effect, logical.Batch.Attempts[0].Primitive!.PilotGeometry!.Identity.Effect);
            Assert.Equal(geometry.PresentationIdentity, logical.Batch.Attempts[0].Primitive!.PilotGeometry!.Identity.ResourceGeneration);
        }
    }

    [Fact]
    public void Views_retain_publication_and_guest_domain_without_promoting_held_old_pointers()
    {
        ImmutableArray<byte> bytes = Geometry();
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        int offset = cpu.Cpu.Banks[0].Capacity + 8;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(0, bytes, 0, bytes.Length).BindPartyResource(offset, bytes, 0, bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer logical = new(new(0, offset) { ResourceOnly = true }, null, bytes.Length);
        NativeBattleResourcePointer guest = new(new(0, 0), null, bytes.Length);
        NativeBattleModelGeometrySource held = NativeBattleModelGeometrySource.Open(cpu, staging, logical, null!);
        NativeBattleModelGeometrySource old = NativeBattleModelGeometrySource.Open(cpu, staging, guest, null!);
        bank = bank.Write(offset + 14, BitConverter.GetBytes((short)-200));
        NativeBattleModelCpuResources changed = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleModelGeometrySource current = NativeBattleModelGeometrySource.Open(changed, staging, logical, null!);
        Assert.Equal((short)-400, BinaryPrimitives.ReadInt16LittleEndian(held.Read(14, 2)));
        Assert.Equal((short)-200, BinaryPrimitives.ReadInt16LittleEndian(current.Read(14, 2)));
        Assert.Equal((short)-400, BinaryPrimitives.ReadInt16LittleEndian(old.Read(14, 2)));
        Assert.NotEqual(held.PresentationIdentity, current.PresentationIdentity);
        Assert.Equal(guest.Cpu!.PhysicalAddress, old.PresentationIdentity);
        Assert.False(old.Cursor(4).Cpu!.ResourceOnly);
        Assert.Throws<InvalidDataException>(() => held.Read(-1, 1));
        Assert.Throws<InvalidDataException>(() => held.Cursor(bytes.Length + 1));
        Assert.Throws<InvalidDataException>(() => held.Read(bytes.Length, 1));
    }

    [Fact]
    public void Legacy_renderer_cursor_snapshot_adapts_and_numeric_publication_clears_logical_domain()
    {
        NativeBattlePrimitiveProcess legacy = NativeBattlePrimitiveProcess.FromColdProcess().Publish50FDF0(123, 456);
        string json = NativeBattleTestSnapshots.Json(legacy);
        Assert.DoesNotContain("SourceResourceCursor", json);
        NativeBattlePrimitiveProcess restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(json);
        Assert.Equal(new NativeBattleResourcePointer(null, 456), restored.ModelRendererRegisters!.ReadSourceCursor());
        NativeBattleResourcePointer pointer = new(new(0, 70000) { ResourceOnly = true }, null, 0);
        NativeBattlePrimitiveProcess logical = restored.PublishModelSource50FDF0(123, pointer);
        Assert.Equal(pointer, logical.ModelRendererRegisters!.ReadSourceCursor());
        Assert.Null(logical.ModelRendererRegisters.SourceCursor);
        Assert.Equal(legacy.ModelRendererRegisters, logical.Publish50FDF0(123, 456).ModelRendererRegisters);
        Assert.Throws<InvalidDataException>(() => (logical.ModelRendererRegisters with { SourceCursor = 456 }).ReadSourceCursor());
    }

    private static NativeBattleModelContext44Step Draw(NativeBattleModelGeometrySource geometry, int effect)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffectArena context = NativeBattleEffectArena.CreateUnknown(44).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x1C, BitConverter.GetBytes(0x808080u)).Write(0x20, BitConverter.GetBytes(1u));
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        return NativeBattleModelContext44.Draw(memory, context, [NativeBattlePackedMatrix32.FromMatrix(view with { Z = 2000 }, null)], geometry, NativeBattleEffectArena.CreateUnknown(64), 64, new(view, 256, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, false, 17, 4, pilotIdentity: new((ushort)effect, 1, 2, 3, 4) { ResourceGeneration = geometry.PresentationIdentity });
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 4);
        short[] points = [-400, -400, 0, 400, -400, 0, -400, 400, 0, 400, 400, 0];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 1);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x00040603);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x08070504);
        return bytes.ToImmutableArray();
    }

    private sealed record Pilot(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleEffectArena Temporary, NativeBattlePrimitiveProcess Process);
}
