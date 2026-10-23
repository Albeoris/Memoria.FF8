namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBladeShot12Controller
{
    public static NativeBattleBladeShot12Frame Initialize(NativeBattleBladeShot12Frame frame, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Blade Shot activation requires its inactive physical bank owners.");
        }
        uint caster = ModelAddress(Parameters(frame).Actor78);
        frame = Dword(Dword(frame, 0x25460FC, 0x1D99A78), 0x2545EF0, caster);
        frame = inputs.Execute(frame, new NativeBattleBladeShot12ActorMeshMask124(caster, 0x80, 0));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Blade Shot requires its captured first physical contact pointer.");
        uint target = ModelAddress(inputs.Packets.ReadContact(first).Primary.ActorIndex);
        frame = Dword(frame, 0x25460F8, target);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Blade Shot requires its actual texture arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2545EF4)?.ArenaOffset ?? throw new NotSupportedException("Blade Shot requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Blade Shot requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = Dword(frame with { Memory = frame.Memory with { Textures = textures } }, 0x2545EF4, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim));
        NativeBattleEffectTaskPool roots = ResetPool(frame, 0x2556110, 0x2545188, 100, 1);
        (roots, int root) = roots.Append(0x6E8C90);
        frame = frame with { Effect = frame.Effect with { Pools = [roots] }, State = frame.State with { RootBankStorage = null } };
        uint address = roots.BankAddress + (uint)(root * roots.Stride);
        frame = NativeBattleBladeShot12Geometry.FillDwords6DA380(frame, address + 12, 0, 22);
        NativeBattleBladeShot12PointStep point = inputs.ReadBone502170(frame, caster, 7, 0);
        frame = WriteBytes(point.Frame, address + 0x1C, point.Point8, 6);
        point = inputs.ReadAnchor571400(frame, target);
        frame = WriteBytes(point.Frame, address + 0x24, point.Point8, 8);
        NativeBattleModulePoolTask task = Read(frame, address);
        short dx = unchecked((short)(Signed(task, 0x24) - Signed(task, 0x1C)));
        short dz = unchecked((short)(Signed(task, 0x28) - Signed(task, 0x20)));
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dz * dz));
        short normalizedX = unchecked((short)((dx << 12) / length));
        short normalizedZ = unchecked((short)((dz << 12) / length));
        int heading = NativePrecision53.RoundInteger(Math.Atan2(normalizedX, normalizedZ) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)normalizedX)).WriteWord(0x5E, unchecked((ushort)normalizedZ)).WriteWord(0x56, unchecked((ushort)heading)).WriteWord(0x58, 0x400));
        frame = InitializeInterpolation6E8C60(frame, address + 0x14, 32);
        NativeBattleEffectTaskPool childrenA = ResetPool(frame, 0x2556120, 0x2545EF8, 32, 16);
        NativeBattleEffectTaskPool childrenB = ResetPool(frame, 0x2556130, 0x25451F0, 52, 64);
        textures = frame.Memory.Textures!.Register5099A0(resources.Camera);
        return frame with { Memory = frame.Memory with { Textures = textures }, Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], childrenA, childrenB], ReturnedDescriptor = 0x2556110, DrawArenaOffset = null }, State = frame.State with { ChildABankStorage = null, ChildBBankStorage = null } };
    }

    public static NativeBattleBladeShot12Frame InitializeInterpolation6E8C60(NativeBattleBladeShot12Frame frame, uint address, int duration)
    {
        frame = WriteBytes(frame, address, ReadBytes(frame, address + 8, 8), 8);
        frame = WriteBytes(frame, address + 14, NativeBattleEffectArena.Clear().Write(0, new byte[2]), 2);
        return WriteBytes(frame, address + 22, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)(4096 / duration)))), 2);
    }

    public static NativeBattleBladeShot12Frame Advance(NativeBattleBladeShot12Frame frame, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 3 || frame.Effect.Pools[0].DescriptorAddress != 0x2556110 || frame.Effect.Pools[0].BankAddress != 0x2545188 || frame.Effect.Pools[0].Stride != 100 || frame.Effect.Pools[0].Slots.Length != 1 || frame.Effect.Pools[1].DescriptorAddress != 0x2556120 || frame.Effect.Pools[1].BankAddress != 0x2545EF8 || frame.Effect.Pools[1].Stride != 32 || frame.Effect.Pools[1].Slots.Length != 16 || frame.Effect.Pools[2].DescriptorAddress != 0x2556130 || frame.Effect.Pools[2].BankAddress != 0x25451F0 || frame.Effect.Pools[2].Stride != 52 || frame.Effect.Pools[2].Slots.Length != 64 || frame.State.RootBankStorage is not null || frame.State.ChildABankStorage is not null || frame.State.ChildBBankStorage is not null)
        {
            throw new InvalidDataException("Blade Shot requires its exclusive three physical task owners.");
        }
        NativeBattleBladeShot12Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x6E8C90)
            {
                throw new NotSupportedException("Blade Shot reached an unsupported root callback.");
            }
            (current, int result) = AdvanceMain(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x2556110u } };
        return step.Survivors == 0 ? DetachBanks(current) : current;
    }

    private static (NativeBattleBladeShot12Frame Frame, int Survivors) AdvanceChildren(NativeBattleBladeShot12Frame frame, int index, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        NativeBattleBladeShot12Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            uint address = pool.BankAddress + (uint)(slot * pool.Stride);
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x6E9B50 or 0x6E9C10 => AdvanceSprite(current, address, resources, inputs),
                0x6E9D60 => AdvanceMesh(current, address, resources, inputs),
                _ => throw new NotSupportedException("Blade Shot reached an unsupported child callback.")
            };
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }
}
