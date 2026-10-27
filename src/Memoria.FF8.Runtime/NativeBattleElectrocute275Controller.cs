using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    public static NativeBattleElectrocute275Frame Initialize(NativeBattleElectrocute275Frame frame, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        _ = FirstContact(frame, inputs);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Electrocute requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Electrocute requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeCrtRandomState random = frame.Random.Draw(out ushort seed);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        NativeBattleElectrocute275State state = frame.State.SeedColdSources(resources).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state, Random = random, Memory = (frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }).SeedSharedEffectRandom7059D0(seed), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleElectrocute275Frame Advance(NativeBattleElectrocute275Frame frame, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 6) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Electrocute requires its original root and five arena task banks.");
        }
        NativeBattleElectrocute275Frame current = frame;
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Electrocute reached an unsupported root callback.");
            }
            (current, int result) = Root(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattleElectrocute275Frame Frame, int Result) Root(NativeBattleElectrocute275Frame frame, int slot, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        NativeBattleModulePoolTask root = Task(frame, 0, slot);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(Working(frame) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))).WriteDword(FadeGlobal, 0) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        int header = Parameters(frame).HeaderIndex7C;
        int count = inputs.Packets.Headers[header].ReadByte(16);
        int firstContact = FirstContact(frame, inputs);
        for (int index = 0; index < count; index++)
        {
            frame = inputs.Execute(frame, new NativeBattleElectrocute275ActorFlags(inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex, 0x800, 0));
        }
        byte caster = checked((byte)Actor(frame.State.ReadDword(CasterGlobal)));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Put(frame, 0, slot, Task(frame, 0, slot).WriteByte(15, 1));
            uint working = Working(frame);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + 0x22A0, 52, 1);
            NativeBattleEffectTaskPool worlds = NativeBattleEffectTaskPool.Initialize(WorldTrailDescriptor, working + 0x13A0, 60, 64);
            NativeBattleEffectTaskPool bones = NativeBattleEffectTaskPool.Initialize(BoneTrailDescriptor, working + 0x9A0, 40, 64);
            NativeBattleEffectTaskPool sprites = NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, working + 0x6A0, 24, 32);
            NativeBattleEffectTaskPool acts = NativeBattleEffectTaskPool.Initialize(ActDescriptor, working, 212, 8);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, worlds, bones, sprites, acts] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, worlds, bones, sprites, acts]) };
            frame = WriteArena(WriteArena(frame, working + BonePointsOffset, new byte[0x5000]), working + WorldPointsOffset, new byte[0x5000]);
            for (int index = 0; index < PointCount; index++)
            {
                frame = WriteArena(frame, working + BonePointsOffset + (uint)(index * PointStride), BitConverter.GetBytes((ushort)0xFFFF));
                frame = WriteArena(frame, working + WorldPointsOffset + (uint)(index * PointStride) + 6, BitConverter.GetBytes((ushort)0xFFFF));
            }
            (frame, uint address) = Append(frame, 1, MainCallback);
            if (address == 0)
            {
                throw new NotSupportedException("Electrocute reached a full original main task bank.");
            }
            frame = Write(frame, address, Read(frame, address).WriteBytes(12, new byte[40]).WriteWord(14, unchecked((ushort)count)));
            for (int index = 0; index < count; index++)
            {
                int actor = inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex;
                uint color = inputs.ReadModel(actor).Color40 ?? throw new NotSupportedException("Electrocute requires its target actor's actual retained color.");
                frame = frame with { State = frame.State.WriteDword(SavedColorsGlobal + (uint)actor * 4, color) };
            }
            frame = inputs.Execute(frame, new NativeBattleElectrocute275BindCaster(caster, address + 0x14, 0x10DA574));
            frame = inputs.Execute(frame, new NativeBattleElectrocute275RingSequence(caster, 1));
            frame = WriteArena(WriteArena(frame, working + WorldFreePointerOffset, BitConverter.GetBytes(0u)), working + BoneFreePointerOffset, BitConverter.GetBytes(0u));
        }
        int survivors = 0;
        if (Task(frame, 0, slot).ReadByte(15) != 0)
        {
            for (int poolIndex = 1; poolIndex < 6; poolIndex++)
            {
                if (poolIndex == 2)
                {
                    frame = inputs.Execute(frame, new NativeBattleElectrocute275LoadView());
                }
                if (poolIndex == 3)
                {
                    frame = inputs.Execute(frame, new NativeBattleElectrocute275ComposeCaster(caster));
                }
                int currentPool = poolIndex;
                NativeBattleElectrocute275Frame current = frame;
                NativeBattleEffectTaskStep scanned = frame.Effect.Pools[poolIndex].Advance((pool, childSlot) =>
                {
                    current = WithPool(current, currentPool, pool);
                    uint address = checked(pool.BankAddress + (uint)(childSlot * pool.Stride));
                    (current, int result) = currentPool switch
                    {
                        1 => Main(current, address, resources, inputs),
                        2 => AdvanceWorldTrail(current, address, resources, inputs),
                        3 => AdvanceBoneTrail(current, address, resources, inputs),
                        4 => Sprite(current, address, resources, inputs),
                        _ => Act(current, address, resources, inputs)
                    };
                    return new(current.Effect.Pools[currentPool], result);
                });
                frame = WithPool(current, poolIndex, scanned.Pool);
                survivors += scanned.Survivors;
                if (poolIndex == 3)
                {
                    frame = inputs.Execute(frame, new NativeBattleElectrocute275RestoreCaster(caster));
                }
            }
        }
        frame = inputs.Execute(frame, new NativeBattleElectrocute275Stage(unchecked((short)frame.State.ReadDword(FadeGlobal)), 0));
        if (Task(frame, 0, slot).ReadByte(15) != 0 && survivors == 0)
        {
            return (frame, 2);
        }
        return (Put(frame, 0, slot, Task(frame, 0, slot).WriteWord(12, unchecked((ushort)(Task(frame, 0, slot).ReadWord(12) + 1)))), 0);
    }
}
