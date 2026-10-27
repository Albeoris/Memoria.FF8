using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect140Controller
{
    public static NativeBattleEffect140Frame Initialize(NativeBattleEffect140Frame frame, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        NativeBattleEffectIoState io = frame.Memory.EffectIo ?? throw new NotSupportedException("Effect140 requires its shared IO callback owner.");
        frame = frame with { Memory = frame.Memory with { EffectIo = io with { Callback = 0x6A6360 } } };
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect140 entry requires its inactive root bank.");
        }
        var bound = frame.State.BindSources(RequireStaging(frame), resources);
        frame = frame with { State = bound.State, Staging = bound.Staging };
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect140 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        if (count == 0)
        {
            throw new DivideByZeroException("Effect140's original recipient mean requires a nonzero native contact count.");
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect140 requires its actual recipient pointer.");
        int total = 0;
        for (int index = 0; index < count; index++)
        {
            int actor = packets.ReadContact(first + index).Primary.ActorIndex;
            total = unchecked(total + Position(frame, actor, inputs).Z);
        }
        NativeCrtRandomState random = frame.Random.Draw(out ushort randomValue);
        int texture = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2517AA8)?.ArenaOffset ?? throw new NotSupportedException("Effect140 requires its executed texture initializer.");
        uint commonSize = NativeBattleEffectArena.Length;
        frame = frame with { Random = random, State = frame.State.WriteDword(0x2517B1C, (uint)randomValue).WriteDword(0x2517AA8, CommonAddress + (uint)texture).WriteDword(0x2517B28, CommonAddress).WriteDword(0x2517AA4, checked(CommonAddress + commonSize - 0x20000)).WriteDword(0x2517AA0, checked(CommonAddress + commonSize - 0x10000)).WriteDword(0x2517AB0, 0x1D99A78).WriteDword(0x2517AB4, ModelAddress(parameters.Actor78)).WriteDword(0x2517B10, unchecked((uint)(total / count))).WriteDword(0x2517B18, unchecked((uint)(total / count + 3690))) };
        frame = inputs.Execute(frame, new NativeBattleEffect140UploadTim505E30(CommonAddress + (uint)texture));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2517AE8, 0x2517AF8, 20, 1);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, 12, CopyBytes(frame.State.RootBankStorage!, 12, NativeBattleEffectArena.Clear(), 0, 8), 8);
        (root, _) = root.Append(0x6A6530);
        frame = frame with { State = frame.State with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = 0x2517AE8 } };
        frame = ClearDwords701200(frame, 0x2517B04, 2);
        foreach (uint source in new uint[] { 0x11D04B4, 0x11D2594, 0x11D3778 })
        {
            frame = NativeBattleEffect140Meshes.InitializeW(frame, source, resources, inputs);
        }
        return frame;
    }

    public static NativeBattleEffect140Frame Advance(NativeBattleEffect140Frame frame, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        (frame, int survivors) = AdvancePool(frame, 0x2517AE8, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        frame = frame with { State = frame.State with { RootBankStorage = NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2517AE8)) } };
        foreach (uint descriptor in frame.Effect.Pools.Where(pool => pool.DescriptorAddress != 0x2517AE8).Select(pool => pool.DescriptorAddress).ToArray())
        {
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect140Frame Frame, int Survivors) AdvancePool(NativeBattleEffect140Frame frame, uint descriptor, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        int index = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect140Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect140Step AdvanceTask(NativeBattleEffect140Frame frame, uint task, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs) => Read(frame, task).Callback switch
    {
        0x6A6530 => AdvanceRoot6A6530(frame, task, resources, inputs),
        0x6A6750 => AdvanceMain6A6750(frame, task, resources, inputs),
        0x6A7BF0 or 0x6A7D90 => NativeBattleEffect140Particles.Advance(frame, task, resources, inputs),
        _ => throw new NotSupportedException("Effect140 reached an unsupported task callback.")
    };

    public static NativeBattleEffect140Frame InitializePool(NativeBattleEffect140Frame frame, uint descriptor, uint bank, ushort stride, ushort count)
    {
        if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == descriptor))
        {
            NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
        }
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, bank, stride, count);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect140 requires its current COMMON owner.");
        for (int slot = 0; slot < count; slot++)
        {
            int offset = checked((int)(bank - CommonAddress) + slot * stride + 12);
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, slot * stride + 12, CopyBytes(arena, offset, NativeBattleEffectArena.Clear(), 0, stride - 12), stride - 12);
        }
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools.Add(pool);
        return frame with { Effect = frame.Effect with { Pools = pools }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]) };
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect140 requires a physical model.");
    public static int Actor(uint model) => model >= 0x1D972C0 && (model - 0x1D972C0) % 156 == 0 && (model - 0x1D972C0) / 156 < 7 ? (int)((model - 0x1D972C0) / 156) : throw new InvalidDataException("Effect140 model pointer leaves the physical rows.");
    private static NativeBattleVector Position(NativeBattleEffect140Frame frame, int actor, NativeBattleEffect140Inputs inputs)
    {
        byte[] bytes = inputs.ReadModelPosition8(frame, actor).Read(0, 6);
        return new(BitConverter.ToInt16(bytes, 0), BitConverter.ToInt16(bytes, 2), BitConverter.ToInt16(bytes, 4));
    }
    private static void RequireBinding(NativeBattleEffect140Frame frame)
    {
        if (frame.Effect.RequestedEffect != 140 || frame.Effect.Binding.Entry != 0x6A6300 || frame.Effect.Binding.Initializer != 0x6A6310)
        {
            throw new InvalidDataException("Effect140 requires its selected native entry.");
        }
    }
}
