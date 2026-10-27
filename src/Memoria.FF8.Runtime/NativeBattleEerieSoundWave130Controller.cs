using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEerieSoundWave130Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEerieSoundWave130Controller
{
    public static NativeBattleEerieSoundWave130Frame Initialize(NativeBattleEerieSoundWave130Frame frame, NativeBattleEerieSoundWave130Resources resources, NativeBattleEerieSoundWave130Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Eerie Sound Wave requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Eerie Sound Wave requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Eerie Sound Wave requires its current arena."), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        NativeBattleEerieSoundWave130State state = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleEerieSoundWave130Frame Advance(NativeBattleEerieSoundWave130Frame frame, NativeBattleEerieSoundWave130Resources resources, NativeBattleEerieSoundWave130Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Eerie Sound Wave requires its original root and three child pools.");
        }
        NativeBattleEerieSoundWave130Frame current = frame;
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Eerie Sound Wave reached an unsupported root callback.");
            }
            (current, int result) = Root(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattleEerieSoundWave130Frame Frame, int Result) Root(NativeBattleEerieSoundWave130Frame frame, int slot, NativeBattleEerieSoundWave130Resources resources, NativeBattleEerieSoundWave130Inputs inputs)
    {
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[slot];
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(Working(frame) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Put(frame, 0, slot, frame.Effect.Pools[0].Slots[slot].WriteByte(15, 1));
            uint working = Working(frame);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, 0x4CC, 1);
            NativeBattleEffectTaskPool travel = NativeBattleEffectTaskPool.Initialize(TravelDescriptor, working + TravelOffset, 0x74, 12);
            NativeBattleEffectTaskPool impact = NativeBattleEffectTaskPool.Initialize(ImpactDescriptor, working, 0xC0, 3);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, travel, impact] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, travel, impact]) };
            (frame, uint address) = Append(frame, 1, MainCallback);
            NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[0x4C0]);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x20, 0x4AC).Initialize7016B0(resources.MainAct, 0x4AC);
            task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 0x20, workspace);
            int count = Header(frame, inputs).ReadByte(16);
            if (count > 3)
            {
                throw new NotSupportedException("Eerie Sound Wave contacts exceed its three physical sorted actor entries.");
            }
            task = task.WriteWord(14, (ushort)count);
            for (int index = 0; index < count; index++)
            {
                int actor = inputs.Packets.ReadContact(FirstContact(frame, inputs) + index).Primary.ActorIndex;
                task = task.WriteDword(0x10 + index * 4, ModelAddress(actor)).WriteByte(0x1C + index, (byte)index);
            }
            for (int firstIndex = 0; firstIndex < count - 1; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < count; secondIndex++)
                {
                    uint firstActor = task.ReadDword(0x10 + firstIndex * 4);
                    uint secondActor = task.ReadDword(0x10 + secondIndex * 4);
                    short firstX = unchecked((short)(inputs.ReadModel(Actor(firstActor)).Transform ?? throw new NotSupportedException("Eerie Sound Wave requires its first sorted actor position.")).Position28.X);
                    short secondX = unchecked((short)(inputs.ReadModel(Actor(secondActor)).Transform ?? throw new NotSupportedException("Eerie Sound Wave requires its second sorted actor position.")).Position28.X);
                    if (firstX < secondX)
                    {
                        byte firstOrdinal = task.ReadByte(0x1C + firstIndex);
                        byte secondOrdinal = task.ReadByte(0x1C + secondIndex);
                        task = task.WriteDword(0x10 + firstIndex * 4, secondActor).WriteDword(0x10 + secondIndex * 4, firstActor).WriteByte(0x1C + firstIndex, secondOrdinal).WriteByte(0x1C + secondIndex, firstOrdinal);
                    }
                }
            }
            frame = Write(frame, address, task);
        }
        int survivors = 0;
        if (frame.Effect.Pools[0].Slots[slot].ReadByte(15) != 0)
        {
            for (int poolIndex = 1; poolIndex < 4; poolIndex++)
            {
                int selected = poolIndex;
                NativeBattleEerieSoundWave130Frame current = frame;
                NativeBattleEffectTaskStep scanned = frame.Effect.Pools[poolIndex].Advance((pool, taskSlot) =>
                {
                    current = WithPool(current, selected, pool);
                    uint address = checked(pool.BankAddress + (uint)(taskSlot * pool.Stride));
                    (current, int result) = selected switch
                    {
                        1 => Main(current, address, resources, inputs),
                        2 => Travel(current, address, resources, inputs),
                        _ => Impact(current, address, inputs)
                    };
                    return new(current.Effect.Pools[selected], result);
                });
                frame = WithPool(current, selected, scanned.Pool);
                survivors += scanned.Survivors;
            }
            if (survivors == 0)
            {
                return (frame, 2);
            }
        }
        return (Put(frame, 0, slot, frame.Effect.Pools[0].Slots[slot].WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleEerieSoundWave130Frame Frame, int Result) Main(NativeBattleEerieSoundWave130Frame frame, uint address, NativeBattleEerieSoundWave130Resources resources, NativeBattleEerieSoundWave130Inputs inputs)
    {
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        NativeBattleEerieSoundWave130PointStep first = inputs.ReadBone(frame, caster, 0xB, 0x4000);
        frame = first.Frame;
        short yaw = inputs.ReadModel(caster).Rotation14 ?? throw new NotSupportedException("Eerie Sound Wave requires its caster's actual yaw.");
        NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(unchecked((short)-yaw)) with { X = unchecked((short)first.Point.X), Y = unchecked((short)(first.Point.Y - 200)), Z = unchecked((short)first.Point.Z) };
        NativeBattleEffectArena context = NativeBattleEerieSoundWave130Contexts.Compose(inputs.ReadView(), world, -128, Working(frame) + MorphOffset);
        NativeBattleEerieSoundWave130ActAdvanceStep advanced = inputs.AdvanceAct(frame, address, 0, context);
        frame = advanced.Frame;
        if (advanced.Remaining == 0)
        {
            return (frame, 2);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = unchecked((short)task.ReadWord(12));
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleEerieSoundWave130Sound(resources.Sound));
        }
        NativeBattleEerieSoundWave130PointStep origin = inputs.ReadBone(frame, caster, 0xB, 0x1000);
        frame = origin.Frame;
        if (age < unchecked((short)task.ReadWord(14)) * 12 && unchecked((uint)(age % 12)) % 3 == 0)
        {
            (frame, uint child) = Append(frame, 2, TravelCallback);
            if (child != 0)
            {
                int index = age / 12;
                NativeBattleModulePoolTask travel = Read(frame, child).WriteWord(12, 0).WriteByte(14, (byte)(age % 12 == 9 ? 0 : 1)).WriteByte(15, task.ReadByte(0x1C + index)).WriteDword(0x10, task.ReadDword(0x10 + index * 4));
                travel = Vector(travel, 0x14, origin.Point).MarkPayloadUnknown(0x1A, 2);
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(travel, 0x1C, 0x58).Initialize7016B0(resources.TravelAct, 0x58);
                frame = Write(frame, child, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(travel, 0x1C, workspace));
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static (NativeBattleEerieSoundWave130Frame Frame, int Result) Travel(NativeBattleEerieSoundWave130Frame frame, uint address, NativeBattleEerieSoundWave130Resources resources, NativeBattleEerieSoundWave130Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Actor(task.ReadDword(0x10));
        NativeBattleEerieSoundWave130AnchorStep anchor = inputs.ReadAnchor(frame, actor);
        frame = anchor.Frame;
        NativeBattleVector point = new(anchor.Anchor.X, anchor.Anchor.Y, anchor.Anchor.Z);
        NativeBattleVector origin = ReadVector(task, 0x14);
        int dx = point.X - origin.X;
        int dy = point.Y - origin.Y;
        int dz = point.Z - origin.Z;
        int age = unchecked((short)task.ReadWord(12));
        if (age < 24)
        {
            int factor = NativeBattleCameraMath.Sine(age * 1024 / 24);
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Eerie Sound Wave blend requires its geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Eerie Sound Wave blend requires its current data registers."));
            registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - factor)) } };
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)origin.X), unchecked((ushort)origin.Y), unchecked((ushort)origin.Z));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
            registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } };
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)point.X), unchecked((ushort)point.Y), unchecked((ushort)point.Z));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, inputs.Colors);
            point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        }
        if (age == 0 && task.ReadByte(14) == 0)
        {
            (frame, uint child) = Append(frame, 3, ImpactCallback);
            if (child != 0)
            {
                NativeBattleEerieSoundWave130AnchorStep currentAnchor = inputs.ReadAnchor(frame, actor);
                frame = currentAnchor.Frame;
                NativeBattleModulePoolTask impact = Read(frame, child).WriteWord(12, unchecked((ushort)currentAnchor.Anchor.X)).WriteWord(14, unchecked((ushort)currentAnchor.Anchor.Y)).WriteWord(16, unchecked((ushort)currentAnchor.Anchor.Z)).WriteWord(18, unchecked((ushort)currentAnchor.Anchor.W)).WriteDword(0x14, ModelAddress(actor));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(impact, 0x18, 0xA8).Initialize7016B0(resources.ImpactAct, 0xA8);
                frame = Write(frame, child, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(impact, 0x18, workspace));
            }
        }
        NativeBattleActionMatrix world = NativeBattleFunguarLaser277.Basis50CBA0(dx, dy, dz).Matrix with { X = point.X, Y = point.Y, Z = point.Z };
        NativeBattleEerieSoundWave130ActAdvanceStep advanced = inputs.AdvanceAct(frame, address, 1, NativeBattleEerieSoundWave130Contexts.Compose(inputs.ReadView(), world, 0, Working(frame) + MorphOffset));
        frame = advanced.Frame;
        if (advanced.Remaining == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static (NativeBattleEerieSoundWave130Frame Frame, int Result) Impact(NativeBattleEerieSoundWave130Frame frame, uint address, NativeBattleEerieSoundWave130Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEerieSoundWave130ActAdvanceStep advanced = inputs.AdvanceAct(frame, address, 2, NativeBattleEerieSoundWave130Contexts.Impact(inputs.ReadView(), ReadVector(task, 12), Working(frame) + MorphOffset));
        frame = advanced.Frame;
        if (advanced.Remaining == 0)
        {
            return (frame, 2);
        }
        if (advanced.Remaining == 8)
        {
            int actor = Actor(Read(frame, address).ReadDword(0x14));
            int count = Header(frame, inputs).ReadByte(16);
            for (int index = 0; index < count; index++)
            {
                int contact = FirstContact(frame, inputs) + index;
                if (inputs.Packets.ReadContact(contact).Primary.ActorIndex == actor)
                {
                    frame = inputs.Execute(frame, new NativeBattleEerieSoundWave130Contact(contact));
                }
            }
        }
        return (frame, 0);
    }

    public static NativeBattleModulePoolTask Read(NativeBattleEerieSoundWave130Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Eerie Sound Wave task address has no current physical owner.");
    }

    public static NativeBattleEerieSoundWave130Frame Write(NativeBattleEerieSoundWave130Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return Put(frame, index, checked((int)((address - pool.BankAddress) / pool.Stride)), task);
            }
        }
        throw new NotSupportedException("Eerie Sound Wave task write has no current physical owner.");
    }

    private static (NativeBattleEerieSoundWave130Frame Frame, uint Address) Append(NativeBattleEerieSoundWave130Frame frame, int index, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    private static void RequireBinding(NativeBattleEerieSoundWave130Frame frame)
    {
        if (frame.Effect.Binding.Effect != 130 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Eerie Sound Wave requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleEffectParameters Parameters(NativeBattleEerieSoundWave130Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Eerie Sound Wave requires its captured native parameters.");
    private static NativeBattleActionHeaderSlot Header(NativeBattleEerieSoundWave130Frame frame, NativeBattleEerieSoundWave130Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
    private static int FirstContact(NativeBattleEerieSoundWave130Frame frame, NativeBattleEerieSoundWave130Inputs inputs) => Header(frame, inputs).FirstContactIndex ?? throw new NotSupportedException("Eerie Sound Wave requires its captured physical contacts.");
    private static uint Working(NativeBattleEerieSoundWave130Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ModelAddress(int actor) => actor is >= 0 and < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new NotSupportedException("Eerie Sound Wave selected an absent physical actor.");
    private static int Actor(uint pointer) => pointer >= 0x1D972C0 && pointer < 0x1D972C0 + 7 * 156 && (pointer - 0x1D972C0) % 156 == 0 ? checked((int)((pointer - 0x1D972C0) / 156)) : throw new NotSupportedException("Eerie Sound Wave requires an actual physical actor pointer.");
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static NativeBattleVector ReadVector(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
    private static NativeBattleEerieSoundWave130Frame WithPool(NativeBattleEerieSoundWave130Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEerieSoundWave130Frame Put(NativeBattleEerieSoundWave130Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task));
}
