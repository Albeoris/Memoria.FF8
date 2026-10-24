using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64ControllerTests
{
    private const uint Common = 0x20DFAB8;

    [Fact]
    public void Eighteen_script_births_clear_the_following_header_and_keep_main_linked()
    {
        NativeBattleEffect64Frame frame = Frame();
        uint main = Common + MainOffset;
        frame = Write(frame, main, Read(frame, main) with { State = 0xABCD });
        frame = AdvanceMain6E53F0(frame, main, Resources(), Inputs()).Frame;
        NativeBattleEffectTaskPool scripts = frame.Effect.Pools[2];
        Assert.Equal(0, scripts.Head);
        Assert.Equal(17, scripts.Tail);
        for (int index = 0; index < 18; index++)
        {
            Assert.Equal(1, scripts.Slots[index].Flags);
            Assert.Equal(0x6E5DD0u, scripts.Slots[index].Callback);
            Assert.Equal(index == 17 ? (int?)null : index + 1, scripts.Slots[index].Next);
            Assert.Equal(ScriptAddresses[index] + 2, scripts.Slots[index].ReadDword(0x18));
        }
        NativeBattleEffectTaskPool mainPool = frame.Effect.Pools[1];
        Assert.Equal(0, mainPool.Head);
        Assert.Equal(0, mainPool.Tail);
        Assert.Equal(0, mainPool.Slots[0].Flags);
        Assert.Equal(0, mainPool.Slots[0].State);
        Assert.Null(mainPool.Slots[0].Next);
        Assert.Equal(MainCallback, mainPool.Slots[0].Callback);
        Assert.Equal(1, mainPool.Slots[0].ReadWord(12));
        mainPool.Validate();
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(MainOffset, 1));
    }

    [Fact]
    public void Linked_main_with_cleared_allocation_flag_still_executes_and_survives()
    {
        NativeBattleEffect64Frame frame = Frame();
        uint main = Common + MainOffset;
        frame = Write(frame, main, Read(frame, main).WriteWord(12, 12) with { Flags = 0 });
        (NativeBattleEffect64Frame result, int survivors) = AdvancePool(frame, 1, Resources(), Inputs());
        Assert.Equal(1, survivors);
        Assert.Equal(13, Read(result, main).ReadWord(12));
        Assert.Equal(0, Read(result, main).Flags);
    }

    [Fact]
    public void Root_retirement_captures_live_children_without_waiting_or_clearing_them()
    {
        NativeBattleEffect64Frame frame = Frame();
        NativeBattleEffect64Resources resources = Resources();
        uint main = Common + MainOffset;
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 2).WriteByte(15, 1));
        frame = Write(frame, main, Read(frame, main).WriteWord(12, 170));
        (frame, uint child) = SpawnScript6E5D80(frame, 0x1D975CC, main + 0x18, ScriptAddresses[0], resources);
        NativeBattleEffect64Frame result = Advance(frame, resources, Inputs());
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Empty(result.Effect.Pools);
        Assert.NotNull(result.State.RootBankStorage);
        int childOffset = checked((int)(child - Common));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena!.Read(childOffset, 2)));
        Assert.Equal(0x6E5DD0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena.Read(childOffset + 8, 4)));
        Assert.Equal(ScriptAddresses[0] + 4, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena.Read(childOffset + 0x18, 4)));
        NativeBattleEffect64Frame restored = JsonSerializer.Deserialize<NativeBattleEffect64Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(result.Memory.Arena.Read(childOffset, ScriptStride), restored.Memory.Arena!.Read(childOffset, ScriptStride));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(169, 170)]
    public void Main_clock_is_signed_and_wraps_as_a_word(short age, ushort next)
    {
        NativeBattleEffect64Frame frame = Frame();
        uint main = Common + MainOffset;
        frame = Write(frame, main, Read(frame, main).WriteWord(12, unchecked((ushort)age)));
        List<NativeBattleEffect64Operation> operations = [];
        NativeBattleEffect64Step result = AdvanceMain6E53F0(frame, main, Resources(), Inputs(operations));
        Assert.Equal(0, result.Result);
        Assert.Equal(next, Read(result.Frame, main).ReadWord(12));
        Assert.Equal(age >= 0, operations.Any(operation => operation is NativeBattleEffect64DrawBody6E56A0));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Main_retirement_restores_only_native_fields_before_drawing_or_clock_increment()
    {
        NativeBattleEffect64Frame frame = Frame();
        uint main = Common + MainOffset;
        frame = Write(frame, main, Read(frame, main).WriteWord(12, 170));
        List<NativeBattleEffect64Operation> operations = [];
        NativeBattleEffect64Step result = AdvanceMain6E53F0(frame, main, Resources(), Inputs(operations));
        Assert.Equal(2, result.Result);
        Assert.Equal(170, Read(result.Frame, main).ReadWord(12));
        Assert.IsType<NativeBattleEffect64ModelFlags>(operations[0]);
        Assert.Equal(3u, Assert.IsType<NativeBattleEffect64ModelMeshMask>(operations[1]).Mask);
        Assert.IsType<NativeBattleEffect64ClearGeometryBit15_6E5C90>(operations[2]);
        Assert.DoesNotContain(operations, operation => operation is NativeBattleEffect64DrawBody6E56A0 or NativeBattleEffect64StopVibration4A2940);
        Assert.Equal(7, operations.Count);
    }

    [Theory]
    [InlineData(18, false)]
    [InlineData(19, true)]
    [InlineData(22, true)]
    [InlineData(23, false)]
    [InlineData(24, true)]
    [InlineData(27, true)]
    [InlineData(28, false)]
    [InlineData(43, true)]
    [InlineData(46, true)]
    [InlineData(47, false)]
    public void Flash_windows_deliver_full_viewport_batches_without_stage_operations(ushort age, bool flashes)
    {
        NativeBattleEffect64Frame frame = Frame();
        uint main = Common + MainOffset;
        frame = Write(frame, main, Read(frame, main).WriteWord(12, age));
        List<NativeBattleEffect64Operation> operations = [];
        AdvanceMain6E53F0(frame, main, Resources(), Inputs(operations));
        Assert.Equal(flashes ? 1 : 0, operations.OfType<NativeBattleEffect64DeliverBatch>().Count());
        foreach (NativeBattleEffect64DeliverBatch delivery in operations.OfType<NativeBattleEffect64DeliverBatch>())
        {
            Assert.Equal(CursorGlobal, delivery.CursorGlobal);
            Assert.Equal(24, delivery.Batch.AllocationBytes);
        }
    }

    [Fact]
    public void Physical_writer_preserves_untouched_pointer_sidecars_and_copies_observed_sources()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 48, 1);
        NativeBattleResourcePointer retained = new(new(0, 0x123), null, 20);
        pool = pool with { Slots = [pool.Slots[0].WriteResourcePointer(16, retained).MarkPayloadUnknown(40, 2)] };
        pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 24, NativeBattleEffectArena.Clear().Write(0, [7, 8]), 2);
        Assert.Equal(retained, pool.Slots[0].ReadResourcePointer(16));
        Assert.Throws<NotSupportedException>(() => pool.Slots[0].ReadWord(40));
        NativeBattleResourcePointer copied = new(null, 0x12340000, 24);
        pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 28, NativeBattleEffectArena.Clear().WriteResourcePointer(0, copied), 4);
        Assert.Equal(copied, pool.Slots[0].ReadResourcePointer(28));
        Assert.Equal(retained, pool.Slots[0].ReadResourcePointer(16));
    }

    [Fact]
    public void Physical_header_writes_preserve_real_links_and_reject_invalid_callbacks_and_cycles()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 32, 2);
        (pool, _) = pool.Append(0x111);
        (pool, _) = pool.Append(0x222);
        pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 0, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
        Assert.Equal(0, pool.Slots[0].Flags);
        Assert.Equal(1, pool.Slots[0].Next);
        Assert.Equal(0x111u, pool.Slots[0].Callback);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.WriteBankSpan(pool, 8, NativeBattleEffectArena.Clear(), 4));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.WriteBankSpan(pool, 32 + 4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x200u)), 4));
    }

    private static NativeBattleEffect64Frame Frame()
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        NativeBattleOriginalEffectState effect = new(64, 63, new(64, Entry, Initializer), null, null) { Pools = [root], ReturnedDescriptor = RootDescriptor };
        NativeBattleEffect64Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(123), NativeBattleEffect64State.FromColdProcess() with { RootBankStorage = null });
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, Common).WriteDword(CasterGlobal, 0x1D975CC) };
        frame = InitializeSecondaryBanks(frame);
        (frame, _) = Append(frame, MainDescriptor, MainCallback);
        return frame;
    }

    private static NativeBattleEffect64Inputs Inputs(List<NativeBattleEffect64Operation>? operations = null) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, null!, null!, (frame, operation) =>
    {
        operations?.Add(operation);
        return frame;
    });
    private static NativeBattleEffect64Resources Resources() => new(new("texture", []), new("camera", []), new("vibration", []), 0x668B3, [], ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, [], ScriptLengths.ToImmutableDictionary(pair => pair.Key, pair => new byte[pair.Value].ToImmutableArray()));
}
