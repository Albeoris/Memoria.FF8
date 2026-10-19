using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ModelTests
{
    [Fact]
    public void Constructor_owns_current_file_sections_and_only_actual_initialization_establishes_the_pose_owner()
    {
        var fixture = Fixture();
        NativeBattleSiren95Frame frame = fixture.Frame;
        NativeBattleEffectOwnedModelResource source = Resource();
        NativeBattleChoco97IoBanks banks = NativeBattleProcessFileBanks.Read(frame.Memory).Write(source.Address, source.Bytes.AddRange(new byte[128]));
        frame = frame with { Memory = NativeBattleProcessFileBanks.Write(frame.Memory, banks) };
        NativeBattleEffectOwnedModelResource current = NativeBattleSiren95Model.ReadCurrentResource(frame.Memory, source.Address);
        Assert.Equal(source.Bytes.ToArray(), current.Bytes.ToArray());
        NativeBattleChoco97IoBanks invalid = banks.Write(source.Address + 20, BitConverter.GetBytes(source.Bytes.Length + 129).ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleSiren95Model.ReadCurrentResource(NativeBattleProcessFileBanks.Write(frame.Memory, invalid), source.Address));
        Assert.Null(frame.Memory.Siren95!.ModelState);
        Assert.Throws<NotSupportedException>(() => NativeBattleSiren95Model.Advance746B60(frame, fixture.Address, current, Services()));
        frame = NativeBattleSiren95Model.Initialize8DD0F0(frame, fixture.Address, current, 0, 0xAB123456);
        Assert.Equal(source.Address, frame.Memory.Siren95!.ModelState!.ResourceAddress);
        Assert.Null(frame.Memory.Siren95.ModelState.DecodedPose);
        NativeBattleModulePoolTask task = Read(frame, fixture.Address);
        Assert.Equal((ushort)0, task.ReadWord(0xDE));
        Assert.Equal(0xAB123456u, task.ReadDword(0x5C));
        Assert.Equal(source.Address + 24, task.ReadDword(0xCC));
        Assert.Equal((ushort)4096, task.ReadWord(0x13E));
    }

    [Fact]
    public void Initial_model_selects_real_pose_zero_before_its_first_draw_and_preserves_raw_coordinate_padding()
    {
        var fixture = Fixture();
        var source = Resource();
        var frame = NativeBattleSiren95Model.Initialize8DD0F0(fixture.Frame, fixture.Address, source, 0, 0x32808080);
        frame = Put(frame, fixture.Address, Read(frame, fixture.Address).WriteWord(0x52, 0xABCD));
        int draws = 0;
        var services = Services() with { Draw = (current, pointer, state) =>
        {
            Assert.NotNull(state.DecodedPose);
            var task = Read(current, pointer);
            Assert.Equal(0x00181818u, task.ReadDword(0x58));
            Assert.Equal(unchecked((ushort)-480), task.ReadWord(0x4E));
            Assert.Equal((ushort)3264, task.ReadWord(0x50));
            Assert.Equal((ushort)0xABCD, task.ReadWord(0x52));
            Assert.Equal(pointer + 0x114, task.ReadDword(0x60));
            draws++;
            return current;
        } };
        string saved = JsonSerializer.Serialize(frame);
        var result = NativeBattleSiren95Model.Advance746B60(frame, fixture.Address, source, services);
        Assert.Equal(1, draws);
        Assert.Equal((byte)1, Read(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.False(result.Complete);
        var restored = NativeBattleSiren95Model.Advance746B60(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, fixture.Address, source, services);
        Assert.Equal(JsonSerializer.Serialize(result.Frame), JsonSerializer.Serialize(restored.Frame));
    }

    [Fact]
    public void Projection_publishes_only_written_static_prefix_and_leaves_Arena_and_unknown_tail_intact()
    {
        var fixture = Fixture();
        var shared = fixture.Frame.Memory.Siren95!;
        int offset = checked((int)(NativeBattleSiren95Model.ProjectedAddress746C10 - NativeBattleSiren95State.GlobalAddress));
        shared = shared with { Globals = shared.Globals.MarkUnknown(offset, 32) };
        var frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Siren95 = shared } };
        var arena = frame.Memory.Arena;
        frame = NativeBattleSiren95Model.PublishProjected746C10(frame, Enumerable.Range(0, 24).Select(value => (byte)value).ToImmutableArray(), 16);
        Assert.Equal(Enumerable.Range(0, 16).Select(value => (byte)value), frame.Memory.Siren95!.Read(NativeBattleSiren95Model.ProjectedAddress746C10, 16));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Siren95.Read(NativeBattleSiren95Model.ProjectedAddress746C10 + 16, 1));
        Assert.Same(arena, frame.Memory.Arena);
    }

    [Fact]
    public void Nested_shadow_last_IR0_overwrites_prefix_RGB_while_model_matrix_padding_stays_unknown()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x70 + index * 2, unchecked((ushort)identity.Rotation[index]));
        }
        task = task.WriteDword(0x8C, 4096);
        NativeBattleShadowSource source = new(identity with { Z = 0 }, new(4096, 4096, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());
        NativeBattleScriptedGraphicsMemory controls = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Dqa = 0, Dqb = 2048 << 12 };
        NativeBattleEffectLocalBounds bounds = new(new(-100, -100, -100), new(100, 100, 100));
        NativeBattleCameraProjection projection = new(identity, 216, 160, 108);
        NativeBattleEffectNodeScratch retained = NativeBattleEffectNodeScratch.FromColdProcess().WritePrefix(0, BitConverter.GetBytes(0xABCDEF12u));
        NativeBattleEffectNodeScratch scratch = NativeBattleSiren95Scratch.Model(retained, task, identity, Resource(), false, bounds, projection, source, true, controls, out uint? cursor);
        Assert.Null(cursor);
        Assert.Equal(2048u, BinaryPrimitives.ReadUInt32LittleEndian(scratch.ReadPrefix(0, 4)));
        Assert.Throws<NotSupportedException>(() => scratch.ReadPrefix(22, 2));
        Assert.Equal(NativeBattleSiren95Model.ProjectedAddress746C10, BinaryPrimitives.ReadUInt32LittleEndian(scratch.Bytes.AsSpan()));
        NativeBattleEffectNodeScratch hidden = NativeBattleSiren95Scratch.Model(retained, task.WriteByte(0x30, 0x20), identity, Resource(), false, bounds, projection, source, true, null, out _);
        Assert.Equal(0xABCDEF12u, BinaryPrimitives.ReadUInt32LittleEndian(hidden.ReadPrefix(0, 4)));
    }

    private static NativeBattleSiren95ModelServices Services() => new((current, _, _) => current, (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
    private static (NativeBattleSiren95Frame Frame, uint Address) Fixture()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        var appended = NativeBattleSiren95Actors.AppendTask(fixture.Frame, 0x258EC40, 0x746B60, 0x140, NativeBattleSiren95Controller.MainBank, fixture.Inputs.Packets);
        return (appended.Frame, appended.Address);
    }
    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] data = new byte[140];
        BinaryPrimitives.WriteInt32LittleEndian(data, 4);
        int[] offsets = [24, 88, 96, 132, 140];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4 + index * 4), offsets[index]);
        }
        data[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(40), -1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(96), 4);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(100 + index * 4), 20 + index * 4);
            data[116 + index * 4] = 1;
        }
        NativeBattlePoseSource pose = new(data.AsSpan(24, 64).ToArray().ToImmutableArray(), data.AsSpan(96, 36).ToArray().ToImmutableArray());
        return new(0x19D1018, data.ToImmutableArray(), pose, []);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
