using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static NativeBattleRayBomb307Frame Initialize(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Ray-Bomb requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Ray-Bomb requires its texture queue.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Ray-Bomb requires its current resource arena.");
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (roots, int rootSlot) = roots.Append(RootCallback);
        roots = roots with { Slots = roots.Slots.SetItem(rootSlot, roots.Slots[rootSlot].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (children, int mainSlot) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(mainSlot, children.Slots[mainSlot].WriteWord(12, 0)) };
        NativeBattleRayBomb307State state = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(0x2242570, 0x1D99A78).WriteDword(0x2242574, Parameters(frame).Actor78);
        textures = textures.Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        for (int index = 0; index < 100; index++)
        {
            state = state with { Particles = state.Particles.Write(index * 24, BitConverter.GetBytes(0u)) };
        }
        return frame with { State = state, Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleRayBomb307Frame Advance(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != ChildDescriptor)
        {
            throw new InvalidDataException("Ray-Bomb requires its two original task pools.");
        }
        NativeBattleRayBomb307Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            NativeBattleModulePoolTask root = Read(current, address);
            if (root.Callback == TextureCallback)
            {
                NativeBattleRayBomb307TaskStep restored = AdvanceTexture(current, address, inputs);
                current = restored.Frame;
                return new(current.Effect.Pools[0], restored.Result);
            }
            if (root.Callback != RootCallback)
            {
                throw new NotSupportedException("Ray-Bomb reached an unsupported root callback.");
            }
            current = current with { State = current.State.WriteDword(CursorGlobal, checked(current.State.ReadDword(WorkingGlobal) + ((root.ReadWord(12) & 1) == 0 ? 0u : PacketBankLength))) };
            NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((childPool, childSlot) =>
            {
                current = WithPool(current, 1, childPool);
                NativeBattleRayBomb307TaskStep step = AdvanceChild(current, checked(childPool.BankAddress + (uint)(childSlot * childPool.Stride)), resources, inputs);
                current = step.Frame;
                return new(current.Effect.Pools[1], step.Result);
            });
            current = WithPool(current, 1, children.Pool);
            (current, int alive) = AdvanceSlots(current, inputs);
            current = Write(current, address, Read(current, address).WriteWord(12, unchecked((ushort)(Read(current, address).ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], (children.Survivors | alive) == 0 ? 2 : 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static NativeBattleRayBomb307TaskStep AdvanceChild(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs) => Read(frame, address).Callback switch
    {
        MainCallback => AdvanceMain(frame, address, resources, inputs),
        TextureCallback => AdvanceTexture(frame, address, inputs),
        0x5A25E0 => AdvanceTarget(frame, address, resources, inputs),
        0x5A1EE0 => AdvanceBeam(frame, address, resources, inputs),
        0x5A1AE0 or 0x5A1C50 or 0x5A23E0 => AdvanceMesh(frame, address, resources, inputs),
        0x5A1D90 or 0x5A2340 or 0x5A24D0 => AdvanceSprite(frame, address, inputs),
        _ => throw new NotSupportedException("Ray-Bomb reached an unsupported child callback.")
    };

    private static NativeBattleRayBomb307TaskStep AdvanceTexture(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(12) == 0)
        {
            frame = Write(frame, address, task.WriteWord(14, 0));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307RestoreTexture(checked(frame.State.ReadDword(WorkingGlobal) + TextureRestoreOffset), address + 14));
        }
        task = Read(frame, address);
        int result = task.ReadWord(14) != 0 ? 2 : 0;
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), result);
    }

    private static NativeBattleEffectParameters Parameters(NativeBattleRayBomb307Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Ray-Bomb requires its captured action parameters.");
    private static void RequireBinding(NativeBattleRayBomb307Frame frame)
    {
        if (frame.Effect.RequestedEffect != 307 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Ray-Bomb requires its original selected entry.");
        }
    }
}
