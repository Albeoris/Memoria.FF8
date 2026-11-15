using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal enum NativeBattleGilgameshParticleVariant
{
    Masamune,
    Excalipoor,
    ExcalipoorSparks
}

internal abstract partial record BattleExecutionReference
{
    internal sealed record GilgameshGroup(BattleContactOccurrence Contact, NativeBattleGilgameshParticleVariant Variant, ushort? OriginalWord) : BattleExecutionReference;
    internal sealed record GilgameshOccupiedParticle(GilgameshGroup Group) : BattleExecutionReference;
}

internal static class NativeBattleGilgameshOriginalParticleGroups
{
    internal static ushort EncodeWord(int ordinal, NativeBattleGilgameshParticleVariant variant) =>
        unchecked((ushort)((variant == NativeBattleGilgameshParticleVariant.ExcalipoorSparks ? 0x100u : 1u) << (ordinal & 31)));

    internal static uint EncodeRow(ushort word) => unchecked((uint)(short)word);
    internal static bool Matches(uint row, ushort word) => (row & EncodeRow(word)) != 0;

    internal static ushort? ExpandedProjection(int ordinal, NativeBattleGilgameshParticleVariant variant) =>
        ordinal < (variant == NativeBattleGilgameshParticleVariant.Masamune ? 16 : 8) ? EncodeWord(ordinal, variant) : null;
}

internal sealed record NativeBattleGilgameshParticleBank(uint Address, int Stride, int Capacity, NativeBattleEffectArena Arena);

internal sealed record NativeBattleGilgameshAttackStorage(uint ChildAddress, ushort ChildCapacity, int OccurrenceCount, NativeBattleGilgameshParticleBank ShortRows, NativeBattleGilgameshParticleBank LongRows)
{
    internal const uint ChildDescriptor = 0x21FFD58;
    internal const uint OriginalChildAddress = 0x21FFD68;
    internal const ushort ChildStride = 36;
    internal const ushort OriginalChildCapacity = 100;
    internal const int OriginalParticleCapacity = 200;

    internal static bool Supports(ushort effect) => effect is 328 or 329 or 330;

    internal static (NativeBattleEffectMemory Memory, NativeBattleGilgameshAttackStorage Storage) Reserve(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, int count)
    {
        ushort childCapacity = checked((ushort)(OriginalChildCapacity + 5 * count));
        int shortCapacity = Math.Max(OriginalParticleCapacity, checked((effect.Binding.Effect == 328 ? 24 : 22) * count));
        int longCapacity = Math.Max(OriginalParticleCapacity, checked((effect.Binding.Effect == 328 ? 24 : 12) * count));
        int shortBytes = checked(shortCapacity * 24);
        int longBytes = checked(longCapacity * 32);
        if (shortBytes > NativeBattleEffectArena.Length || longBytes > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Gilgamesh attack particles exceed the addressed storage extent.");
        }
        (memory, uint child) = NativeBattlePresentationTaskStorage.Reserve(memory, effect, packets.Roster, ChildStride, childCapacity);
        (memory, uint shortAddress) = NativeBattlePresentationTaskStorage.ReserveByteRange(memory, effect, packets.Roster, shortBytes);
        (memory, uint longAddress) = NativeBattlePresentationTaskStorage.ReserveByteRange(memory, effect, packets.Roster, longBytes);
        NativeBattleGilgameshParticleBank shortRows = new(shortAddress, 24, shortCapacity, NewArena(shortBytes));
        NativeBattleGilgameshParticleBank longRows = new(longAddress, 32, longCapacity, NewArena(longBytes));
        return (memory, new(child, childCapacity, count, shortRows, longRows));

        static NativeBattleEffectArena NewArena(int bytes) => NativeBattleEffectArena.BorrowWorking(new NativeBattleWorkingMemory(bytes), 0, bytes);
    }

    internal static bool ValidChildBank(NativeBattleEffectTaskPool pool) => pool.DescriptorAddress == ChildDescriptor && pool.Stride == ChildStride &&
        (pool.BankAddress == OriginalChildAddress && pool.Slots.Length == OriginalChildCapacity ||
        pool.Slots.Length >= OriginalChildCapacity && NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, ChildStride, pool.Slots.Length));

    internal static bool ValidChildBank(NativeBattleOriginalEffectState effect, NativeBattleEffectTaskPool pool)
    {
        if (!ValidChildBank(pool))
        {
            return false;
        }
        return effect.GilgameshAttackStorage is { } storage
            ? Supports(effect.Binding.Effect) && pool.BankAddress == storage.ChildAddress && pool.Slots.Length == storage.ChildCapacity
            : pool.BankAddress == OriginalChildAddress;
    }

    internal void Validate(ushort effect)
    {
        int shortCapacity = Math.Max(OriginalParticleCapacity, checked((effect == 328 ? 24 : 22) * OccurrenceCount));
        int longCapacity = Math.Max(OriginalParticleCapacity, checked((effect == 328 ? 24 : 12) * OccurrenceCount));
        if (!Supports(effect) || OccurrenceCount < 1 || ChildCapacity != OriginalChildCapacity + 5 * OccurrenceCount ||
            !ValidBank(ShortRows, 24, shortCapacity) || !ValidBank(LongRows, 32, longCapacity) ||
            ChildAddress + (ulong)ChildStride * ChildCapacity > ShortRows.Address || End(ShortRows) > LongRows.Address)
        {
            throw new InvalidDataException("Gilgamesh lost its reserved attack task and particle owners.");
        }

        static bool ValidBank(NativeBattleGilgameshParticleBank bank, ushort stride, int capacity) =>
            bank.Stride == stride && bank.Capacity == capacity && bank.Arena.LogicalLength == stride * capacity &&
            NativeBattlePresentationTaskStorage.IsHostedBank(bank.Address, stride, capacity);
        static ulong End(NativeBattleGilgameshParticleBank bank) => bank.Address + (ulong)bank.Stride * (uint)bank.Capacity;
    }
}

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    internal NativeBattleGilgameshAttackStorage? GilgameshAttackStorage { get; init; }
}

