using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyOwnedHeapTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Owned_load_clone_write_release_and_json_keep_relative_owners_and_pins(bool remastered)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess(remastered ? NativeFieldRuntimeProfile.RemasteredEnImageHash : NativeFieldRuntimeProfile.FF8EnExecutableHash) with { EnemyCursor = 0, EnemyLinkedSlots = 0, ScratchMaximum = 0 };
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0] with { LinkedPhysicalIndex36 = 255 }) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelDatSource source = Source(0x70004);
        staging.WriteByte(NativeBattleModelCpuResources.EnemyAddress + 80, 0xE7);
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, 0, null, false);
        Assert.Equal(0xE7, staging.ReadByte(NativeBattleModelCpuResources.EnemyAddress + 80));
        NativeBattleModelCpuReference geometry = cpu.Rows[0].Require(16);
        Assert.NotNull(geometry.EnemyResourceId);
        Assert.Null(geometry.TryPhysicalAddress());
        Assert.Throws<NotSupportedException>(() => geometry.SharedAddress);
        NativeBattleModelResourceView geometryView = NativeBattleModelResourceView.Open(cpu, staging, geometry, cpu.Rows[0].Lengths[16]);
        Assert.Single(NativeBattleRawGeometryReader.Read(geometryView.Read, 1));
        NativeBattlePoseSource pose = new(NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[0].Require(12), cpu.Rows[0].Lengths[12]).ToImmutableArray(), NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[0].Require(20), cpu.Rows[0].Lengths[20]).ToImmutableArray());
        Assert.Single(pose.Sample(0, 1, 0).Bones);
        NativeBattleResourcePointer pin = new NativeBattleResourcePointer(geometry, null, cpu.Rows[0].Lengths[16]).Pin(cpu);
        NativeBattleModelGeometrySource held = NativeBattleModelGeometrySource.Open(cpu, staging, pin, (_, _) => throw new InvalidOperationException());
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, 1, 0, false);
        Assert.NotEqual(cpu.Rows[0].Require(12).EnemyResourceId, cpu.Rows[1].Require(12).EnemyResourceId);
        Assert.Equal(geometry.EnemyResourceId, cpu.Rows[1].Require(16).EnemyResourceId);
        NativeBattleResourcePointer originalBone = new(cpu.Rows[1].Require(12), null, cpu.Rows[1].Lengths[12]);
        NativeBattleResourcePointer bone = NativeBattleEffect319BoneCache.ResolveRecord(originalBone, 1, 0);
        NativeBattleResourcePointer privatePointer = new(cpu.Rows[1].Require(12), null, cpu.Rows[1].Lengths[12]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, privatePointer, 6, [0xCC]);
        Assert.Equal(0xCC, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(12), 7)[6]);
        Assert.NotEqual(0xCC, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[0].Require(12), 7)[6]);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, new(geometry, null, cpu.Rows[0].Lengths[16]), 9, [0xDD]);
        Assert.Equal(0xDD, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(16), 10)[9]);
        Assert.NotEqual(0xDD, held.Read(9, 1)[0]);
        NativeBattleModelTextureBinding binding = new(Enumerable.Repeat<byte?>(null, 16).ToImmutableArray());
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess();
        cache = cache with { Rows = cache.Rows.SetItem(0, new(3, 16, 0, binding)).SetItem(1, new(3, 16, 0, binding)) };
        int relativeSkeleton = cpu.Rows[1].Require(12).ResourceOffset;
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(cpu, staging, cache, 0);
        Assert.Equal(relativeSkeleton, cpu.Rows[1].Require(12).ResourceOffset);
        Assert.Equal(0, NativeBattleEffect319BoneCache.ResolveIndex(new(cpu.Rows[1].Require(12), null, cpu.Rows[1].Lengths[12]), 1, bone));
        Assert.Equal(0xDD, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(16), 10)[9]);
        Saved restored = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(new Saved(cpu, staging, pin)));
        Assert.Equal(held.Read(0, held.Owner.Length!.Value), NativeBattleModelResourceView.Open(restored.Cpu, restored.Staging, restored.Pin, restored.Pin.Length!.Value).Read(0, restored.Pin.Length.Value));
        cache = cache.Release5080D0(0);
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(restored.Cpu, restored.Staging, cache, 1);
        Assert.Empty(cpu.EnemyResources!);
        Assert.Equal(held.Read(0, 32), NativeBattleModelResourceView.Open(cpu, staging, pin, 32).Read(0, 32));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceView.Open(cpu, staging, pin, pin.Length!.Value + 1));
    }

    [Fact]
    public void Fresh_enemy_io_retains_DAT_without_changing_guest()
    {
        NativeBattleModelDatSource source = Source(0x70004);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        staging.WriteByte(NativeBattleFileStaging.Address, 0xA7);
        NativeBattleModelLoader loader = NativeBattleModelLoader.Queue507080(16, 0, 0, 0, 0);
        NativeBattleModelLoadStep step = loader.Poll507070(-1, [source], staging);
        Assert.Equal(source, step.State.Task!.ResourceSource);
        Assert.Equal(0xA7, step.Staging.ReadByte(NativeBattleFileStaging.Address));
        NativeBattleModelLoadTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelLoadTask>(NativeBattleTestSnapshots.Json(step.State.Task));
        Assert.Equal(source.Bytes, restored.ResourceSource!.Bytes);
    }

    [Fact]
    public void Special143_keeps_model142_owners_and_distinct_information_until_last_release()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 0, EnemyLinkedSlots = 0, ScratchMaximum = 0 };
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0] with { LinkedPhysicalIndex36 = 255 }) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelDatSource original = Source(0x10004) with { ModelId = 142 };
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, original, 0, null, false);
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, original with { ModelId = 143 }, 1, 0, true);
        foreach (int field in new[] { 12, 20, 24, 44 })
        {
            Assert.Equal(cpu.Rows[0].Fields[field], cpu.Rows[1].Fields[field]);
        }
        Assert.NotEqual(cpu.Rows[0].Require(12).EnemyResourceId, cpu.Rows[1].Require(28).EnemyResourceId);
        Assert.Equal(new NativeBattleModelCpuReference(4, 0), cpu.Rows[1].Require(16));
        NativeBattleModelTextureBinding binding = new(Enumerable.Repeat<byte?>(null, 16).ToImmutableArray());
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess();
        cache = cache with { Rows = cache.Rows.SetItem(0, new(3, 142, 0, binding)).SetItem(1, new(3, 143, 0, binding)) };
        byte[] animation = NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(20), 12);
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(cpu, staging, cache, 0);
        Assert.Equal(animation, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(20), 12));
        Assert.Equal(2, cpu.EnemyResources!.Count);
        cache = cache.Release5080D0(0);
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(cpu, staging, cache, 1);
        Assert.Empty(cpu.EnemyResources!);
    }

    [Fact]
    public void Reset_selects_owned_storage_preserves_identity_generation_and_old_JSON_keeps_guest_adapter()
    {
        NativeBattleModelCpuResources current = NativeBattleModelCpuResources.FromColdProcess() with { NextEnemyResourceId = 12 };
        NativeBattleModelCpuResources reset = current.InvalidateResourceExtents();
        Assert.Empty(reset.EnemyResources!);
        Assert.Equal(12, reset.NextEnemyResourceId);
        System.Text.Json.Nodes.JsonObject json = System.Text.Json.Nodes.JsonNode.Parse(NativeBattleTestSnapshots.Json(current))!.AsObject();
        json.Remove("EnemyResources");
        json.Remove("NextEnemyResourceId");
        NativeBattleModelCpuResources legacy = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(json.ToJsonString());
        Assert.Null(legacy.EnemyResources);
        NativeBattleModelCpuReference original = new(3, 48);
        NativeBattleModelCpuReference restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuReference>(NativeBattleTestSnapshots.Json(original));
        Assert.Equal(original, restored);
        Assert.Equal(NativeBattleModelCpuResources.EnemyAddress + 48, restored.TryPhysicalAddress());
        Assert.Equal(0x1D999C0u, new NativeBattleModelCpuReference(4, 0).TryPhysicalAddress());
    }

    internal static NativeBattleModelDatSource Source(int geometryLength)
    {
        NativeBattleModelDatSource template = NativeBattleModelResourceCapacityTests.Source(false, geometryLength - 64);
        byte[] skeleton = template.Bytes.AsSpan(template.Section(0), template.Section(1) - template.Section(0)).ToArray();
        byte[] geometry = template.Bytes.AsSpan(template.Section(1), template.Section(2) - template.Section(1)).ToArray();
        byte[] animation = new byte[0x21000];
        new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0x80, 2, 0 }.CopyTo(animation, 0);
        byte[][] sections = [skeleton, geometry, animation, [], [], [], new byte[4], new byte[4], new byte[4], [], [0, 0, 0, 0, 8, 0, 0, 0]];
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
        return new(16, false, 0, 166, "model.dat", bytes.ToImmutableArray());
    }

    private sealed record Saved(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleResourcePointer Pin);
}
