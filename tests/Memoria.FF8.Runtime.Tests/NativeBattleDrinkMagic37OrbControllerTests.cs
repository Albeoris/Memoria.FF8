using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrinkMagic37OrbControllerTests
{
    [Fact]
    public void Orb_waits_to_age15_then_fades_holds20_and_finishes_with_parent_child_release()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = fixture.Hierarchy;
        int draws = 0;
        for (int age = 0; age <= 67; age++)
        {
            NativeBattleDrinkMagic37OrbStep step = NativeBattleDrinkMagic37OrbController.Advance(hierarchy, fixture.Address, fixture.Inputs);
            hierarchy = step.Hierarchy;
            NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address);
            draws += step.Operations.OfType<NativeBattleDrinkMagic37OrbDraw>().Count();
            if (age < 15)
            {
                Assert.Empty(step.Operations);
            }
            if (age == 15)
            {
                Assert.Equal((ushort)4096, task.ReadWord(0x50));
                Assert.Equal(5000u, task.ReadDword(0x38));
                Assert.Equal((short)-56, unchecked((short)task.ReadWord(0x1E)));
                Assert.Equal((ushort)0xBEEF, task.ReadWord(0x22));
                Assert.Equal((ushort)8, task.ReadWord(0x5E));
                Assert.Equal((short)0x400, Assert.Single(step.Operations.OfType<NativeBattleDrinkMagic37Anchor>()).Distance);
            }
            if (age == 31)
            {
                Assert.Equal((ushort)0, task.ReadWord(0x50));
                Assert.Equal((ushort)20, task.ReadWord(0x5C));
                NativeBattleEffectHierarchy restored = JsonSerializer.Deserialize<NativeBattleEffectHierarchy>(JsonSerializer.Serialize(hierarchy))!;
                Assert.Equal(JsonSerializer.Serialize(NativeBattleDrinkMagic37OrbController.Advance(hierarchy, fixture.Address, fixture.Inputs)), JsonSerializer.Serialize(NativeBattleDrinkMagic37OrbController.Advance(restored, fixture.Address, fixture.Inputs)));
            }
            if (age == 51)
            {
                Assert.Equal((byte)4, task.ReadByte(0x29));
            }
            Assert.Equal(age == 67 ? 2 : 0, step.Result);
        }
        Assert.Equal(53, draws);
        Assert.Equal((byte)0, hierarchy.Pools[0].Slots[0].ReadByte(0x28));
        Assert.Equal((ushort)40, hierarchy.Read(fixture.Address).ReadWord(0x5E));
    }

    [Fact]
    public void Orb_context_writes_its_actual_U_window_and_signed_operands_without_clearing_retained_fields()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = fixture.Hierarchy.Read(fixture.Address).WriteDword(0x4C, 0x15D62EC).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x40, 0x12345678).WriteWord(0x50, unchecked((ushort)-123)).WriteWord(0x52, unchecked((ushort)-4)).WriteWord(0x5E, 120);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 104).ToArray()).MarkUnknown(0x44, 2);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
        NativeBattleDrinkMagic37OrbPreparation prepared = NativeBattleDrinkMagic37OrbRendering.Prepare(task, camera, retained);
        Assert.Equal(0x15D62ECu, BinaryPrimitives.ReadUInt32LittleEndian(prepared.Context104.Read(0, 4)));
        Assert.Equal(0xA5A5A5A5u, BinaryPrimitives.ReadUInt32LittleEndian(prepared.Context104.Read(4, 4)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(prepared.Context104.Read(8, 4)));
        Assert.Equal(-123, BinaryPrimitives.ReadInt32LittleEndian(prepared.Context104.Read(12, 4)));
        Assert.Equal(-4, BinaryPrimitives.ReadInt32LittleEndian(prepared.Context104.Read(16, 4)));
        Assert.Equal(0xF0u, BinaryPrimitives.ReadUInt32LittleEndian(prepared.Context104.Read(20, 4)));
        Assert.Equal((ushort)120, BinaryPrimitives.ReadUInt16LittleEndian(prepared.Context104.Read(0x18, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(prepared.Context104.Read(0x1A, 2)));
        Assert.Equal((ushort)128, BinaryPrimitives.ReadUInt16LittleEndian(prepared.Context104.Read(0x28, 2)));
        Assert.Equal((ushort)256, BinaryPrimitives.ReadUInt16LittleEndian(prepared.Context104.Read(0x2A, 2)));
        Assert.All(prepared.Context104.ReadSnapshot(0x44, 2).Known, known => Assert.False(known));
        Assert.Equal(new[] { 'Y', 'X', 'Z' }, prepared.Operations.OfType<NativeBattleDrawStock15Rotate>().Select(operation => operation.Axis));
    }

    private static (NativeBattleEffectHierarchy Hierarchy, uint Address, NativeBattleDrinkMagic37OrbInputs Inputs) Fixture()
    {
        var original = NativeBattleDrinkMagic37ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(original.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        NativeBattleEffectHierarchyAppend orb = hierarchy.Append8DC540(4, 0x85C1F0, 96, root, (_, _, _) => new(0, 3));
        hierarchy = orb.Hierarchy.Write(orb.Address, orb.Hierarchy.Read(orb.Address).WriteWord(0x22, 0xBEEF));
        NativeBattleDrinkMagic37OrbInputs inputs = new(original.Inputs.Packets, original.Frame.Effect.Parameters!, (_, _) => new(100, 200, 300), _ => 0, actor => actor == 0 ? new(0, 0, 0) : new(3000, 0, 4000));
        return (hierarchy, orb.Address, inputs);
    }
}
