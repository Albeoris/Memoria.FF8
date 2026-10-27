using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect69Controller
{
    public static NativeBattleEffect69Frame Initialize(NativeBattleEffect69Frame frame, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.FixedBanks.Values.Any(bank => bank is null))
        {
            throw new InvalidDataException("Effect69 entry requires its retained inactive physical banks.");
        }
        int actor = Parameters(frame).Actor78;
        uint model = PhysicalModelAddress(actor);
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, model) };
        frame = inputs.Execute(frame, new NativeBattleEffect69ModelFlags(actor, 0xFFFF, 0xC, false));
        frame = inputs.Execute(frame, new NativeBattleEffect69ModelPositionZ(actor, -2000));
        frame = InitializePool(frame, 0);
        (frame, _) = AppendPool(frame, 0, RootCallback, 4);
        frame = inputs.Execute(frame, new NativeBattleEffect69RegisterCamera(resources.Camera));
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleEffect69Frame Advance(NativeBattleEffect69Frame frame, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is < 1 or > 2)
        {
            throw new InvalidDataException("Effect69 requires its actual root and optional initialized main bank.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            NativeBattleCureFamilyPool shape = Pools[index];
            pool.Validate();
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != shape.Bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity || frame.State.FixedBanks[shape.Bank] is not null)
            {
                throw new InvalidDataException("Effect69 lost its exclusive current physical task bank.");
            }
        }
        (frame, int survivors) = Scan(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.Head is not null)
            {
                throw new InvalidDataException("Effect69 root retired before its actual main list.");
            }
            frame = frame with { State = frame.State.AttachBank(index, NativeBattleEffectTaskMemory.CaptureBank(pool)) };
        }
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = 0, Pools = [] } };
    }

    public static NativeBattleEffect69Step AdvanceTask(NativeBattleEffect69Frame frame, uint address, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        MainCallback => AdvanceMain(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect69 reached an unsupported actual private task callback.")
    };

    private static NativeBattleEffect69Frame InitializePool(NativeBattleEffect69Frame frame, int index)
    {
        if (index != frame.Effect.Pools.Length)
        {
            throw new InvalidDataException("Effect69 pool initialization changed native descriptor order.");
        }
        NativeBattleCureFamilyPool shape = Pools[index];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena retained = frame.State.ReadBank(index);
        for (int slot = 0; slot < pool.Slots.Length; slot++)
        {
            pool = pool.WriteSlot(slot, CopyTask(pool.Slots[slot], 12, Slice(retained, slot * shape.Stride + 12, shape.Stride - 12), shape.Stride - 12));
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, State = frame.State.DetachBank(index) };
    }

    private static (NativeBattleEffect69Frame Frame, uint Address) AppendPool(NativeBattleEffect69Frame frame, int index, uint callback, int clearLength)
    {
        (NativeBattleEffectTaskPool pool, int slot) = frame.Effect.Pools[index].Append(callback);
        uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
        return (ClearBytes(frame, address + 12, clearLength), address);
    }

    private static (NativeBattleEffect69Frame Frame, int Survivors) Scan(NativeBattleEffect69Frame frame, int index, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        NativeBattleEffect69Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, pool) } };
            NativeBattleEffect69Step advanced = AdvanceTask(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            current = advanced.Frame;
            return new(current.Effect.Pools[index], advanced.Result);
        });
        return (current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, step.Pool) } }, step.Survivors);
    }

    public static (NativeBattleEffect69Frame Frame, int Value) NextPrivateRandom6FFFC0(NativeBattleEffect69Frame frame)
    {
        uint seed = unchecked(frame.State.ReadDword(RandomGlobal) * 125 + 14) & 0x7FFF;
        return (frame with { State = frame.State.WriteDword(RandomGlobal, seed) }, (int)seed);
    }

    private static int Caster(NativeBattleEffect69Frame frame)
    {
        uint delta = unchecked(frame.State.ReadDword(CasterGlobal) - 0x1D972C0);
        return delta % 156 == 0 && delta / 156 < 7 ? (int)(delta / 156) : throw new InvalidDataException("Effect69 requires its current physical caster pointer.");
    }

    private static void RequireBinding(NativeBattleEffect69Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect69 requires its original selected native entry and initializer.");
        }
    }
}
