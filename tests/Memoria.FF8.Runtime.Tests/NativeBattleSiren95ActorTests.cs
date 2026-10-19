using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ActorTests
{
    [Fact]
    public void Stage_one_uses_real_physical_pools_and_sparse_constructor_operands()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = WithStage(fixture.Frame, 1);
        frame = NativeBattleSiren95Actors.Spawn73BCF0(frame, NativeBattleSiren95Controller.MainBank, new(fixture.Inputs.Packets, (_, _, _, _) => throw new InvalidOperationException("No model was initialized.")));
        NativeBattleEffectTaskPool surface = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x258A010);
        NativeBattleModulePoolTask task = surface.Slots[surface.Head!.Value];
        Assert.Equal(0x73C1A0u, task.Callback);
        Assert.Equal(0x16A5380u, task.ReadDword(0x74));
        Assert.Equal(0x94u, task.ReadDword(0x78));
        Assert.Equal((ushort)0, task.ReadWord(0x80));
        Assert.Equal((ushort)0, task.ReadWord(0x82));
        Assert.Equal(NativeBattleSiren95Controller.MainBank, task.ReadDword(0x10));
        NativeBattleEffectTaskPool particles = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x257FA90);
        Assert.Equal(new uint[] { 0x743C70, 0x744060, 0x746920 }, particles.Slots.Where(slot => slot.Callback != 0).Select(slot => slot.Callback));
        Assert.Equal((byte)4, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(NativeBattleSiren95Controller.MainBank).ReadByte(0x28));
    }

    [Fact]
    public void Surface_initializes_and_draws_once_publishes_selected_raw_vertex_cache_and_updates_UV_after_draw()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = NativeBattleSiren95Actors.Spawn73BCF0(WithStage(fixture.Frame, 1), NativeBattleSiren95Controller.MainBank, new(fixture.Inputs.Packets, (_, _, _, _) => throw new InvalidOperationException()));
        const uint address = 0x2587670;
        NativeBattleSiren95State shared = frame.Memory.Siren95!;
        uint director = shared.DirectorAddress1533010!.Value;
        shared = shared.Write(director + 8, BitConverter.GetBytes(0x5678007Bu)).Write(director + 0xC, BitConverter.GetBytes(0xBEEF0011u));
        frame = frame with { Memory = frame.Memory with { Siren95 = shared } };
        NativeBattleGfAnimationSource source = Source();
        List<NativeBattleGfGeometryBatch> batches = [];
        NativeBattleSiren95ProgramDrawServices services = new((current, batch, _) =>
        {
            batches.Add(batch);
            Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).ReadWord(0x8C));
            return current;
        });
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleSiren95Frame Draw(NativeBattleSiren95Frame current, uint task) => NativeBattleSiren95ProgramDrawing.Advance73C280(current, task, source, identity, new(null, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, services);
        NativeBattleSiren95TaskStep result = NativeBattleSiren95Actors.Advance73C1A0(frame, address, source, Draw);
        Assert.Single(batches);
        NativeBattleModulePoolTask surface = new NativeBattleEffectHierarchy(result.Frame.Effect.Pools).Read(address);
        Assert.Equal((byte)1, surface.ReadByte(0x29));
        Assert.Equal((ushort)123, surface.ReadWord(0x1C));
        Assert.Equal((ushort)0, surface.ReadWord(0x1E));
        Assert.Equal((ushort)0xE011, surface.ReadWord(0x20));
        Assert.Equal((ushort)0xBEEF, surface.ReadWord(0x22));
        Assert.Equal((ushort)4, surface.ReadWord(0x8C));
        Assert.Equal((ushort)122, surface.ReadWord(0x90));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(surface.Payload.AsSpan().Slice(0x94 - 12 + 4)));
        Assert.Equal(source.Data.AsSpan().Slice(12 + 12 + 0x48, 8).ToArray(), result.Frame.Memory.Siren95!.Read(0x258E9A0, 8));
        NativeBattleSiren95Frame finished = WithStage(result.Frame, 2);
        string saved = JsonSerializer.Serialize(finished);
        result = NativeBattleSiren95Actors.Advance73C1A0(finished, address, source, (_, _) => throw new InvalidOperationException("Completed surface must not draw."));
        Assert.True(result.Complete);
        Assert.Equal((ushort)8, new NativeBattleEffectHierarchy(result.Frame.Effect.Pools).Read(address).ReadWord(0x8C));
        var restored = NativeBattleSiren95Actors.Advance73C1A0(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, address, source, (_, _) => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(result.Frame), JsonSerializer.Serialize(restored.Frame));
    }

    [Fact]
    public void Stage_companion_preserves_color_code_and_clears_only_the_actual_stage_flag_bit()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = NativeBattleSiren95Actors.Spawn73BCF0(WithStage(fixture.Frame, 1), NativeBattleSiren95Controller.MainBank, new(fixture.Inputs.Packets, (_, _, _, _) => throw new InvalidOperationException()));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x257FA90);
        uint address = pool.BankAddress + 2u * pool.Stride;
        NativeBattleStageState stage = new(0, 0, 0, Enumerable.Range(0, 4).Select(_ => new NativeBattleStageGroup(255, -1, 0xAA123456, 0, 0, 0, 0, null, [])).ToImmutableArray());
        var result = NativeBattleSiren95Actors.Advance746920(frame, address, stage);
        Assert.All(result.Stage.Groups, group =>
        {
            Assert.Equal((short)0x600, group.ColorDepth);
            Assert.Equal(0xAA000000u, group.ColorFlags);
            Assert.Equal((byte)255, group.Flags);
        });
        result = NativeBattleSiren95Actors.Advance746920(WithStage(result.Frame, 3), address, result.Stage);
        Assert.True(result.Complete);
        Assert.All(result.Stage.Groups, group =>
        {
            Assert.Equal((short)4096, group.ColorDepth);
            Assert.Equal((byte)253, group.Flags);
            Assert.Equal(0xAA000000u, group.ColorFlags);
        });
    }

    [Fact]
    public void Loaded_surface_reads_current_bank_bytes_and_has_its_own_Z_UV_and_signed_completion_contract()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = NativeBattleSiren95Actors.Spawn73BCF0(WithStage(fixture.Frame, 1), NativeBattleSiren95Controller.MainBank, new(fixture.Inputs.Packets, (_, _, _, _) => throw new InvalidOperationException()));
        const uint address = 0x2587670;
        const uint sourceAddress = 0x19F1018;
        NativeBattleGfAnimationSource source = Source() with { Address = sourceAddress };
        NativeBattleChoco97IoBanks banks = NativeBattleChoco97State.FromColdProcess().PendingIoBanks!;
        banks = banks.Write(sourceAddress, source.Data);
        frame = frame with { Memory = frame.Memory with { Choco97 = NativeBattleChoco97State.FromColdProcess() with { PendingIoBanks = banks } } };
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address) with { Callback = 0x73EE50 };
        task = task.WriteDword(0x74, sourceAddress).WriteDword(0x78, 0x48).WriteWord(0x80, 1).WriteWord(0x8C, 1).WriteWord(0x90, 0x1234);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95.WriteDword(director + 8, 0xABCD007B).WriteDword(director + 12, 0xBEEF0011) } };
        source = NativeBattleSiren95Io.ReadProgrammeSource(frame.Memory, sourceAddress);
        int calls = 0;
        NativeBattleSiren95Frame Draw(NativeBattleSiren95Frame current, uint pointer)
        {
            calls++;
            NativeBattleModulePoolTask drawing = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(pointer);
            Assert.Equal((ushort)0xF011, drawing.ReadWord(0x20));
            Assert.Equal((ushort)0xBEEF, drawing.ReadWord(0x22));
            Assert.Equal((ushort)1, drawing.ReadWord(0x8C));
            return current;
        }
        var initialized = NativeBattleSiren95Actors.Advance73EE50(WithStage(frame, 2), address, source, Draw);
        Assert.Equal(1, calls);
        task = new NativeBattleEffectHierarchy(initialized.Frame.Effect.Pools).Read(address);
        Assert.Equal((ushort)125, task.ReadWord(0x8C));
        Assert.Equal((ushort)0x1234, task.ReadWord(0x90));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        NativeBattleSiren95Frame ending = WithStage(initialized.Frame, 3);
        string saved = JsonSerializer.Serialize(ending);
        var completed = NativeBattleSiren95Actors.Advance73EE50(ending, address, source, (_, _) => throw new InvalidOperationException());
        Assert.True(completed.Complete);
        Assert.Equal((ushort)121, new NativeBattleEffectHierarchy(completed.Frame.Effect.Pools).Read(address).ReadWord(0x8C));
        var restored = NativeBattleSiren95Actors.Advance73EE50(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, address, source, (_, _) => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(completed.Frame), JsonSerializer.Serialize(restored.Frame));
        banks = banks.Write(sourceAddress + 4, BitConverter.GetBytes(123).ToImmutableArray());
        frame = frame with { Memory = frame.Memory with { Choco97 = frame.Memory.Choco97! with { PendingIoBanks = banks } } };
        Assert.Equal(123, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleSiren95Io.ReadProgrammeSource(frame.Memory, sourceAddress).Data.AsSpan().Slice(4)));
    }

    private static NativeBattleSiren95Frame WithStage(NativeBattleSiren95Frame frame, short stage)
    {
        NativeBattleSiren95State shared = frame.Memory.Siren95!;
        return frame with { Memory = frame.Memory with { Siren95 = shared.Write(shared.DirectorAddress1533010!.Value + 0x40, BitConverter.GetBytes(stage)) } };
    }

    private static NativeBattleGfAnimationSource Source()
    {
        const int vertices = 85;
        const int geometry = 12;
        const int polygon = 12 + vertices * 8;
        const int animation = geometry + polygon + 32;
        byte[] bytes = new byte[animation + 20];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), animation);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), geometry);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry), polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 4), vertices);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 8), 1);
        for (int index = 0; index < vertices; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(geometry + 12 + 8 * index), (short)index);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(geometry + 18 + 8 * index), 0xBEEF);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(animation), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(animation + 4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(animation + 8), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(animation + 12), 0x6000);
        return new(0x16A5380, bytes.ToImmutableArray());
    }
}
