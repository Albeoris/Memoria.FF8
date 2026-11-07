using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFireBreath230TextureTests
{
    [Fact]
    public void Native_descriptor_encoding_preserves_logical_resource_owner_and_survives_json_restore()
    {
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown().Write(0, new byte[] { 99, 88 }).PromoteResourceStorage().BindPartyResource(0, ImmutableArray.Create<byte>(1, 2), 0, 2);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess();
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleResourcePointer source = new(new(0, 0) { ResourceOnly = true }, null, 2);
        NativeBattleResourcePointer encoded = source.Pin(cpu).WithNativeAddressEncoding();
        Assert.Null(encoded.Cpu!.TryPhysicalAddress());
        Assert.True(encoded.Cpu.ResourceOnly);
        Assert.Same(bank, encoded.CpuPublication);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().WriteResourcePointer(0, encoded);
        Assert.Equal(NativeBattlePartyCpuLayout.Classic.BaseAddress, row.ReadUInt32(0));
        Assert.Equal(new byte[] { 1, 2 }, NativeBattleModelResourceView.Open(cpu, NativeBattleFileStaging.Unknown(), row.ReadResourcePointer(0)!, 2).Read(0, 2));
        Assert.Equal(new byte[] { 99, 88 }, bank.ReadGuest(0, 2));
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(row))!;
        Assert.Equal(NativeBattlePartyCpuLayout.Classic.BaseAddress, restored.ReadUInt32(0));
        Assert.True(restored.ReadResourcePointer(0)!.Cpu!.ResourceOnly);
        NativeBattleEffectArena legacy = NativeBattleEffectArena.Clear().WriteResourcePointer(0, source);
        string legacyJson = NativeBattleTestSnapshots.Json(legacy);
        Assert.DoesNotContain("NativeAddressEncoding", legacyJson);
        NativeBattleEffectArena old = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(legacyJson)!;
        Assert.Throws<NotSupportedException>(() => old.ReadUInt32(0));
        Assert.True(old.ReadResourcePointer(0)!.Cpu!.ResourceOnly);
    }

    [Fact]
    public void Deferred_texture_upload_reads_native_descriptor_dwords_written_after_enqueue()
    {
        const int length = 128 * 256 * 2;
        const int descriptors = 12288;
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)7, length).ToArray());
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(576, 256, 128, 256), source, 0);
        int[] offsets = [0, 1312, 49844, 50292];
        int[] fields = [0, 4, 16, 20];
        for (int index = 0; index < offsets.Length; index++)
        {
            NativeBattleResourcePointer pointer = new(new(0, offsets[index]) { ResourceOnly = true }, null);
            source.WriteResourcePointer(descriptors + fields[index], pointer.WithNativeAddressEncoding());
        }
        source.Write(descriptors + 32, [11, 22]);
        NativeBattleEffectTextureDrain uploaded = queued.Drain505D20(source);
        Assert.Equal(0, uploaded.State.Count);
        Assert.Equal((ushort)0xB018, uploaded.State.ReadVramWord(576, 304));
        Assert.Equal((ushort)0x0196, uploaded.State.ReadVramWord(577, 304));
        Assert.Equal((ushort)0xB538, uploaded.State.ReadVramWord(578, 304));
        Assert.Equal((ushort)0x0196, uploaded.State.ReadVramWord(579, 304));
        Assert.Equal((ushort)0x72CC, uploaded.State.ReadVramWord(584, 304));
        Assert.Equal((ushort)0x0197, uploaded.State.ReadVramWord(585, 304));
        Assert.Equal((ushort)0x748C, uploaded.State.ReadVramWord(586, 304));
        Assert.Equal((ushort)0x0197, uploaded.State.ReadVramWord(587, 304));
        Assert.Equal((ushort)0x160B, uploaded.State.ReadVramWord(592, 304));
        Assert.Equal((ushort)0x0707, uploaded.State.ReadVramWord(576, 256));
        Assert.Equal(Enumerable.Repeat(true, length), source.ReadSnapshot(0, length).Known);
    }
    [Fact]
    public void Encoded_resource_pointer_transport_keeps_task_bytes_and_owner_consistent()
    {
        NativeBattleResourcePointer pointer = new(new(0, 1312) { ResourceOnly = true }, null, 4);
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().WriteResourcePointer(0, pointer.WithNativeAddressEncoding());
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, 48, 2);
        pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 16, source, 4);
        Assert.Equal(0x0196B538u, pool.Slots[0].ReadDword(16));
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(pool.Slots[0]))!;
        Assert.Equal(0x0196B538u, restored.ReadDword(16));
        Assert.True(restored.ReadResourcePointer(16).NativeAddressEncoding);
        Assert.True(restored.ReadResourcePointer(16).Cpu!.ResourceOnly);
        Assert.Throws<InvalidDataException>(() => source.WriteResourcePointer(0, new(null, 0) { NativeAddressEncoding = true }));
    }

}
