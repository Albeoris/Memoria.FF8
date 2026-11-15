using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectHierarchyTests
{
    private static readonly BattleModelOwner Actor = new BattleEnemyActorModel(new("caster"));
    private static readonly BattleModelOwner Target = new BattleEnemyActorModel(new("recipient"));
    private static readonly NativeBattleModelAliases Aliases = new(new([Actor, Target]), [(NativeBattleModelSlot.Decode(4), Actor), (NativeBattleModelSlot.Decode(6), Target)]);
    [Fact]
    public void Root_clears_only_the_requested_DWORD_prefix_and_retains_the_original_slot_suffix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 2);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteBytes(12, Enumerable.Repeat((byte)0xA5, 88).ToImmutableArray().AsSpan()).WriteTraversalPosition(0x2E, 165));
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy([pool]).Append8DC540(0, 0x8D6B30, 45);
        Assert.Equal(0x1000u, appended.Address);
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(appended.Address);
        Assert.Equal(0u, task.ReadDword(0x0C));
        Assert.Equal(appended.Address, task.ReadDword(0x10));
        Assert.Equal(0u, task.ReadDword(0x14));
        Assert.Equal(0u, task.ReadDword(0x18));
        Assert.Equal((byte)0, task.ReadByte(43));
        Assert.Equal(0, task.ReadTraversalPosition(0x2B));
        Assert.Equal((byte)0xA5, task.ReadByte(44));
        Assert.Equal(165, task.ReadTraversalPosition(0x2E));
        Assert.Equal((byte)0xA5, task.ReadByte(0x2F));
        Assert.Throws<InvalidDataException>(() => task.ReadTraversalPosition(0x2F));
        Assert.Equal((byte)0xA5, task.ReadByte(99));
        Assert.Equal((ushort)1, task.Flags);
        Assert.Null(task.Next);
    }

    [Fact]
    public void Descendants_inherit_root_links_signed_header_and_full_traversal_positions()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8D6B30, 100);
        hierarchy = root.Hierarchy;
        NativeBattleModulePoolTask parent = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteDword(0x1C, 0x1234FEDC).WriteDword(0x20, 0x6789ABCD).WriteByte(0x2A, 0xFF).WriteTraversalPosition(0x2B, 300).WriteTraversalPosition(0x2E, 301).WriteTraversalPosition(0x2F, 800).WriteByte(0x28, 255);
        hierarchy = hierarchy.Write(root.Address, parent);
        NativeBattleEffectTaskActors Select(uint parameters, sbyte group, int target)
        {
            Assert.Equal(0x1D99A78u, parameters);
            Assert.Equal((sbyte)-1, group);
            Assert.Equal(300, target);
            return new(Actor, Target, Aliases);
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
        Assert.Equal(300, first.ReadTraversalPosition(0x2B));
        Assert.Equal(301, first.ReadTraversalPosition(0x2E));
        Assert.Equal(800, first.ReadTraversalPosition(0x2F));
        Assert.Throws<NotSupportedException>(() => first.ReadByte(0x2E));
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
                state = state.Append8DC540(0, 0x8D6CC0, 100, address, (_, _, _) => new(Actor, Target, Aliases)).Hierarchy;
                state = state.Append8DC540(1, 0x8D6D80, 88, address, (_, _, _) => new(Actor, Target, Aliases)).Hierarchy;
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
    public void Self_parent_allocation_selects_using_the_planned_clear_prefix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1);
        pool.Slots[0].WriteByte(0x2A, 4).WriteTraversalPosition(0x2B, 300).WriteTraversalPosition(0x2E, 301).WriteTraversalPosition(0x2F, 800);
        NativeBattleEffectHierarchy hierarchy = new([pool]);

        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(0, 0x8D6CC0, 45, pool.BankAddress, (parameters, group, target) =>
        {
            Assert.Equal(0u, parameters);
            Assert.Equal(0, (int)group);
            Assert.Equal(0, target);
            return new(Actor, Target, Aliases);
        });

        Assert.Equal(pool.BankAddress, child.Address);
        NativeBattleModulePoolTask task = child.Hierarchy.Read(child.Address);
        Assert.Equal(0, task.ReadTraversalPosition(0x2B));
        Assert.Equal(301, task.ReadTraversalPosition(0x2E));
        Assert.Equal(800, task.ReadTraversalPosition(0x2F));
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
