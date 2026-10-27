using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrainFamilyTests
{
    [Theory]
    [InlineData(39)]
    [InlineData(56)]
    public void Current_node_mesh_bridge_publishes_canonical_registers_context_and_unknown_tail_through_flush(ushort effect)
    {
        var fixture = NativeBattleDrain39NodeTests.Fixture(effect);
        NativeBattleDrainFamilyProfile profile = NativeBattleDrainFamilyProfile.ForEffect(effect);
        NativeBattleEffectMemory process = fixture.Frame.Memory with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown(),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(),
            PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() with { ModelRendererRegisters = new(1, 2, 3, 4, 5) },
            NodeScratch2793E30 = NativeBattleEffectNodeScratch.FromColdProcess(),
            NodeContextExtension = NativeBattleEffectNodeContextExtension.FromColdProcess()
        };
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(process).MarkUnknown(NativeBattleCuraga88Mesh.SharedViewOffset + 0x40, 24);
        process = NativeBattleEffectNodeContextMemory.Publish(process, view);
        const int first = 0x10000;
        const int node = first + 0x10E0;
        using NativeBattleEffectNodeMemory memory = new(process, fixture.Frame.Effect, fixture.Address, first, 0x10E0, node, 0x16F80, true);
        uint meshAddress = profile.BankAddress + 0x1000;
        memory.Dword(node + 0x170, meshAddress);
        memory.Word(node + 0x1CE, 2048);
        memory.Dword(node + 0x16C, 0xC08040);
        memory.Word(node + 0xAC + 18, 0x4321);
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
        NativeBattleEffectNodeMeshInstance emit = NativeBattleEffect88MeshPreparation.Prepare(memory, node, new byte[344], projection, constants);
        using NativeBattleGfSourceReader resource = new(Triangle());
        NativeBattleGfGeometryBatch batch = emit(resource, NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 });
        Assert.NotNull(Assert.Single(batch.Attempts).Primitive);
        NativeBattleEffectMemory after = memory.CaptureProcessMemory();
        Assert.Equal(499u, after.GeometryRegisters!.Otz);
        Assert.Equal(499u, after.GeometryRegisters.Mac0);
        Assert.Equal((byte?)0x21, after.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0x43, after.GeometryRegisters.Rotation20[19]);
        Assert.NotNull(after.ScriptedColorRegisters!.Material);
        Assert.All(after.ScriptedColorRegisters.Rgb, color => Assert.NotNull(color));
        Assert.Equal(0x400u, after.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x800u, after.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0xC00u, after.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        view = NativeBattleEffectNodeContextMemory.Capture(after);
        Assert.Equal(meshAddress, BinaryPrimitives.ReadUInt32LittleEndian(view.Read(NativeBattleCuraga88Mesh.SharedViewOffset, 4)));
        Assert.Equal(499u, BinaryPrimitives.ReadUInt32LittleEndian(view.Read(NativeBattleCuraga88Mesh.SharedViewOffset + 0x2C, 4)));
        Assert.Throws<NotSupportedException>(() => view.Read(NativeBattleCuraga88Mesh.SharedViewOffset + 0x40, 24));
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        Assert.Equal(JsonSerializer.Serialize(after.GeometryRegisters), JsonSerializer.Serialize(written.Memory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(after.ScriptedColorRegisters), JsonSerializer.Serialize(written.Memory.ScriptedColorRegisters));
        Assert.Equal(JsonSerializer.Serialize(after.PrimitiveProcess.ModelRendererRegisters), JsonSerializer.Serialize(written.Memory.PrimitiveProcess!.ModelRendererRegisters));
    }

    private static ImmutableArray<byte> Triangle()
    {
        byte[] mesh = new byte[92];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 0x1111, 400, -400, 0, 0x2222, -400, 400, 0, 0x3333, 400, 400, 0, 0x4444];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), positions[index]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(60), 0x30332211);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(64), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(66), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(68), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(72), 0xA0554422);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(76), 0xB0886633);
        return mesh.ToImmutableArray();
    }

    [Theory]
    [InlineData(39)]
    [InlineData(56)]
    public void Current_controller_uses_each_original_pool_callback_directory_and_flash_schedule(ushort effect)
    {
        var fixture = NativeBattleDrain39ControllerTests.Fixture(effect: effect);
        NativeBattleDrainFamilyProfile profile = NativeBattleDrainFamilyProfile.ForEffect(effect);
        NativeBattleDrain39Resources resources = NativeBattleDrain39ControllerTests.Resources(effect);
        Assert.Equal(profile.Pools.Select(pool => pool.Descriptor), fixture.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(profile.Pools.Select(pool => pool.Bank), fixture.Frame.Effect.Pools.Select(pool => pool.BankAddress));
        NativeBattleDrain39Frame frame = fixture.Frame;
        List<int> contacts = [];
        for (int index = 1; index <= 35; index++)
        {
            NativeBattleDrain39ControllerStep step = NativeBattleDrain39Controller.Advance851510(frame, fixture.Inputs, resources, (current, _) => new(current, 0));
            contacts.AddRange(step.Contacts);
            frame = step.Frame;
        }
        Assert.Equal(new[] { 0 }, contacts);
        NativeBattleModulePoolTask generic = Assert.Single(frame.Effect.Pools[3].Slots, task => task.Callback == profile.GenericCallback);
        Assert.Equal(profile.DirectoryAddress, generic.ReadDword(0x30));
        Assert.Equal((ushort)0, generic.ReadWord(0x298));
        Assert.Equal((ushort)0, generic.ReadWord(0x29C));
        Assert.Equal((ushort)45, generic.ReadWord(0x29E));
        Assert.Equal(new uint[] { profile.FirstFlashAddress, profile.SecondFlashAddress }, frame.Effect.Pools[2].Slots.Where(task => task.Callback == profile.FlashCallback).Select(task => task.ReadDword(0x30)));
        Assert.Equal(256 + 0xC000, frame.State.Emitters);
        Assert.Equal(256 + 0xC000 + 0x10E0, frame.State.Geometry);
    }

    [Fact]
    public void Absorb_profile_cannot_consume_Drain_binding_or_the_Drain_initializer_pointer()
    {
        var fixture = NativeBattleDrain39ControllerTests.Fixture(effect: 56);
        NativeBattleDrain39Resources resources = NativeBattleDrain39ControllerTests.Resources(56);
        Assert.Throws<InvalidDataException>(() => NativeBattleDrain39Controller.Initialize8513D0(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Inputs.Packets, resources with { Effect = 39 }));
        NativeBattleOriginalEffectState wrongPointer = fixture.Frame.Effect with { ResourcePointers = [new(0x269DA1C, 256)] };
        Assert.Throws<NotSupportedException>(() => NativeBattleDrain39Controller.Initialize8513D0(fixture.Frame.Memory, wrongPointer, fixture.Inputs.Packets, resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleDrainFamilyProfile.ForEffect(55));
    }

    [Fact]
    public void Current_view_requires_the_packed_producer_and_preserves_its_nonzero_padding()
    {
        var fixture = NativeBattleDrain39ControllerTests.Fixture(effect: 56);
        NativeBattleDrain39Resources resources = NativeBattleDrain39ControllerTests.Resources(56);
        Assert.Throws<NotSupportedException>(() => NativeBattleDrain39Controller.Advance851510(fixture.Frame, fixture.Inputs with { PackedCamera = null }, resources, (current, _) => new(current, 0)));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(fixture.Inputs.Camera, 0x1234);
        NativeBattleDrain39ControllerStep step = NativeBattleDrain39Controller.Advance851510(fixture.Frame, fixture.Inputs with { PackedCamera = packed }, resources, (current, _) => new(current, 0));
        Assert.Equal(JsonSerializer.Serialize(packed), JsonSerializer.Serialize(step.Frame.Memory.SharedEffectView2793E58));
    }

    [Theory]
    [InlineData(39)]
    [InlineData(56)]
    public void Original_absolute_flag1_bank_initializes_without_relocation_and_preserves_other_module_owner(ushort effect)
    {
        var fixture = NativeBattleDrain39NodeTests.Fixture(effect);
        NativeBattleDrainFamilyProfile profile = NativeBattleDrainFamilyProfile.ForEffect(effect);
        NativeBattleDrain39Resources resources = NativeBattleDrain39NodeTests.Resources(effect);
        NativeBattleDrain39Frame frame = fixture.Frame with { Memory = NativeBattleDrainFamilyProfile.ForEffect(effect == 39 ? (ushort)56 : (ushort)39).WithBank(fixture.Frame.Memory, new(resources.NodeBank)) };
        NativeBattleDrain39BankState other = NativeBattleDrainFamilyProfile.ForEffect(effect == 39 ? (ushort)56 : (ushort)39).RequireBank(frame.Memory);
        NativeBattleDrain39NodeStep step = NativeBattleDrain39Nodes.Advance(frame, fixture.Address, resources, fixture.Inputs, new(123), new(fixture.Inputs.Camera, 320, 160, 112), 0, NativeBattleLogicalOrdering.Clear(), new(0, 0xFFFFFF, 4096, 0));
        Assert.Equal(resources.NodeBank.ToArray(), NativeBattleStorageSnapshots.Capture(profile.RequireBank(step.Frame.Memory).Data!).Data.ToArray());
        Assert.Null(profile.RequireBank(fixture.Frame.Memory).Data);
        Assert.Same(other, NativeBattleDrainFamilyProfile.ForEffect(effect == 39 ? (ushort)56 : (ushort)39).RequireBank(step.Frame.Memory));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(profile.DirectoryAddress + 4, task.ReadDword(0x34 + 0x224));
        Assert.Equal(profile.DirectoryAddress + 0x44, task.ReadDword(0x34 + 0x228));
    }

    [Fact]
    public void Absorb_external_channel_owner_ends_before_the_next_module_and_rejects_cross_boundary_reads()
    {
        NativeBattleDrain39Resources resources = NativeBattleDrain39NodeTests.Resources(56) with { Channels = Enumerable.Repeat((byte)0x5A, 0x1E60).ToImmutableArray() };
        Assert.Equal(new byte[] { 0x5A, 0x5A }, resources.ReadChannels(0x2610FA2, 2));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(0x2610FA2, 3));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(0x2610FA8, 1));
        Assert.Throws<InvalidDataException>(() => resources.ReadChannels(0x269E764, 1));
        Assert.Throws<NotSupportedException>(() => (resources with { Channels = null }).ReadChannels(0x260F144, 2));
    }

    [Theory]
    [InlineData(39)]
    [InlineData(56)]
    public void Shared_flash_interpreter_uses_actual_profile_script_and_releases_without_terminal_packet(ushort effect)
    {
        var fixture = NativeBattleDrain39ControllerTests.Fixture(effect: effect);
        NativeBattleDrainFamilyProfile profile = NativeBattleDrainFamilyProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend flash = hierarchy.Append8DC540(2, profile.FlashCallback, 60, hierarchy.Address(0, 0), (_, _, _) => new(2, 3));
        hierarchy = flash.Hierarchy.Write(flash.Address, flash.Hierarchy.Read(flash.Address).WriteDword(0x30, profile.FirstFlashAddress));
        NativeBattleDrain39Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleDrain39Resources resources = NativeBattleDrain39ControllerTests.Resources(effect) with { FirstFlash = [0x007F7F7F, 0xFF000000] };
        List<NativeBattleDrain39Operation> operations = [];
        for (int index = 0; index < 2; index++)
        {
            NativeBattleDrain39LeafStep step = NativeBattleDrain39Simple.Advance(frame, flash.Address, resources, (current, operation) =>
            {
                operations.Add(operation);
                return current;
            });
            Assert.Equal(index == 0 ? 0 : 2, step.Result);
            frame = step.Frame;
        }
        Assert.Equal(36, Assert.IsType<NativeBattleDrain39PacketOperation>(Assert.Single(operations)).Batch.AllocationBytes);
    }
}
