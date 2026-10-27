using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22GeometryTaskTests
{
    [Theory]
    [InlineData(0, 424, -272, 120, 0x800)]
    [InlineData(1, 24, -440, 8, 0x800)]
    [InlineData(2, -176, 520, -8, 0xA00)]
    [InlineData(3, 40, -8, 520, 0x100)]
    [InlineData(4, 176, 16, -440, 0xC00)]
    [InlineData(5, -520, -144, -64, 0xC00)]
    public void Initialization_uses_signed_truncation_all_six_coefficients_and_variant_four_yaw(int variant, int x, int y, int z, int yaw)
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask parent = fixture.Parent.WriteWord(0x86, unchecked((ushort)-4097));
        NativeBattleBio22GeometryTaskStep step = Run(fixture.Task.WriteWord(0x8C, (ushort)variant), parent, fixture.Memory, fixture.Resources);
        Assert.Equal(unchecked((uint)-2048), step.Task.ReadDword(0x30));
        Assert.Equal(unchecked((ushort)x), step.Task.ReadWord(0x54));
        Assert.Equal(unchecked((ushort)y), step.Task.ReadWord(0x56));
        Assert.Equal(unchecked((ushort)z), step.Task.ReadWord(0x58));
        Assert.Equal((ushort)yaw, step.Task.ReadWord(0x46));
        Assert.Equal((ushort)1, step.Task.ReadWord(0x24));
        Assert.Equal((byte)1, step.Task.ReadByte(0x29));
    }

    [Fact]
    public void Default_variant_retains_targets_and_yaw_but_still_initializes_the_common_tail()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = fixture.Task.WriteWord(0x8C, 0xFFFF).WriteWord(0x54, 17).WriteWord(0x56, 19).WriteWord(0x58, 23).WriteWord(0x46, 29);
        NativeBattleBio22GeometryTaskStep step = Run(task, fixture.Parent, fixture.Memory, fixture.Resources);
        Assert.Equal((ushort)17, step.Task.ReadWord(0x54));
        Assert.Equal((ushort)19, step.Task.ReadWord(0x56));
        Assert.Equal((ushort)23, step.Task.ReadWord(0x58));
        Assert.Equal((ushort)29, step.Task.ReadWord(0x46));
        Assert.Equal((ushort)34, step.Task.ReadWord(0x5C));
        Assert.Equal((ushort)46, step.Task.ReadWord(0x60));
        Assert.Equal(0x1615CE4u, step.Task.ReadDword(0x4C));
    }

    [Fact]
    public void Ratio_boundary_is_strict_and_switch_publishes_second_owner_before_draw_without_resetting_first()
    {
        var fixture = Fixture();
        NativeBattleBio22GeometryTaskStep initialized = Run(fixture.Task, fixture.Parent, fixture.Memory, fixture.Resources);
        NativeBattleBio22GeometryTaskStep exact = Run(initialized.Task.WriteWord(0x84, 0x800), fixture.Parent, initialized.Memory, fixture.Resources);
        Assert.Equal((ushort)0x1000, exact.Task.ReadWord(0x84));
        Assert.Equal((ushort)0, exact.Task.ReadWord(0x74));
        NativeBattleModulePoolTask switching = exact.Task.WriteWord(0x84, 0x801).WriteWord(0x74, 3).WriteWord(0x76, 4).WriteWord(0x78, 0xF00);
        NativeBattleBio22Geometry primary = exact.Memory.Bio22Geometry!;
        int draws = 0;
        NativeBattleBio22GeometryTaskStep switched = NativeBattleBio22GeometryTask.Advance89AAE0(switching, fixture.Parent.WriteWord(0x24, 7).WriteWord(0x82, 0x100), exact.Memory, fixture.Resources, (memory, task, mesh, deformed) =>
        {
            draws++;
            Assert.True(deformed);
            Assert.Same(mesh, memory.Bio22SecondaryGeometry);
            Assert.Same(primary, memory.Bio22Geometry);
            Assert.Equal((ushort)5, task.ReadWord(0x74));
            return memory with { Parameter80 = 93 };
        });
        Assert.Equal(1, draws);
        Assert.Equal((ushort)23, switched.Task.ReadWord(0x90));
        Assert.Equal(unchecked((ushort)-512), switched.Task.ReadWord(0x82));
        Assert.Equal((byte?)93, switched.Memory.Parameter80);
        Assert.Equal((ushort)1, switched.Task.ReadWord(0x84));
        Assert.Equal(0x1617E3Cu, switched.Task.ReadDword(0x4C));
    }

    [Fact]
    public void Complete_progression_uses_all_sources_and_every_JSON_restored_next_draw_matches()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = fixture.Task;
        NativeBattleEffectMemory memory = fixture.Memory;
        HashSet<uint> pointers = [];
        HashSet<byte> phases = [];
        int draws = 0;
        for (int tick = 0; tick < 100; tick++)
        {
            NativeBattleModulePoolTask restoredTask = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
            NativeBattleEffectMemory restoredMemory = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
            NativeBattleBio22GeometryTaskStep expected = Run(restoredTask, fixture.Parent, restoredMemory, fixture.Resources);
            NativeBattleBio22GeometryTaskStep step = NativeBattleBio22GeometryTask.Advance89AAE0(task, fixture.Parent, memory, fixture.Resources, (current, currentTask, mesh, deformed) =>
            {
                draws++;
                pointers.Add(currentTask.ReadDword(0x6C));
                pointers.Add(currentTask.ReadDword(0x70));
                phases.Add(currentTask.ReadByte(0x29));
                Assert.Same(mesh, deformed ? current.Bio22SecondaryGeometry : current.Bio22Geometry);
                return current;
            });
            Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(step));
            task = step.Task;
            memory = step.Memory;
            if (step.CompleteRequested)
            {
                Assert.Equal((ushort)draws, task.ReadWord(0x24));
                Assert.Equal(2u, step.Result);
                Assert.Equal(7, pointers.Count);
                Assert.Contains((byte)2, phases);
                Assert.Contains((byte)3, phases);
                Assert.Equal((byte)4, task.ReadByte(0x29));
                return;
            }
        }
        Assert.Fail("Synthetic geometry component did not reach its explicit release flag.");
    }

    [Fact]
    public void Terminal_and_held_children_still_position_morph_draw_and_increment_once()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = Run(fixture.Task, fixture.Parent, fixture.Memory, fixture.Resources).Task.WriteByte(0x29, 5).WriteByte(0x26, 1).WriteByte(0x28, 1).WriteWord(0x24, 0xFFFF);
        NativeBattleBio22GeometryTaskStep held = Run(task, fixture.Parent, fixture.Memory, fixture.Resources);
        Assert.False(held.CompleteRequested);
        Assert.Equal((ushort)0, held.Task.ReadWord(0x24));
        NativeBattleBio22GeometryTaskStep complete = Run(held.Task.WriteByte(0x28, 0), fixture.Parent, held.Memory, fixture.Resources);
        Assert.True(complete.CompleteRequested);
        Assert.Equal((ushort)1, complete.Task.ReadWord(0x24));
    }

    [Fact]
    public void Interpolation_uses_current_parent_signed_division_and_does_not_invent_zero_duration()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = Run(fixture.Task, fixture.Parent, fixture.Memory, fixture.Resources).Task.WriteByte(0x29, 2).WriteWord(0x90, 3).WriteWord(0x96, unchecked((ushort)-7)).WriteWord(0x82, unchecked((ushort)-5));
        NativeBattleBio22GeometryTaskStep step = Run(task, fixture.Parent.WriteWord(0x86, 101).WriteWord(0x82, 200), fixture.Memory, fixture.Resources);
        Assert.Equal(94u, step.Task.ReadDword(0x30));
        Assert.Equal((ushort)195, step.Task.ReadWord(0x78));
        Assert.Equal((ushort)1365, step.Task.ReadWord(0x92));
        Assert.Throws<DivideByZeroException>(() => Run(task.WriteWord(0x90, 0), fixture.Parent, fixture.Memory, fixture.Resources));
    }

    [Fact]
    public void Position_wraps_short_coordinates_and_copies_parent_fourth_word_without_saturation()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = fixture.Task.WriteWord(0x5C, 30000).WriteWord(0x5E, unchecked((ushort)-30000)).WriteWord(0x60, 10000).WriteWord(0x78, 512);
        NativeBattleModulePoolTask parent = fixture.Parent.WriteWord(0x78, 30000).WriteWord(0x7A, 30000).WriteWord(0x7C, 30000).WriteWord(0x7E, 0xBEEF);
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 512), new(30000, -30000, 10000));
        NativeBattleModulePoolTask positioned = NativeBattleBio22GeometryTask.Position89B170(task, parent);
        Assert.Equal(unchecked((ushort)(30000 + rotated.X)), positioned.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)(30000 + rotated.Y)), positioned.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)(30000 + rotated.Z)), positioned.ReadWord(0x20));
        Assert.Equal((ushort)0xBEEF, positioned.ReadWord(0x22));
    }

    [Fact]
    public void Missing_selected_warm_mesh_fails_without_hydrating_its_baseline()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => Run(fixture.Task, fixture.Parent, fixture.Memory with { Bio22Geometry = null }, fixture.Resources));
    }

    private static NativeBattleBio22GeometryTaskStep Run(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleEffectMemory memory, NativeBattleBio22Resources resources) => NativeBattleBio22GeometryTask.Advance89AAE0(task, parent, memory, resources, KeepMemory);
    private static NativeBattleEffectMemory KeepMemory(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleBio22Geometry mesh, bool deformed) => memory;

    internal static (NativeBattleModulePoolTask Task, NativeBattleModulePoolTask Parent, NativeBattleEffectMemory Memory, NativeBattleBio22Resources Resources) Fixture()
    {
        uint[] addresses = [0x1615A04, 0x1617164, 0x1617444, 0x1617724, 0x1617A04, 0x1617CE4, 0x1618874];
        byte[] pointers = new byte[28];
        ImmutableArray<NativeBattleEffectHeapAllocation>.Builder sources = ImmutableArray.CreateBuilder<NativeBattleEffectHeapAllocation>(7);
        for (int index = 0; index < 7; index++)
        {
            int count = index < 5 ? 91 : 42;
            byte[] bytes = new byte[8 + count * 8];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
            for (int vertex = 0; vertex < count; vertex++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), (short)(index * 100 + vertex));
            }
            sources.Add(new($"FF8_EN:{addresses[index]:X}", bytes.ToImmutableArray()));
            BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(index * 4), addresses[index]);
        }
        NativeBattleStaticMeshResource first = Mesh(0x1615CE4, 91, 0x1480);
        NativeBattleStaticMeshResource second = Mesh(0x1617E3C, 42, 0xA38);
        byte[] animation = new byte[0x918];
        BinaryPrimitives.WriteUInt16LittleEndian(animation.AsSpan(6), (ushort)animation.Length);
        NativeBattleBio22Resources resources = new(new("FF8_EN:1626014", animation.ToImmutableArray()), first, second, sources.MoveToImmutable(), new("FF8_EN:16189CC", pointers.ToImmutableArray()), new("FF8_EN:16146E4", new byte[4].ToImmutableArray()), Mesh(0x1619888, 467, 0x5028), Mesh(0x161F750, 258, 0x4038), [Source(0x16189E8, 467), Source(0x161E8B0, 467)], Directory([0x1623FA0, 0x16247B8, 0x1624FD0, 0x16257E8, 0x1623788]), [Source(0x1623FA0, 258), Source(0x16247B8, 258), Source(0x1624FD0, 258), Source(0x16257E8, 258), Source(0x1623788, 258)], [Sprite(0x1614F74, 0x1AC, 16), Sprite(0x1615120, 0x1AC, 16), Sprite(0x16152CC, 0x174, 13), Sprite(0x1615440, 0x174, 13), Sprite(0x16156F8, 0x160, 13), Sprite(0x1615858, 0x1AC, 16), Sprite(0x16155B4, 0x144, 12)])
        {
            OverlayProgram = new("FF8_EN:162693C", new byte[0x10].ToImmutableArray()),
            PlanarCounts = new("FF8_EN:162694C", new byte[0x15].ToImmutableArray()),
            BillboardCounts = new("FF8_EN:162696C", new byte[0x15].ToImmutableArray()),
            LaterPlanarCounts = new("FF8_EN:162698C", new byte[0x19].ToImmutableArray()),
            LaterBillboardCounts = new("FF8_EN:16269AC", new byte[0x15].ToImmutableArray()),
            PaletteBaseline = new("FF8_EN:16146E8", NativeBattleBio22PaletteTests.Tim(0)),
            PaletteSources = new uint[] { 0x1614D48, 0x1614B28, 0x1614908 }.Select(address => new NativeBattleEffectHeapAllocation($"FF8_EN:{address:X}", NativeBattleBio22PaletteTests.Tim(0))).ToImmutableArray(),
            PalettePointers = Directory([0x1614D48, 0x1614B28, 0x1614908]) with { Identity = "FF8_EN:1614F68" }
        };
        resources.Validate();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = NativeBattleBio22Geometry.FromColdProcess(first), Bio22SecondaryGeometry = NativeBattleBio22Geometry.FromColdProcess(second) };
        NativeBattleModulePoolTask task = new(0, 0, null, 0x89AAE0, new byte[0x98 - 12].ToImmutableArray());
        NativeBattleModulePoolTask parent = new(0, 0, null, 1, new byte[0x98 - 12].ToImmutableArray());
        return (task, parent.WriteWord(0x86, 4096), memory, resources);
    }

    private static NativeBattleModuleResource Sprite(uint address, int length, int frames)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
        for (int frame = 0; frame <= frames; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)(12 + frames * 2));
        }
        return new(0, address, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleEffectHeapAllocation Source(uint address, int count)
    {
        byte[] bytes = new byte[8 + count * 8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        return new($"FF8_EN:{address:X}", bytes.ToImmutableArray());
    }

    private static NativeBattleEffectHeapAllocation Directory(uint[] pointers)
    {
        byte[] bytes = new byte[pointers.Length * 4];
        for (int index = 0; index < pointers.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), pointers[index]);
        }
        return new("FF8_EN:1626000", bytes.ToImmutableArray());
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, int count, int length)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8 + count * 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + count * 8 + 24), count switch { 91 => 160, 42 => 80, 467 => 598, _ => 512 });
        return new(0, address, bytes.ToImmutableArray());
    }
}
