using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectHierarchyTests
{
    [Fact]
    public void Root_clears_only_the_requested_DWORD_prefix_and_retains_the_original_slot_suffix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 2);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteBytes(12, Enumerable.Repeat((byte)0xA5, 88).ToImmutableArray().AsSpan()));
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy([pool]).Append8DC540(0, 0x8D6B30, 45);
        Assert.Equal(0x1000u, appended.Address);
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(appended.Address);
        Assert.Equal(0u, task.ReadDword(0x0C));
        Assert.Equal(appended.Address, task.ReadDword(0x10));
        Assert.Equal(0u, task.ReadDword(0x14));
        Assert.Equal(0u, task.ReadDword(0x18));
        Assert.Equal((byte)0, task.ReadByte(43));
        Assert.Equal((byte)0xA5, task.ReadByte(44));
        Assert.Equal((byte)0xA5, task.ReadByte(99));
        Assert.Equal((ushort)1, task.Flags);
        Assert.Null(task.Next);
    }

    [Fact]
    public void Descendants_inherit_root_first_level_parameter_position_and_signed_header_contact_selectors()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8D6B30, 100);
        hierarchy = root.Hierarchy;
        NativeBattleModulePoolTask parent = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteDword(0x1C, 0x1234FEDC).WriteDword(0x20, 0x6789ABCD).WriteByte(0x2A, 0xFF).WriteByte(0x2B, 0xFE).WriteByte(0x2E, 7).WriteByte(0x2F, 8).WriteByte(0x28, 255);
        hierarchy = hierarchy.Write(root.Address, parent);
        NativeBattleEffectTaskActors Select(uint parameters, sbyte group, sbyte target)
        {
            Assert.Equal(0x1D99A78u, parameters);
            Assert.Equal((sbyte)-1, group);
            Assert.Equal((sbyte)-2, target);
            return new(4, 6);
        }
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(0, 0x8D6CC0, 88, root.Address, Select);
        Assert.Equal((byte)0, child.Hierarchy.Read(root.Address).ReadByte(0x28));
        Assert.Equal(1, child.Hierarchy.Read(root.Address).Next);
        NativeBattleModulePoolTask first = child.Hierarchy.Read(child.Address);
        Assert.Equal(root.Address, first.ReadDword(0x10));
        Assert.Equal(child.Address, first.ReadDword(0x14));
        Assert.Equal(root.Address, first.ReadDword(0x18));
        Assert.Equal(0x1234FEDCu, first.ReadDword(0x1C));
        Assert.Equal(0x6789ABCDu, first.ReadDword(0x20));
        Assert.Equal((byte)4, first.ReadByte(0x2C));
        Assert.Equal((byte)6, first.ReadByte(0x2D));
        Assert.Equal((byte)7, first.ReadByte(0x2E));
        Assert.Equal((byte)8, first.ReadByte(0x2F));
        NativeBattleEffectHierarchyAppend grandchild = child.Hierarchy.Append8DC540(0, 0x8D6D80, 100, child.Address, Select);
        Assert.Equal(child.Address, grandchild.Hierarchy.Read(grandchild.Address).ReadDword(0x14));
        Assert.Equal(root.Address, grandchild.Hierarchy.Read(grandchild.Address).ReadDword(0x10));
        hierarchy = grandchild.Hierarchy.Complete8DC530(grandchild.Address).Complete8DC530(child.Address);
        Assert.Equal((byte)0, hierarchy.Read(child.Address).ReadByte(0x28));
        Assert.Equal((byte)255, hierarchy.Read(root.Address).ReadByte(0x28));
        Assert.Same(hierarchy, hierarchy.Complete8DC530(root.Address));
    }

    [Fact]
    public void The_original_pool_pass_visits_appended_children_and_preserves_cross_pool_parent_updates()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 2), NativeBattleEffectTaskPool.Initialize(0x4000, 0x3000, 88, 2)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8D6B30, 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78));
        List<uint> visited = [];
        NativeBattleEffectHierarchyStep step = hierarchy.Advance(0, (state, address) =>
        {
            visited.Add(address);
            if (address == root.Address)
            {
                state = state.Append8DC540(0, 0x8D6CC0, 100, address, (_, _, _) => new(0, 3)).Hierarchy;
                state = state.Append8DC540(1, 0x8D6D80, 88, address, (_, _, _) => new(0, 3)).Hierarchy;
                return new(state, 0);
            }
            return new(state.Complete8DC530(address), 2);
        });
        Assert.Equal(new uint[] { 0x1000, 0x1064 }, visited);
        Assert.Equal(1, step.Survivors);
        Assert.Equal((byte)1, step.Hierarchy.Read(root.Address).ReadByte(0x28));
        Assert.Null(step.Hierarchy.Read(root.Address).Next);
        Assert.Equal(0x8D6D80u, step.Hierarchy.Pools[1].Slots[0].Callback);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Hierarchy));
        NativeBattleEffectHierarchy restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectHierarchy>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        NativeBattleEffectHierarchyStep childPass = restored.Advance(1, (state, address) => new(state.Complete8DC530(address), 2));
        Assert.Equal((byte)0, childPass.Hierarchy.Read(root.Address).ReadByte(0x28));
        Assert.Equal(0, childPass.Survivors);
    }

    [Fact]
    public void Exhaustion_returns_null_without_mutating_a_parent_and_invalid_physical_addresses_are_rejected()
    {
        NativeBattleEffectHierarchyAppend root = new NativeBattleEffectHierarchy([NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1)]).Append8DC540(0, 0x8D6B30, 100);
        NativeBattleEffectHierarchyAppend full = root.Hierarchy.Append8DC540(0, 0x8D6CC0, 88, root.Address);
        Assert.Equal(0u, full.Address);
        Assert.Same(root.Hierarchy, full.Hierarchy);
        Assert.Throws<InvalidDataException>(() => full.Hierarchy.Read(0x1001));
        Assert.Throws<InvalidDataException>(() => full.Hierarchy.Read(0x1064));
        Assert.Throws<ArgumentOutOfRangeException>(() => full.Hierarchy.Append8DC540(0, 0x8D6CC0, 101));
    }
}
