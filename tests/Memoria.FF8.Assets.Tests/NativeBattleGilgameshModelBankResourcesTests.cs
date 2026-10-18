using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshModelBankResourcesTests
{
    [Theory]
    [InlineData(0x596710u)]
    [InlineData(0x596970u)]
    public void Original_counter_zero_initializers_repeat_while_paused_preserve_partial_matrix_padding_and_select_all_sources(uint callback)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        NativeBattleGilgameshModelMemory memory = Retained();
        NativeBattleModulePoolTask task = Task(callback, 0);
        NativeBattleGilgameshModelBankStep first = Run(memory, task, resources, 0x201);
        Assert.Same(task, first.UpdatedTask);
        Assert.Empty(first.Packets);
        uint primary = callback == 0x596710 ? 0x2200F00u : 0x2200FC0u;
        NativeBattleCactuarRawModelState main = first.ModelMemory.Read(primary).State;
        Assert.Equal((short)0x1000, Word(main.Model, 0x40));
        Assert.Equal((short)0x1000, Word(main.Model, 0x48));
        Assert.Equal((short)0x1000, Word(main.Model, 0x50));
        Assert.Equal(unchecked((short)0x8080), Word(main.Model, 0x52));
        Assert.Equal(callback == 0x596710 ? -1000 : 0, Int(main.Model, 0x58));
        Assert.Equal(callback == 0x596710 ? (short)128 : (short)15, Word(main.Control, 2));
        Assert.Equal((byte)1, main.Model[0x72]);
        Assert.Equal((byte)0xA5, main.Control[0]);
        Assert.Equal((byte)0x80, main.Model[0x6D] & 0xF0);
        for (int index = 0; index < 4; index++)
        {
            uint address = callback == 0x596710 ? Clone(index) : Sword(index);
            NativeBattleCactuarRawModelState child = first.ModelMemory.Read(address).State;
            Assert.Equal(callback == 0x596710 ? (byte)index : (byte)7, child.Model[0x6C]);
            Assert.Equal(callback == 0x596710 ? (short)64 : (short)16, Word(child.Control, 2));
            Assert.Equal(Enumerable.Repeat((byte)0x80, 32).ToArray(), child.Model.AsSpan().Slice(0x40, 32).ToArray());
        }
        NativeBattleEffectOwnedModelResource primaryResource = callback == 0x596710 ? resources.Stream749Models[0] : resources.StreamModel751;
        NativeBattleGilgameshModelOwner mainOwner = first.ModelMemory.Read(primary);
        NativeBattleCactuarRawModelState advanced = NativeBattleCactuarRawModel.Advance5A8FD0(main, primaryResource, new(mainOwner.ControlAddress, 4)).State;
        Assert.Equal((byte)2, advanced.Model[0x72]);
        NativeBattleGilgameshModelBankStep repeated = Run(first.ModelMemory.Write(primary, advanced), task, resources, 0x201);
        Assert.Equal((byte)1, repeated.ModelMemory.Read(primary).State.Model[0x72]);
        Assert.Equal(JsonSerializer.Serialize(first.ModelMemory), JsonSerializer.Serialize(repeated.ModelMemory));
    }

    [Fact]
    public void Original_B_draw_uses_depth3_then_four_depth4_clones_and_paused_copy_Z_is_not_cumulative()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        NativeBattleGilgameshModelBankStep initialized = Run(Retained(), Task(0x596710, 0), resources, 0x201);
        NativeBattleGilgameshModelMemory memory = SetModel(initialized.ModelMemory, 0x2200F00, bytes =>
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xAA02);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x5C), 123);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x28), 0x123456);
        });
        NativeBattleGilgameshModelBankStep draw = Run(memory, Task(0x596800, 41), resources, 0x201);
        Assert.Equal(5, draw.Packets.Length);
        NativeBattleCactuarRawModelState primary = draw.ModelMemory.Read(0x2200F00).State;
        NativeBattleGilgameshRawModelDraw expectedPrimary = NativeBattleGilgameshRawModel.Draw595E40(memory.Read(0x2200F00).State, resources.Stream749Models[0], 0x21FF2B0, NativeBattleEffectArena.Clear(), View(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, 3);
        Assert.Equal(JsonSerializer.Serialize(expectedPrimary.Packets), JsonSerializer.Serialize(draw.Packets[0]));
        for (int index = 0; index < 4; index++)
        {
            NativeBattleCactuarRawModelState child = draw.ModelMemory.Read(Clone(index)).State;
            Assert.Equal(unchecked((short)0xAA02), Word(child.Model, 0));
            Assert.Equal(203, Int(child.Model, 0x5C));
            Assert.Equal(Int(primary.Model, 0x28), Int(child.Model, 0x28));
            Assert.Equal((byte)1, child.Model[0x72]);
        }
        NativeBattleGilgameshModelBankStep repeated = Run(draw.ModelMemory, Task(0x596800, 41), resources, 0x201, draw.Arena);
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal(203, Int(repeated.ModelMemory.Read(Clone(index)).State.Model, 0x5C)));
        NativeBattleGilgameshModelBankStep restored = JsonSerializer.Deserialize<NativeBattleGilgameshModelBankStep>(JsonSerializer.Serialize(draw))!;
        Assert.Equal(JsonSerializer.Serialize(repeated), JsonSerializer.Serialize(Run(restored.ModelMemory, Task(0x596800, 41), resources, 0x201, restored.Arena)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_C_advance_gate_reads_old_B_bit8_independently_of_current_C_bit8(bool oldBStops)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        NativeBattleGilgameshModelBankStep initialized = Run(Retained(), Task(0x596970, 0), resources, 0x201);
        NativeBattleGilgameshModelMemory memory = SetModel(initialized.ModelMemory, 0x2200F00, bytes => bytes[0] = oldBStops ? (byte)8 : (byte)0);
        memory = SetModel(memory, 0x2200FC0, bytes => bytes[0] = oldBStops ? (byte)4 : (byte)12);
        NativeBattleGilgameshModelBankStep step = Run(memory, Task(0x596A60, 49), resources, 0);
        Assert.Empty(step.Packets);
        Assert.Equal(2, step.Result);
        Assert.Equal(oldBStops ? (byte)1 : (byte)2, step.ModelMemory.Read(0x2200FC0).State.Model[0x72]);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(oldBStops ? (byte)1 : (byte)2, step.ModelMemory.Read(Sword(index)).State.Model[0x72]);
        }
    }

    [Fact]
    public void Original_sword_draw_copies_matrix_and_color_while_paused_without_copying_flags_and_C_bank_copies_flags()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        NativeBattleGilgameshModelBankStep initialized = Run(Retained(), Task(0x596970, 0), resources, 0x201);
        NativeBattleGilgameshModelMemory memory = SetModel(initialized.ModelMemory, Sword(0), bytes =>
        {
            Identity(bytes);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xAA02);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x28), 0x123456);
        });
        NativeBattleGilgameshModelBankStep swords = Run(memory, Task(0x595D50, 104).WriteWord(14, 105), resources, 0x201);
        Assert.Equal(4, swords.Packets.Length);
        for (int index = 1; index < 4; index++)
        {
            NativeBattleCactuarRawModelState child = swords.ModelMemory.Read(Sword(index)).State;
            Assert.Equal((short)2, Word(child.Model, 0));
            Assert.Equal(swords.ModelMemory.Read(Sword(0)).State.Model.AsSpan().Slice(0x40, 32).ToArray(), child.Model.AsSpan().Slice(0x40, 32).ToArray());
            Assert.Equal(0x123456, Int(child.Model, 0x28));
        }
        NativeBattleGilgameshModelMemory fromC = SetModel(initialized.ModelMemory, 0x2200FC0, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xBB02));
        NativeBattleGilgameshModelBankStep bankC = Run(fromC, Task(0x596A60, 49), resources, 0x201);
        Assert.Equal(5, bankC.Packets.Length);
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal(unchecked((short)0xBB02), Word(bankC.ModelMemory.Read(Sword(index)).State.Model, 0)));
    }

    [Fact]
    public void Original_preinitialized_A_draws_then_advances_and_completes_at105()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshModelResources resources = game.ReadNativeGilgameshModelResources();
        NativeBattleGilgameshModelMemory memory = Retained();
        NativeBattleCactuarRawModelState model = NativeBattleCactuarRawModel.Initialize5A8F40(memory.Read(0x21FF760).State, resources.StreamModel746, resources.StreamModel746.Address, 128, new(0x21FF4B8, 4));
        model = NativeBattleCactuarRawModel.Select5A8FB0(model, resources.StreamModel746, 0, new(0x21FF4B8, 4));
        memory = SetModel(memory.Write(0x21FF760, model), 0x21FF760, Identity);
        NativeBattleGilgameshModelBankStep paused = Run(memory, Task(0x5966B0, 104), resources, 0x201);
        NativeBattleGilgameshModelBankStep step = Run(memory, Task(0x5966B0, 104), resources, 0);
        Assert.Single(step.Packets);
        Assert.Equal(JsonSerializer.Serialize(paused.Packets), JsonSerializer.Serialize(step.Packets));
        Assert.Equal((byte)1, paused.ModelMemory.Read(0x21FF760).State.Model[0x72]);
        Assert.Equal((byte)2, step.ModelMemory.Read(0x21FF760).State.Model[0x72]);
        Assert.Equal(2, step.Result);
    }

    private static NativeBattleGilgameshModelBankStep Run(NativeBattleGilgameshModelMemory memory, NativeBattleModulePoolTask task, NativeBattleGilgameshModelResources resources, uint flags, NativeBattleEffectArena? arena = null) => NativeBattleGilgameshModelBanks.Step(memory, arena ?? NativeBattleEffectArena.Clear(), task, resources, View(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags);
    private static NativeBattleModulePoolTask Task(uint callback, ushort counter) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(12, counter);
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static uint Clone(int index) => 0x21FF800u + (uint)index * 0x9C;
    private static uint Sword(int index) => 0x21FF4F0u + (uint)index * 0x9C;
    private static short Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static int Int(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static NativeBattleGilgameshModelMemory Retained()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        return memory with { Owners = memory.Owners.Select(owner => owner with { State = owner.State with { Model = Enumerable.Repeat((byte)0x80, 0x90).ToImmutableArray(), Control = Enumerable.Repeat((byte)0xA5, 0x34).ToImmutableArray() } }).ToImmutableArray() };
    }

    private static NativeBattleGilgameshModelMemory SetModel(NativeBattleGilgameshModelMemory memory, uint address, Action<byte[]> action)
    {
        NativeBattleCactuarRawModelState state = memory.Read(address).State;
        byte[] bytes = state.Model.ToArray();
        action(bytes);
        return memory.Write(address, state with { Model = bytes.ToImmutableArray() });
    }

    private static void Identity(byte[] bytes)
    {
        bytes.AsSpan(0x40, 32).Clear();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x40), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x48), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x50), 4096);
    }
}
