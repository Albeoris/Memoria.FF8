using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDisolvingAcid132Controller
{
    public static NativeBattleDisolvingAcid132Frame Initialize(NativeBattleDisolvingAcid132Frame frame, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Disolving Acid requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Disolving Acid requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0)) };
        return frame with
        {
            State = frame.State.WriteDword(InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, NativeBattleEffectNodeMemory.ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)),
            Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) },
            Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }
        };
    }

    public static NativeBattleDisolvingAcid132Frame Advance(NativeBattleDisolvingAcid132Frame frame, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 6) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Disolving Acid requires its original root and five working pools.");
        }
        NativeBattleDisolvingAcid132Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Disolving Acid reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
        return current;
    }

    public static (NativeBattleDisolvingAcid132Frame Frame, int Result) AdvanceRoot(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint working = Working(frame);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadBytes(15, 1)[0] == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteBytes(15, [1]));
                NativeBattleEffectTaskPool main = ResetPool(frame, MainDescriptor, working + MainOffset, MainStride, MainCount);
                NativeBattleEffectTaskPool act = ResetPool(frame, ActDescriptor, working + ActOffset, ActStride, ActCount);
                NativeBattleEffectTaskPool simple = ResetPool(frame, SimpleSpriteDescriptor, working + SimpleSpriteOffset, SimpleSpriteStride, SimpleSpriteCount);
                NativeBattleEffectTaskPool curves = ResetPool(frame, CurveDescriptor, working + CurveOffset, CurveStride, CurveCount);
                NativeBattleEffectTaskPool sprites = ResetPool(frame, SpriteDescriptor, working + SpriteOffset, SpriteStride, SpriteCount);
                frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, act, simple, curves, sprites] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, act, simple, curves, sprites]) };
                for (int index = 0; index < PointCount; index++)
                {
                    frame = WriteArena(frame, checked(working + PointsOffset + (uint)(index * PointStride) + 6), BitConverter.GetBytes(ushort.MaxValue));
                }
            }
            byte contact = Read(frame, address).ReadBytes(14, 1)[0];
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            if (contact <= header.ReadByte(17))
            {
                int firstContact = header.FirstContactIndex ?? throw new NotSupportedException("Disolving Acid requires its captured contact pointer.");
                uint target = ModelAddress(inputs.Packets.ReadContact(firstContact + contact).Primary.ActorIndex);
                if (frame.Effect.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(16) == target))
                {
                    frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
                }
                else
                {
                    (frame, uint child) = Append(frame, 1, MainCallback);
                    if (child != 0)
                    {
                        frame = Write(frame, child, Read(frame, child).WriteDword(12, 0).WriteWord(14, contact).WriteDword(16, target));
                        frame = Write(frame, address, Read(frame, address).WriteBytes(14, [unchecked((byte)(contact + 1))]));
                    }
                }
            }
        }
        if (Read(frame, address).ReadBytes(15, 1)[0] != 0)
        {
            int survivors = 0;
            for (int index = 1; index < 5; index++)
            {
                int owner = index;
                NativeBattleDisolvingAcid132Frame current = frame;
                NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, owner, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when owner == 1 => AdvanceMain(current, child, resources, inputs),
                        PrimaryActCallback when owner == 2 => AdvancePrimaryAct(current, child, resources, inputs),
                        ImpactActCallback when owner == 2 => AdvanceImpactAct(current, child, resources, inputs),
                        CurveCallback when owner == 4 => NativeBattleDisolvingAcid132Trail.Advance6B2030(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Disolving Acid reached an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[owner], result);
                });
                frame = WithPool(current, owner, advanced.Pool);
                survivors += advanced.Survivors;
            }
            (frame, int sprites) = AdvanceSpriteScheduler(frame, resources, inputs);
            if (survivors == 0 && sprites == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }
}