internal readonly struct NativeBattleGilgameshParticleRows
{
    private readonly bool longRows;
    private readonly NativeBattleGilgameshParticleBank? hosted;

    internal NativeBattleGilgameshParticleRows(NativeBattleEffectArena original, bool longRows, NativeBattleGilgameshParticleBank? hosted)
    {
        this.longRows = longRows;
        this.hosted = hosted;
        Arena = hosted is null ? original : NativeBattleTaskOperation.AdmitCurrentRetainedEffectStorage(hosted.Arena);
    }

    internal NativeBattleEffectArena Arena { get; }
    internal int Capacity => hosted?.Capacity ?? NativeBattleGilgameshAttackStorage.OriginalParticleCapacity;
    internal uint Address => hosted?.Address ?? NativeBattleEffectNodeMemory.ArenaAddress + (longRows ? 0x2F800u : 0x2E000u);
    internal int Record(int index) => (hosted is null ? longRows ? 0x2F800 : 0x2E000 : 0) + index * (longRows ? 32 : 24);

    internal BattleExecutionReference.GilgameshGroup? ReadGroup(NativeBattleModulePoolTask task)
    {
        if (hosted is null)
        {
            return null;
        }
        var group = task.ReadExecutionReference<BattleExecutionReference.GilgameshGroup>(0x16, 2);
        NativeBattleGilgameshParticleVariant expected = task.Callback switch
        {
            0x590B60 or 0x590F10 => NativeBattleGilgameshParticleVariant.Masamune,
            0x591850 => NativeBattleGilgameshParticleVariant.ExcalipoorSparks,
            _ => NativeBattleGilgameshParticleVariant.Excalipoor
        };
        return group.Variant == expected ? group : throw new InvalidDataException("Gilgamesh particle group does not match its callback variant.");
    }

    internal bool Free(int record) => hosted is null
        ? NativeBattleStaticMeshContext88.Dword(Arena, record) == 0
        : !Arena.TryReadExecutionReference<BattleExecutionReference.GilgameshOccupiedParticle>(record, 4, out _);

    internal bool Matches(int record, ushort originalWord, BattleExecutionReference.GilgameshGroup? group)
    {
        if (group is null)
        {
            return NativeBattleGilgameshOriginalParticleGroups.Matches(NativeBattleStaticMeshContext88.Dword(Arena, record), originalWord);
        }
        return Arena.TryReadExecutionReference<BattleExecutionReference.GilgameshOccupiedParticle>(record, 4, out var occupied) &&
            occupied.Group.Contact == group.Contact && occupied.Group.Variant == group.Variant;
    }

    internal void Occupy(int record, ushort originalWord, BattleExecutionReference.GilgameshGroup? group)
    {
        if (group is null)
        {
            NativeBattleStaticMeshContext88.Put(Arena, record, NativeBattleGilgameshOriginalParticleGroups.EncodeRow(originalWord));
        }
        else
        {
            ImmutableArray<byte>? bytes = group.OriginalWord is ushort projection ? BitConverter.GetBytes(NativeBattleGilgameshOriginalParticleGroups.EncodeRow(projection)).ToImmutableArray() : null;
            Arena.WriteExecutionReference(record, 4, new BattleExecutionReference.GilgameshOccupiedParticle(group), bytes);
        }
    }

    internal static NativeBattleModulePoolTask WriteGroup(NativeBattleModulePoolTask task, int ordinal, NativeBattleGilgameshParticleVariant variant, BattleContactOccurrence? occurrence)
    {
        if (occurrence is not { } captured)
        {
            return task.WriteWord(0x16, NativeBattleGilgameshOriginalParticleGroups.EncodeWord(ordinal, variant));
        }
        ushort? word = NativeBattleGilgameshOriginalParticleGroups.ExpandedProjection(ordinal, variant);
        task.WriteExecutionReference(0x16, 2, new BattleExecutionReference.GilgameshGroup(captured, variant, word), word is ushort raw ? BitConverter.GetBytes(raw).ToImmutableArray() : null);
        return task;
    }
}
