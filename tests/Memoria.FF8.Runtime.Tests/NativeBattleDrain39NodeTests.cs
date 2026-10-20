using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrain39NodeTests
{
    [Fact]
    public void Initial_context_uses_absolute_directory_without_relocating_the_backward_bank_and_retains_JSON_owner()
    {
        var fixture = Fixture();
        NativeBattleDrain39Resources resources = Resources();
        NativeBattleDrain39NodeStep step = Advance(fixture.Frame, fixture.Address, resources, fixture.Inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(NativeBattleDrain39Nodes.DirectoryAddress + 4, task.ReadDword(0x34 + 0x224));
        Assert.Equal(NativeBattleDrain39Nodes.DirectoryAddress + 0x44, task.ReadDword(0x34 + 0x228));
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x1C));
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x1E));
        Assert.Equal((ushort)45, task.ReadWord(0x34 + 0x1A));
        Assert.Equal(3u, task.ReadDword(0x34 + 4));
        Assert.Equal(4u, task.ReadDword(0x34 + 8));
        Assert.Equal((ushort)1, task.ReadWord(0x34 + 0x20));
        Assert.Equal(resources.NodeBank.ToArray(), step.Frame.Memory.Drain39Bank!.Data!.Value.ToArray());
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Drain39Bank.Data.Value.AsSpan()));
        Assert.Null(fixture.Frame.Memory.Drain39Bank!.Data);
        NativeBattleDrain39Frame independent = JsonSerializer.Deserialize<NativeBattleDrain39Frame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(independent, fixture.Address, resources, fixture.Inputs)));
        Assert.Throws<NotSupportedException>(() => Advance(fixture.Frame with { Memory = fixture.Frame.Memory with { Drain39Bank = null } }, fixture.Address, resources, fixture.Inputs));
    }

    [Fact]
    public void Signed_delay_does_not_materialize_the_bank_and_completion_waits_for_owned_children()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x24, 65534).WriteWord(0x298, 32767));
        NativeBattleDrain39Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleDrain39NodeStep step = Advance(frame, fixture.Address, Resources(), fixture.Inputs);
        Assert.Null(step.Frame.Memory.Drain39Bank!.Data);
        Assert.Equal((ushort)65535, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address).ReadWord(0x24));
        hierarchy = new(step.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteByte(0x29, 1).WriteByte(0x28, 1).WriteWord(0x34 + 0x20, 2));
        frame = step.Frame with { Effect = step.Frame.Effect with { Pools = hierarchy.Pools } };
        step = Advance(frame, fixture.Address, Resources(), fixture.Inputs);
        Assert.Equal(0, step.Result);
        hierarchy = new(step.Frame.Effect.Pools);
        Assert.Equal((byte)1, hierarchy.Read(fixture.Address).ReadByte(0x26));
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteByte(0x28, 0));
        step = Advance(step.Frame with { Effect = step.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Address, Resources(), fixture.Inputs);
        Assert.Equal(2, step.Result);
    }

    [Fact]
    public void Bank_JSON_requires_the_nullable_data_member_and_validates_materialized_directory_flag()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleDrain39BankState>("{}"));
        NativeBattleDrain39BankState state = JsonSerializer.Deserialize<NativeBattleDrain39BankState>("{\"Data\":null}")!;
        state.Validate();
        Assert.Null(state.Data);
        Assert.Throws<InvalidDataException>(() => new NativeBattleDrain39BankState([]).Validate());
        byte[] bytes = Resources().NodeBank.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)(NativeBattleDrain39Nodes.DirectoryAddress - NativeBattleDrain39Nodes.BankAddress)), 0);
        Assert.Throws<InvalidDataException>(() => new NativeBattleDrain39BankState(bytes.ToImmutableArray()).Validate());
    }

    [Fact]
    public void Native_flash_writes_two_fullscreen_packets_then_releases_at_the_script_terminal()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend added = hierarchy.Append8DC540(2, 0x851900, 60, hierarchy.Address(0, 0), (_, _, _) => new(2, 3));
        hierarchy = added.Hierarchy.Write(added.Address, added.Hierarchy.Read(added.Address).WriteDword(0x30, 0x15D190C));
        NativeBattleDrain39Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleDrain39Resources resources = Resources() with { FirstFlash = [0x007F7F7F, 0x003F3F3F, 0xFF000000] };
        List<NativeBattleDrain39Operation> operations = [];
        for (int tick = 0; tick < 3; tick++)
        {
            NativeBattleDrain39LeafStep step = NativeBattleDrain39Simple.Advance(frame, added.Address, resources, (current, operation) =>
            {
                operations.Add(operation);
                return current;
            });
            frame = step.Frame;
            Assert.Equal(tick == 2 ? 2 : 0, step.Result);
        }
        Assert.Equal(2, operations.Count);
        for (int tick = 0; tick < 2; tick++)
        {
            NativeBattleModulePacketBatch batch = Assert.IsType<NativeBattleDrain39PacketOperation>(operations[tick]).Batch;
            Assert.Equal(36, batch.AllocationBytes);
            Assert.Equal(2, batch.Primitives.Length);
            Assert.Equal(tick == 0 ? 0x2A7F7F7Fu : 0x2A3F3F3Fu, batch.Primitives[0].Primitive.Words[1]);
            Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, batch.Primitives[1].Primitive.Words);
        }
    }

    private static NativeBattleDrain39NodeStep Advance(NativeBattleDrain39Frame frame, uint address, NativeBattleDrain39Resources resources, NativeBattleDrain39ControllerInputs inputs) => NativeBattleDrain39Nodes.Advance(frame, address, resources, inputs, new(123), new(inputs.Camera, 320, 160, 112), 0, NativeBattleLogicalOrdering.Clear(), new(0, 0xFFFFFF, 4096, 0));

    private static NativeBattleDrain39Resources Resources()
    {
        byte[] bytes = new byte[NativeBattleDrain39Nodes.BankLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)(NativeBattleDrain39Nodes.DirectoryAddress - NativeBattleDrain39Nodes.BankAddress)), 1);
        return new(new("unused", []), 0x49433, bytes.ToImmutableArray(), [], []);
    }

    private static (NativeBattleDrain39Frame Frame, uint Address, NativeBattleDrain39ControllerInputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 23, 39, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(2);
        NativeBattleOriginalEffectState selected = new(39, 38, new(39, 0x8513D0, 0x8513B0), new(2, 1, 24, 0, 0, 0), null) { ResourcePointers = [new(0x269DA1C, 256)] };
        NativeBattleDrain39Frame frame = NativeBattleDrain39Controller.Initialize8513D0((NativeBattleEffectMemory.FromColdProcess() with { Drain39Bank = NativeBattleDrain39BankState.FromColdProcess() }).BeginLoad(38), selected, packets, Resources());
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(1, 0x851830, 88, hierarchy.Address(0, 0), (_, _, _) => new(2, 3));
        NativeBattleEffectHierarchyAppend child = first.Hierarchy.Append8DC540(3, 0x851A80, 676, first.Address, (_, _, _) => new(2, 3));
        hierarchy = child.Hierarchy;
        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteDword(0x30, NativeBattleDrain39Nodes.DirectoryAddress).WriteWord(0x29E, 45));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State with { PassEmitterCount = 0, PassGeometryCount = 0 } };
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleGraphicsModel Model(int index)
        {
            NativeBattleVector position = index == 2 ? new(0, 100, 0) : index == 3 ? new(-10, -200, 30) : new(20, -400, -50);
            short angle36 = index == 2 ? (short)-3 : index == 3 ? (short)10 : (short)-20;
            return new(index, 0) { Transform = new(new(0, 0, 0), position, 0, angle36, 4096), GeometryBounds52 = new(new(-100, -200, -300), new((short)(600 + index * 100), (short)(-700 - index * 10), (short)(900 + index * 5))) };
        }
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, child.Address, new(packets, camera, Model, _ => source));
    }
}
