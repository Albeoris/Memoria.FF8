using System.Runtime.CompilerServices;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleRenzokukenVisualLifetimes
{
    private static readonly ConditionalWeakTable<object, long[]> rows = new();

    internal static bool Owns(ushort effect) => effect is 141 or 159 or 160 or 161 or 259 or 332;

    internal static NativeBattlePilotVisualIdentity? Task(NativeBattleOriginalEffectState effect, NativeBattleModulePoolTask task, uint source, int primitive = 0)
    {
        return Owns(effect.Binding.Effect) ? NativeBattlePilotVisualLifetimes.Identity(effect.Binding.Effect, task, primitive) with { ResourceGeneration = source } : null;
    }

    internal static int Bank(ushort effect, int bank) => effect == 161 && bank == 2 ? 4 : bank;

    internal static void Allocated(NativeBattleOriginalEffectState effect, int bank, int row)
    {
        if (!Owns(effect.Binding.Effect) || effect.Pools.IsDefaultOrEmpty)
        {
            return;
        }
        rows.GetValue(effect.Pools[0].Slots[0].RequireOwner(), _ => new long[500])[checked(bank * 100 + row)]++;
    }

    internal static NativeBattlePilotVisualIdentity? Particle(NativeBattleOriginalEffectState effect, int bank, int row, uint source)
    {
        if (!Owns(effect.Binding.Effect) || effect.Pools.IsDefaultOrEmpty)
        {
            return null;
        }
        NativeBattleModulePoolTask root = effect.Pools[0].Slots[0];
        long birth = rows.GetValue(root.RequireOwner(), _ => new long[500])[checked(bank * 100 + row)];
        return NativeBattlePilotVisualLifetimes.Identity(effect.Binding.Effect, root, checked(bank * 100 + row)) with { ResourceGeneration = unchecked(((long)source << 32) ^ birth) };
    }

    internal static NativeBattleModuleState EnsureShared(NativeBattleModuleState modules, int slot, ushort effect)
    {
        NativeBattleModuleTask task = modules.Slots[slot];
        if (!Owns(effect) || task.PilotIdentity is not null)
        {
            return modules;
        }
        uint callback = task.Callback ?? throw new InvalidDataException("Renzokuken shared presentation requires its actual callback.");
        NativeBattlePilotVisualIdentity owner = new(effect, NativeBattleModuleState.DescriptorAddress, slot, NativeBattlePilotVisualLifetimes.EpochFor(task), 0) { ResourceGeneration = callback };
        return modules with { Slots = modules.Slots.SetItem(slot, task with { PilotIdentity = owner }) };
    }

    internal static ImmutableArray<NativeBattlePrimitiveReference> Record(ImmutableArray<NativeBattlePrimitiveReference> packets, string bank, byte buffer, int start, NativeBattleModulePacketBatch batch)
    {
        if (batch.CapturedWrites is { } captured)
        {
            return NativeBattleEffectPresentationPackets.Record(packets, bank, buffer, start, captured);
        }
        foreach (NativeBattleAllocatedPrimitive primitive in batch.Primitives)
        {
            packets = packets.Add(new(bank, buffer, checked(start + primitive.ByteOffset)));
        }
        return packets;
    }

    internal static NativeBattleModuleState Shared(NativeBattleModuleState modules, int slot, NativeBattlePilotVisualIdentity? source)
    {
        if (source is not { } parent || !Owns(parent.Effect))
        {
            return modules;
        }
        NativeBattleModuleTask child = modules.Slots[slot];
        NativeBattlePilotVisualIdentity owner = new(parent.Effect, NativeBattleModuleState.DescriptorAddress, slot, NativeBattlePilotVisualLifetimes.EpochFor(child), parent.Primitive) { ResourceGeneration = parent.Generation };
        return modules with { Slots = modules.Slots.SetItem(slot, child with { PilotIdentity = owner }) };
    }
}
