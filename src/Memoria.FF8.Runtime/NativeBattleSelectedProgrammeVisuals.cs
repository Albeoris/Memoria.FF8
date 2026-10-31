using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleProgrammeProjectedPoint(System.Numerics.Vector3 World, uint Screen);

internal static class NativeBattleSelectedProgrammeVisuals
{
    private sealed record Birth(long TaskGeneration, long Generation);
    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<uint, Birth>> workspaces = new();

    internal static void Allocated(ImmutableArray<NativeBattleEffectTaskPool> pools, uint address)
    {
        NativeBattleModulePoolTask task = Containing(pools, address);
        long parent = NativeBattlePilotVisualLifetimes.Generation(task, task.PilotSlot);
        workspaces.GetOrCreateValue(task.RequireOwner())[address] = new(parent, NativeBattlePilotVisualLifetimes.EpochFor(new object()));
    }

    internal static NativeBattlePilotVisualIdentity Workspace(ushort effect, NativeBattleModulePoolTask task, uint address, uint source, int node)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(effect, task, node);
        Dictionary<uint, Birth> rows = workspaces.GetOrCreateValue(task.RequireOwner());
        if (!rows.TryGetValue(address, out Birth? birth) || birth.TaskGeneration != owner.Generation)
        {
            birth = new(owner.Generation, NativeBattlePilotVisualLifetimes.EpochFor(new object()));
            rows[address] = birth;
        }
        return owner with { ResourceGeneration = unchecked((birth.Generation * 397 ^ address) * 397 ^ source) };
    }

    internal static NativeBattlePilotVisualIdentity Resource(NativeBattlePilotVisualIdentity identity, uint source, uint vertices = 0)
    {
        return identity with { ResourceGeneration = unchecked((identity.ResourceGeneration * 397 ^ source) * 397 ^ vertices) };
    }

    internal static NativeBattlePilotVisualIdentity RowPair(NativeBattleEffectArena arena, NativeBattlePilotVisualIdentity task, uint first, uint second, int primitive)
    {
        const uint bank = 0x20DFAB8 + NativeBattleMegaSpark279Profile.RowsOffset;
        int Slot(uint row)
        {
            uint offset = unchecked(row - bank);
            if (offset % 40 != 0 || offset / 40 >= NativeBattleMegaSpark279Profile.RowCount)
            {
                throw new InvalidDataException("Selected linked strip capture leaves its actual native rows.");
            }
            return checked((int)(offset / 40));
        }
        NativeBattlePilotVisualIdentity a = NativeBattlePilotVisualLifetimes.Identity(279, arena, bank, Slot(first), primitive);
        NativeBattlePilotVisualIdentity b = NativeBattlePilotVisualLifetimes.Identity(279, arena, bank, Slot(second));
        return a with { ResourceGeneration = unchecked((b.Generation * 397 ^ task.Generation) * 397 ^ second) };
    }

    internal static NativeBattleGeometryPrimitive Discrete(NativeBattleGeometryPrimitive primitive, NativeBattlePilotVisualIdentity identity)
    {
        if (primitive.Words.Length != 6 || (byte)(primitive.Words[1] >> 24) != 0x2A)
        {
            throw new InvalidDataException("Selected fixed white capture requires its actual native screen quad.");
        }
        int[] words = [2, 3, 4, 5];
        System.Numerics.Vector3 Corner(int index)
        {
            uint xy = primitive.Words[words[index]];
            return new(unchecked((short)xy), unchecked((short)(xy >> 16)), primitive.VertexPayload4[index]);
        }
        return primitive with { PilotGeometry = new(identity, Corner(0), Corner(1), Corner(2), Corner(3), 4, false) { ScreenSpace = true, ScreenScale = 8, SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } };
    }

    private static NativeBattleModulePoolTask Containing(ImmutableArray<NativeBattleEffectTaskPool> pools, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length))
            {
                return pool.Slots[checked((int)(offset / pool.Stride))];
            }
        }
        throw new InvalidDataException("Selected programme presentation allocation leaves its actual task banks.");
    }
}
