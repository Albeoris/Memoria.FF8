using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static NativeBattleSaliva138Frame Initialize(NativeBattleSaliva138Frame frame, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame = frame with { State = frame.State.SeedColdSources(resources) };
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Saliva requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Saliva requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        return frame with
        {
            State = frame.State.WriteDword(InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, NativeBattleEffectNodeMemory.ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)),
            Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) },
            Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }
        };
    }

    public static NativeBattleSaliva138Frame Advance(NativeBattleSaliva138Frame frame, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Saliva requires its original root and four working pools.");
        }
        NativeBattleSaliva138Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Saliva reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleSaliva138Frame Frame, int Result) AdvanceRoot(NativeBattleSaliva138Frame frame, uint address, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint working = Working(frame);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1)
        {
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 1));
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool curves = NativeBattleEffectTaskPool.Initialize(CurveDescriptor, working + CurveOffset, CurveStride, CurveCount);
            NativeBattleEffectTaskPool moving = NativeBattleEffectTaskPool.Initialize(MovingSpriteDescriptor, working + MovingSpriteOffset, MovingSpriteStride, MovingSpriteCount);
            NativeBattleEffectTaskPool simple = NativeBattleEffectTaskPool.Initialize(SimpleSpriteDescriptor, working, SimpleSpriteStride, SimpleSpriteCount);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, curves, moving, simple] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, curves, moving, simple]) };
            (frame, uint child) = Append(frame, 1, MainCallback);
            if (child == 0)
            {
                throw new NotSupportedException("Saliva exhausted its single main task.");
            }
            uint target = ModelAddress(inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.ActorIndex);
            NativeBattleModulePoolTask task = Read(frame, child).WriteBytes(12, new byte[884]).WriteDword(16, target);
            NativeBattleSaliva138PointStep anchor = inputs.ReadBone502170(frame, target, 0xF1, 0);
            frame = anchor.Frame;
            task = Vector(task, 28, anchor.Point);
            task = InitializeWorkspace(task, 0x2C, 0xA8, resources.MainAct);
            task = InitializeWorkspace(task, 0xD4, 0x194, resources.SecondaryAct);
            task = InitializeWorkspace(task, 0x268, 0x118, resources.ImpactAct);
            frame = Write(frame, child, task);
            frame = frame with { State = frame.State.WriteMesh(Preprocess6AD430(frame.State.ReadMesh())) };
            for (int index = 0; index < PointCount; index++)
            {
                frame = WriteArena(frame, checked(working + PointsOffset + (uint)(index * PointStride) + 6), BitConverter.GetBytes(ushort.MaxValue));
            }
        }
        if (Read(frame, address).ReadWord(14) != 0)
        {
            int mainSurvivors = 0;
            int simpleSurvivors = 0;
            for (int index = 1; index < 5; index++)
            {
                int owner = index;
                NativeBattleSaliva138Frame current = frame;
                NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, owner, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when owner == 1 => AdvanceMain(current, child, resources, inputs),
                        CurveCallback when owner == 2 => AdvanceCurve(current, child, resources, inputs),
                        MovingSpriteCallback when owner == 3 => AdvanceMovingSprite(current, child, resources, inputs),
                        SimpleSpriteCallback when owner == 4 => AdvanceSimpleSprite(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Saliva reached an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[owner], result);
                });
                frame = WithPool(current, owner, advanced.Pool);
                if (owner == 1)
                {
                    mainSurvivors = advanced.Survivors;
                }
                if (owner == 4)
                {
                    simpleSurvivors = advanced.Survivors;
                }
            }
            if (mainSurvivors == 0 && simpleSurvivors == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }
}
