using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlCleanupTests
{
    [Fact]
    public void Actual355_restores_both_backups_and_unlinks_only_child_without_completing_main_or_reseeding_flag()
    {
        var (memory, effect) = Fixture();
        ImmutableArray<byte> backup = Enumerable.Repeat((byte)0xA5, NativeBattleQuezacotlExecutableMemory.Length).ToImmutableArray();
        memory = memory with { GuardianWorkBuffer = memory.GuardianWorkBuffer!.Write(0, backup.AsSpan()), QuezacotlCompletion17A8 = null, QuezacotlPause = 1 };
        byte[] before = memory.Arena!.Read(0, 0x8D0);
        NativeBattleQuezacotlWorkspaceWrite restored = NativeBattleQuezacotlCleanup.RestoreAndUnlink355(memory, effect, current => current);
        Assert.Equal(backup.ToArray(), NativeBattleStorageSnapshots.Capture(restored.Memory.QuezacotlExecutableMemory!).Data.ToArray());
        NativeBattleEffectTaskPool child = restored.Effect.Pools[1];
        Assert.Null(child.Head);
        Assert.Null(child.Tail);
        Assert.Equal((ushort)0, child.Slots[0].Flags);
        Assert.Equal((ushort)355, child.Slots[0].ReadWord(12));
        Assert.Equal(new byte[2], restored.Memory.Arena!.Read(0, 2));
        Assert.Equal(before.AsSpan(2).ToArray(), restored.Memory.Arena.Read(2, before.Length - 2));
        Assert.Equal(effect.ReturnedDescriptor, restored.Effect.ReturnedDescriptor);
        Assert.Null(restored.Memory.QuezacotlCompletion17A8);
        Assert.Equal(SnapshotJson(restored), SnapshotJson(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleQuezacotlWorkspaceWrite>(SnapshotJson(restored)))));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlCleanup.RestoreAndUnlink355(restored.Memory, restored.Effect, current => current));
    }

    [Theory]
    [InlineData(0, (byte)0, false)]
    [InlineData(0, (byte)255, true)]
    [InlineData(1, (byte)0, false)]
    [InlineData(1, (byte)255, false)]
    public void Child_empty_wait_reads_completion_only_when_unpaused_and_does_not_increment_main_counter(int pause, byte completion, bool complete)
    {
        var (memory, effect) = Fixture();
        NativeBattleQuezacotlWorkspaceWrite restored = NativeBattleQuezacotlCleanup.RestoreAndUnlink355(memory, effect, current => current);
        NativeBattleOriginalEffectState after = NativeBattleQuezacotlChild.FinishMain6C3760(restored.Memory with { QuezacotlPause = pause, QuezacotlCompletion17A8 = completion }, restored.Effect);
        Assert.Equal(complete ? 0u : effect.ReturnedDescriptor, after.ReturnedDescriptor);
        Assert.Equal((ushort)777, after.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(complete ? null : (int?)0, after.Pools[0].Head);
        Assert.Equal(complete ? (ushort)0 : (ushort)1, after.Pools[0].Slots[0].Flags);
    }

    [Fact]
    public void Restore_precedes_scheduling_and_retains_its_actual_completion_write_before_child_unlink()
    {
        var (memory, effect) = Fixture();
        int scheduled = 0;
        NativeBattleQuezacotlWorkspaceWrite restored = NativeBattleQuezacotlCleanup.RestoreAndUnlink355(memory with { QuezacotlCompletion17A8 = null }, effect, current =>
        {
            Assert.Equal(memory.GuardianWorkBuffer!.Read(0, NativeBattleQuezacotlExecutableMemory.Length), NativeBattleStorageSnapshots.Capture(current.QuezacotlExecutableMemory!).Data.ToArray());
            Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(current.Arena!.Read(0, 2)));
            scheduled++;
            return current with { QuezacotlCompletion17A8 = 0 };
        });
        Assert.Equal(1, scheduled);
        Assert.Equal((byte)0, restored.Memory.QuezacotlCompletion17A8);
        Assert.Equal((ushort)0, restored.Effect.Pools[1].Slots[0].Flags);
        Assert.Equal(new byte[2], restored.Memory.Arena!.Read(0, 2));
    }

    [Fact]
    public void Unknown_old_completion_is_not_defaulted_and_is_not_read_during_pause_or_live_child()
    {
        var (memory, effect) = Fixture();
        memory = memory with { QuezacotlCompletion17A8 = null };
        string json = SnapshotJson(memory);
        Assert.DoesNotContain("QuezacotlCompletion17A8", json);
        Assert.Null(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!).BeginLoad(115).QuezacotlCompletion17A8);
        NativeBattleOriginalEffectState active = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect);
        Assert.Equal((ushort)778, active.Pools[0].Slots[0].ReadWord(12));
        NativeBattleQuezacotlWorkspaceWrite restored = NativeBattleQuezacotlCleanup.RestoreAndUnlink355(memory, effect, current => current);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlChild.FinishMain6C3760(restored.Memory, restored.Effect));
        NativeBattleOriginalEffectState paused = NativeBattleQuezacotlChild.FinishMain6C3760(restored.Memory with { QuezacotlPause = 1 }, restored.Effect);
        Assert.Equal((ushort)777, paused.Pools[0].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Aux_phase2_completion_is_visible_to_main_in_that_same_callback()
    {
        var (memory, effect) = Fixture();
        NativeBattleQuezacotlWorkspaceWrite restored = NativeBattleQuezacotlCleanup.RestoreAndUnlink355(memory, effect, current => current);
        memory = restored.Memory;
        effect = restored.Effect;
        (NativeBattleAuxiliaryState aux, _) = NativeBattleAuxiliaryState.Clear().AppendCommonTextureLoad508630(0x20E2908, 0x25217A8, (_, value) => memory = memory with { QuezacotlCompletion17A8 = value });
        aux = aux.Advance(new()
        {
            RequestFile5084B0 = (_, _) =>
            {
            }
        }).State;
        aux = aux.Advance(new()
        {
            ReadEffectIoResult508470 = () => 0,
            EnqueueTexture505E30 = _ =>
            {
            }
        }).State;
        NativeBattleOriginalEffectState waiting = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect);
        Assert.Equal(effect.ReturnedDescriptor, waiting.ReturnedDescriptor);
        aux = aux.Advance(new() { WriteCompletionByte = (_, value) => memory = memory with { QuezacotlCompletion17A8 = value } }).State;
        NativeBattleOriginalEffectState completed = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect);
        Assert.Equal(0u, completed.ReturnedDescriptor);
        Assert.Null(aux.Head);
        Assert.Null(completed.Pools[0].Head);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(115);
        memory = memory with { QuezacotlExecutableMemory = new(new byte[NativeBattleQuezacotlExecutableMemory.Length].ToImmutableArray()) };
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x2521748, 0x2521758, 20, 1);
        (main, _) = main.Append(0x6C3760);
        main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, 777).WriteByte(15, 1)) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, 355)) };
        byte[] raw = new byte[0x8D0];
        BinaryPrimitives.WriteUInt16LittleEndian(raw, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), 0x6C3940);
        child.Slots[0].ReadRawPayloadSpan().CopyTo(raw.AsSpan(12));
        memory = memory with { Arena = memory.Arena!.Write(0, raw) };
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [main, child], ReturnedDescriptor = main.DescriptorAddress };
        return (memory, effect);
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

}
