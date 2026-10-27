using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaControllerFrame([property: JsonRequired] NativeBattleShivaEntryStep Entry, [property: JsonRequired] int? CurrentDrawOffset);
public sealed record NativeBattleShivaChildStep(NativeBattleShivaControllerFrame Frame, int Result);

/// <summary>5C7F50's root scheduler over the two banks established by5C0D50.</summary>
public static class NativeBattleShivaController
{
    public static NativeBattleShivaControllerFrame Advance5C7F50(NativeBattleShivaControllerFrame frame, Func<NativeBattleShivaControllerFrame, uint, NativeBattleShivaChildStep> advanceChild)
    {
        ArgumentNullException.ThrowIfNull(advanceChild);
        frame.Entry.Effect.Validate();
        if (frame.Entry.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        RequireController(frame);
        NativeBattleShivaControllerFrame current = frame;
        NativeBattleEffectTaskStep main = frame.Entry.Effect.Pools[0].Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask root = pool.Slots[slot];
            if (root.Callback != 0x5C7F50)
            {
                throw new NotSupportedException("Native Shiva root requires its5C7F50 callback.");
            }
            current = current with
            {
                Entry = current.Entry with { Effect = current.Entry.Effect with { Pools = current.Entry.Effect.Pools.SetItem(0, pool) } },
                CurrentDrawOffset = (root.ReadWord(0x0C) & 1) != 0 ? 0x10000 : 0x18000
            };
            NativeBattleEffectTaskStep children = current.Entry.Effect.Pools[1].Advance((childPool, childSlot) =>
            {
                current = current with { Entry = current.Entry with { Effect = current.Entry.Effect with { Pools = current.Entry.Effect.Pools.SetItem(1, childPool) } } };
                uint address = checked(childPool.BankAddress + (uint)(childSlot * childPool.Stride));
                NativeBattleShivaChildStep child = advanceChild(current, address);
                current = child.Frame;
                return new(current.Entry.Effect.Pools[1], child.Result);
            });
            current = current with { Entry = current.Entry with { Effect = current.Entry.Effect with { Pools = current.Entry.Effect.Pools.SetItem(1, children.Pool) } } };
            pool = current.Entry.Effect.Pools[0];
            root = pool.Slots[slot];
            root = root.WriteWord(0x0C, unchecked((ushort)(root.ReadWord(0x0C) + 1)));
            pool = pool.WriteSlot(slot, root);
            return new(pool, children.Survivors == 0 ? 2 : 0);
        });
        return current with { Entry = current.Entry with { Effect = current.Entry.Effect with { Pools = current.Entry.Effect.Pools.SetItem(0, main.Pool), ReturnedDescriptor = main.Survivors == 0 ? 0u : 0x22BC180u } } };
    }

    private static void RequireController(NativeBattleShivaControllerFrame frame)
    {
        NativeBattleOriginalEffectState effect = frame.Entry.Effect;
        if (effect.Binding is not { Effect: 185, Entry: 0x5C0D50, Initializer: 0x5C0D30 } || effect.Parameters is null || effect.ReturnedDescriptor != 0x22BC180 || effect.Pools.Length != 2 || frame.CurrentDrawOffset is not (null or 0x10000 or 0x18000))
        {
            throw new InvalidDataException("Native Shiva scheduler requires its executed5C0D50 entry and current draw-bank publication.");
        }
        (uint Descriptor, uint Bank, ushort Stride, int Capacity)[] banks = [(0x22BC180, 0x22BC160, 0x10, 2), (0x22BC198, 0x22BC1A8, 0x24, 100)];
        for (int index = 0; index < banks.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            pool.Validate();
            if (pool.DescriptorAddress != banks[index].Descriptor || pool.BankAddress != banks[index].Bank || pool.Stride != banks[index].Stride || pool.Slots.Length != banks[index].Capacity)
            {
                throw new InvalidDataException("Native Shiva scheduler lost an original descriptor, bank, stride or capacity.");
            }
        }
    }
}
