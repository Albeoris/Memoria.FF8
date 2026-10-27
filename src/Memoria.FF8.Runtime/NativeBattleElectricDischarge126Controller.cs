using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectricDischarge126Controller
{
    public static NativeBattleElectricDischarge126Frame Initialize(NativeBattleElectricDischarge126Frame frame, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Electric Discharge requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Electric Discharge requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        NativeBattleElectricDischarge126State state = frame.State.SeedColdSources(resources).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        for (int actor = 0; actor < 3; actor++)
        {
            state = state.WriteDword(SavedColorsGlobal + (uint)(actor * 4), inputs.ReadModel(actor).Color40 ?? throw new NotSupportedException("Electric Discharge requires all three actual retained party colors."));
        }
        return frame with { State = state, Memory = (frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }).SeedSharedEffectRandom7059D0(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleElectricDischarge126Frame Advance(NativeBattleElectricDischarge126Frame frame, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 6) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Electric Discharge requires its actual root and five working pools.");
        }
        NativeBattleElectricDischarge126Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Electric Discharge reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleElectricDischarge126Frame Frame, int Result) AdvanceRoot(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadDword(16) != 0;
        uint working = Working(frame);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))).WriteDword(FadeGlobal, 0) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        for (byte actor = 0; actor < 3; actor++)
        {
            frame = inputs.Execute(frame, new NativeBattleElectricDischarge126ActorFlags(actor, 0x800, 0));
        }
        byte caster = checked((byte)Actor(frame.State.ReadDword(CasterGlobal)));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool world = NativeBattleEffectTaskPool.Initialize(WorldTrailDescriptor, working + WorldOffset, WorldStride, WorldCount);
            NativeBattleEffectTaskPool bone = NativeBattleEffectTaskPool.Initialize(BoneTrailDescriptor, working + BoneOffset, BoneStride, BoneCount);
            NativeBattleEffectTaskPool sprite = NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, working + SpriteOffset, SpriteStride, SpriteCount);
            NativeBattleEffectTaskPool act = NativeBattleEffectTaskPool.Initialize(ActDescriptor, working, ActStride, ActCount);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, world, bone, sprite, act] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, world, bone, sprite, act]) };
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = WriteArena(WriteArena(frame, working + BonePointsOffset, new byte[0x5000]), working + WorldPointsOffset, new byte[0x5000]);
            for (int index = 0; index < PointCount; index++)
            {
                frame = PointWord(frame, working + BonePointsOffset + (uint)(index * PointStride), -1);
                frame = PointWord(frame, working + WorldPointsOffset + (uint)(index * PointStride) + 6, -1);
            }
            (frame, uint mainAddress) = Append(frame, 1, MainCallback);
            if (mainAddress == 0)
            {
                throw new NotSupportedException("Electric Discharge exhausted its actual single main task.");
            }
            frame = Write(frame, mainAddress, Read(frame, mainAddress).WriteBytes(12, new byte[8]));
            frame = WriteArena(WriteArena(frame, working + WorldFreePointerOffset, BitConverter.GetBytes(0u)), working + BoneFreePointerOffset, BitConverter.GetBytes(0u));
        }
        int survivors = 0;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            for (int index = 1; index < 6; index++)
            {
                if (index == 2)
                {
                    frame = inputs.Execute(frame, new NativeBattleElectricDischarge126LoadView());
                }
                if (index == 3)
                {
                    frame = inputs.Execute(frame, new NativeBattleElectricDischarge126ComposeCaster(caster));
                }
                int ownerIndex = index;
                NativeBattleElectricDischarge126Frame current = frame;
                NativeBattleEffectTaskStep children = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, ownerIndex, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when ownerIndex == 1 => AdvanceMain(current, child, resources, inputs),
                        WorldTrailCallback when ownerIndex == 2 => AdvanceWorldTrail(current, child, resources, inputs),
                        BoneTrailCallback when ownerIndex == 3 => AdvanceBoneTrail(current, child, resources, inputs),
                        SpriteCallback when ownerIndex == 4 => AdvanceSprite(current, child, resources, inputs),
                        ActCallback when ownerIndex == 5 => AdvanceAct(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Electric Discharge reached an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[ownerIndex], result);
                });
                frame = WithPool(current, index, children.Pool);
                survivors = checked(survivors + children.Survivors);
                if (index == 3)
                {
                    frame = inputs.Execute(frame, new NativeBattleElectricDischarge126RestoreCaster(caster));
                }
            }
        }
        frame = inputs.Execute(frame, new NativeBattleElectricDischarge126Stage(unchecked((short)frame.State.ReadDword(FadeGlobal)), 0));
        if (Read(frame, address).ReadByte(15) != 0 && survivors == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    private static void RequireBinding(NativeBattleElectricDischarge126Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Electric Discharge requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleActionHeaderSlot Header(NativeBattleElectricDischarge126Frame frame, NativeBattleElectricDischarge126Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
}
