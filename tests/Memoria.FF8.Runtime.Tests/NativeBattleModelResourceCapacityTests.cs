using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceCapacityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Oversized_body_or_linked_geometry_uses_logical_views_for_pose_draw_rebind_restore_and_field_return(bool remastered, bool largeLinked)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(2, cpu.Cpu.Banks[2].Write(4, [0xA1, 0xB2])) } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        (cpu, staging) = Load(cpu, staging, Source(false, largeLinked ? 8 : layout.BankLength + 4), null);
        NativeBattleModelResourceView beforeLinked = NativeBattleModelResourceView.Open(cpu, staging, cpu.Rows[0].Require(12), 64);
        (cpu, staging) = Load(cpu, staging, Source(true, largeLinked ? layout.BankLength + 4 : 8), 1);
        cpu.Validate();
        Assert.True(cpu.Cpu.Banks[1].HasResourceStorage);
        Assert.Equal(layout.BankLength, cpu.Cpu.Banks[1].Capacity);
        Assert.True(cpu.Cpu.Banks[1].ResourceCapacity > layout.BankLength);
        Assert.All(cpu.Rows.Take(2).SelectMany(row => row.Fields.Values).Where(reference => reference is not null), reference => Assert.True(reference!.ResourceOnly));
        Assert.Null(cpu.Rows[1].Require(16).TryPhysicalAddress());
        Assert.Throws<NotSupportedException>(() => cpu.Rows[1].Require(16).PhysicalAddress);
        Assert.Equal(new byte[] { 0xA1, 0xB2 }, cpu.Cpu.Banks[2].Read(4, 2));
        NativeBattleModelCpuReference largeGeometry = cpu.Rows[largeLinked ? 1 : 0].Require(16);
        NativeBattleModelResourceView geometryView = NativeBattleModelResourceView.Open(cpu, staging, largeGeometry, cpu.Rows[largeLinked ? 1 : 0].Lengths[16]);
        Assert.Equal((byte)1, geometryView.Read(0, 1)[0]);
        Assert.Equal(beforeLinked.Read(0, 64), NativeBattleModelResourceView.Open(cpu, staging, cpu.Rows[0].Require(12), 64).Read(0, 64));
        uint directory = NativeBattleFileStaging.Address + (uint)(4 * layout.BankLength);
        uint rowAddress = directory + 0x100;
        staging.Write(directory, Dat([Animation(), new byte[4], Animation()]));
        NativeBattleGraphicsModel model = new(1, 2) { ModelId4 = 0, Flags2 = 4, ActiveResourceRow100 = 0, OriginalResourceRow104 = 0, LinkedTextureResourceIndex = 1, LinkedTransform = new(new(0, 0, 0), new(0, 0, 0)), SequenceState = new() { Default2 = 6 } };
        NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(staging, cpu, model, rowAddress, directory);
        Pilot restored = NativeBattleTestSnapshots.RestoreJson<Pilot>(NativeBattleTestSnapshots.Json(new Pilot(cpu, bound.Staging, bound.Model)));
        NativeBattleGraphicsResource resolved = NativeBattleVictoryResources.Resolve(restored.Staging, restored.Cpu, restored.Model, new([0, 0]) { AnimationFrames = [] });
        Assert.Equal(1, resolved.PoseSource!.BoneCount);
        Assert.Equal(1, resolved.LinkedPoseSource!.BoneCount);
        model = restored.Model with { SequenceState = restored.Model.SequenceState! with { DecodedPose = resolved.PoseSource.Sample(0, 1, 0), LinkedDecodedPose = resolved.LinkedPoseSource.Sample(0, 1, 0), LinkedPose = new(0, 1, 1, 0) } };
        model = NativeBattleBodyBoneMatrices.Publish508F60(model, resolved.PoseSource, resolved.LinkedPoseSource);
        Draw(restored.Cpu, restored.Staging, 0, NativeBattleBodyBoneMatrices.Require(model, resolved.PoseSource));
        Draw(restored.Cpu, restored.Staging, 1, model.CurrentLinkedBoneMatrices!.Matrices);
        NativeBattleModelCpuBank prior = restored.Cpu.Cpu.Banks[1];
        int vertex = largeGeometry.Offset + layout.BankLength + 10;
        NativeBattleModelCpuBank changed = prior.Write(vertex, BitConverter.GetBytes((short)-20));
        Assert.Equal((short)-20, BinaryPrimitives.ReadInt16LittleEndian(changed.Read(vertex, 2)));
        Assert.Equal((short)-10, BinaryPrimitives.ReadInt16LittleEndian(prior.Read(vertex, 2)));
        cpu = restored.Cpu with { Cpu = restored.Cpu.Cpu with { Banks = restored.Cpu.Cpu.Banks.SetItem(1, changed) } };
        uint bankAddress = checked(layout.BaseAddress + (uint)layout.BankLength);
        NativeBattleModelCpuResources returned;
        using (NativeFieldRegistrationMemory memory = new(restored.Staging, layout.BaseAddress, checked(layout.BaseAddress + (uint)(3 * layout.BankLength))))
        {
            memory.ObserveCpuBanks(cpu);
            Assert.Equal(new byte[] { 0xA1, 0xB2 }, memory.Read(bankAddress + (uint)layout.BankLength + 4, 2));
            memory.Write8(bankAddress + 4, 0xCC);
            returned = memory.ReconcileCpuBanks(cpu)!;
            memory.Finish();
        }
        Assert.Equal((short)-20, BinaryPrimitives.ReadInt16LittleEndian(returned.Cpu.Banks[1].Read(vertex, 2)));
        Assert.Equal(new byte[] { 0xCC }, NativeKnownByteRanges.Read(returned.Cpu.Banks[1].KnownRanges, 4, 1, layout.BankLength));
        Assert.Equal(new byte[] { 0xA1, 0xB2 }, returned.Cpu.Banks[2].Read(4, 2));
        returned.Validate();
        Pilot afterReturn = NativeBattleTestSnapshots.RestoreJson<Pilot>(NativeBattleTestSnapshots.Json(new Pilot(returned, restored.Staging, model)));
        Draw(afterReturn.Cpu, afterReturn.Staging, largeLinked ? 1 : 0, largeLinked ? model.CurrentLinkedBoneMatrices!.Matrices : NativeBattleBodyBoneMatrices.Require(model, resolved.PoseSource));
        Assert.All(afterReturn.Cpu.Cpu.Banks[1].KnownRanges, range => Assert.True(range.Offset + range.Data.Length <= layout.BankLength));
        (cpu, staging) = Load(afterReturn.Cpu, afterReturn.Staging, Source(false, 8), null);
        (cpu, staging) = Load(cpu, staging, Source(true, 8), 1);
        cpu.Validate();
        Assert.NotSame(afterReturn.Cpu.Cpu.Banks[1], cpu.Cpu.Banks[1]);
        Draw(cpu, staging, 0, NativeBattleBodyBoneMatrices.Require(model, resolved.PoseSource));
        Draw(cpu, staging, 1, model.CurrentLinkedBoneMatrices!.Matrices);
        Assert.Equal((short)-20, BinaryPrimitives.ReadInt16LittleEndian(afterReturn.Cpu.Cpu.Banks[1].Read(vertex, 2)));
    }

    [Fact]
    public void Logical_pointer_metadata_survives_working_arena_transaction_task_and_json_without_numeric_alias_bytes()
    {
        NativeBattleResourcePointer pointer = new(new(1, 100000) { ResourceOnly = true }, null, 64);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().WriteResourcePointer(16, pointer);
        Assert.Throws<NotSupportedException>(() => arena.Read(16, 4));
        using (NativeBattleEffectArenaTransaction transaction = new(arena))
        {
            transaction.BeginCheckpoint();
            transaction.View.WriteResourcePointer(20, pointer);
            transaction.RollbackCheckpoint();
            Assert.Equal(pointer, transaction.View.ReadResourcePointer(16));
            transaction.View.WriteResourcePointer(24, pointer);
            arena = transaction.Freeze();
        }
        arena = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena));
        Assert.Equal(pointer, arena.ReadResourcePointer(24));
        Assert.Throws<NotSupportedException>(() => arena.Read(24, 4));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000000, 0x2001000, 64, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteResourcePointer(16, pointer);
        task = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task));
        Assert.Equal(pointer, task.ReadResourcePointer(16));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(16));
    }

    private static void Draw(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, int row, ImmutableArray<NativeBattleActionMatrix> bones)
    {
        NativeBattleModelCpuResourceRow owner = cpu.Rows[row];
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, new NativeBattleResourcePointer(owner.Require(16), null, owner.Lengths[16]), owner.Lengths[16]);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(view.Read, bones.Length);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 1000, 160, 108);
        NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, bones, projection, 0x808080, uint.MaxValue, new byte[24].ToImmutableArray(), new(0, 0, 1f / 4096, 6755401588539392d));
        Assert.Contains(drawn.Packets.Attempts, attempt => attempt.Primitive is not null);
        Assert.Equal(24, drawn.ProjectedBytesWritten);
    }

    private static (NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging) Load(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelDatSource source, int? linkedRow)
    {
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(source.Linked ? (ushort)0x1000 : (ushort)0, 1, 0, 0, 0);
        NativeBattleModelLoadStep requested = loader.Poll507070(-1, [source], staging);
        NativeBattleModelLoadStep prepared = requested.State.Poll507070(source.Bytes.Length, [source], requested.Staging);
        NativeBattleModelLoadStep loaded = prepared.State.Poll507070(source.Bytes.Length, [source], prepared.Staging);
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(loaded.Bind!, cpu.Cpu, cpu.Rows, 0, linkedRow);
        return (cpu with { Cpu = completed.Cpu, Rows = completed.Rows, AnimationFrames = completed.AnimationCopy!.BindTo(cpu.AnimationFrames) }, loaded.Staging);
    }

    private static NativeBattleModelDatSource Source(bool linked, int meshOffset)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] geometry = new byte[meshOffset + 64];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), meshOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(meshOffset), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(meshOffset + 4), 3);
        short[] vertices = [-10, -10, 1000, 10, -10, 1000, 0, 10, 1000];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(meshOffset + 6 + index * 2), vertices[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(meshOffset + 24), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(meshOffset + 36), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(meshOffset + 38), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(meshOffset + 40), 2);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(geometry.Length - 4), 3);
        byte[] textures = [0, 0, 0, 0, 8, 0, 0, 0];
        byte[][] sections = linked ? [skeleton, geometry, Animation(), new byte[4], new byte[4], [], textures, Animation()] : [skeleton, geometry, Animation(), [], [], textures, Animation(), new byte[4]];
        return new(0, linked, 0, 310, "pilot.dat", Dat(sections).ToImmutableArray());
    }

    private static byte[] Animation() => [1, 0, 0, 0, 8, 0, 0, 0, 1, 0x80, 2, 0];

    private static byte[] Dat(byte[][] sections)
    {
        int cursor = 4 + 4 * (sections.Length + 1);
        byte[] bytes = new byte[cursor + sections.Sum(section => section.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, sections.Length);
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), cursor);
            sections[index].CopyTo(bytes, cursor);
            cursor += sections[index].Length;
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + sections.Length * 4), cursor);
        return bytes;
    }

    private sealed record Pilot(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model);
}
