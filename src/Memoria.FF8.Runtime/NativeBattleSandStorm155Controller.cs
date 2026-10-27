using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSandStorm155Controller
{
    public static NativeBattleSandStorm155Frame Initialize(NativeBattleSandStorm155Frame frame, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        NativeBattleEffectTaskPool root = ResetPool(frame, 0x23F0818, 0x23F0808, 16, 1);
        (root, int rootSlot) = root.Append(0x607BB0);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = ResetPool(frame, 0x23F1AE8, 0x23F1AF8, 36, 100);
        (children, int director) = children.Append(0x606DB0);
        children = children.WriteSlot(director, children.Slots[director].WriteWord(12, 0));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Sand Storm requires its actual first contact.");
        frame = Dword(frame, 0x23F0800, inputs.Packets.ReadContact(contact).Primary.ActorIndex);
        frame = Dword(frame, 0x23F2950, Parameters(frame).Actor78);
        frame = Dword(frame, 0x23F2940, 0x1D99A78);
        foreach (uint bank in new uint[] { 0x23F0828, 0x23F1188 })
        {
            for (int index = 0; index < 100; index++)
            {
                frame = Dword(frame, checked(bank + (uint)(index * 24)), 0);
            }
        }
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Sand Storm requires its actual texture queue.");
        textures = textures.Register5099A0(resources.Camera);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23F2954)?.ArenaOffset ?? throw new NotSupportedException("Sand Storm requires its executed texture initializer.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Sand Storm requires its texture allocation."), tim);
        frame = Dword(frame, 0x23F2954, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [root, children], ReturnedDescriptor = 0x23F0818, DrawArenaOffset = null }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }

    public static NativeBattleSandStorm155Frame Advance(NativeBattleSandStorm155Frame frame, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23F0818 || frame.Effect.Pools[1].DescriptorAddress != 0x23F1AE8)
        {
            throw new InvalidDataException("Sand Storm requires its actual root and child pool owners.");
        }
        NativeBattleSandStorm155Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x607BB0)
            {
                throw new NotSupportedException("Sand Storm reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : 0x23F0818u } };
        return roots.Survivors == 0 ? DetachBanks(current) : current;
    }

    public static (NativeBattleSandStorm155Frame Frame, int Result) AdvanceRoot(NativeBattleSandStorm155Frame frame, uint address, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs)
    {
        ushort old = Read(frame, address).ReadWord(12);
        frame = Dword(frame, 0x2404230, (old & 1) == 0 ? 0x23F4230u : 0x23FC230u);
        NativeBattleSandStorm155Frame current = frame;
        NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, slot) =>
        {
            current = WithPool(current, 1, pool);
            uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x606DB0 => AdvanceDirector(current, child, resources, inputs),
                0x607130 => NativeBattleSandStorm155Geometry.Displace607130(current, child, resources, inputs.GraphicsFlags),
                0x607210 => NativeBattleSandStorm155Mesh.Advance607210(current, child, resources, inputs, inputs.GraphicsFlags),
                0x6076E0 => AdvanceSpray(current, child, resources, inputs),
                0x607920 => AdvanceSpiral(current, child, resources, inputs),
                _ => throw new NotSupportedException("Sand Storm reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[1], result);
        });
        frame = WithPool(current, 1, children.Pool);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(old + 1))));
        return (frame, children.Survivors == 0 ? 2 : 0);
    }
}
