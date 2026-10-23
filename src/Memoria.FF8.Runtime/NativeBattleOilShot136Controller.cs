using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilShot136Controller
{
    public static NativeBattleOilShot136Frame Initialize(NativeBattleOilShot136Frame frame, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame = frame with { State = frame.State.SeedColdSources(resources) };
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Oil Shot requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Oil Shot requires its actual texture queue.");
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

    public static NativeBattleOilShot136Frame Advance(NativeBattleOilShot136Frame frame, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 6) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Oil Shot requires its original root and five working pools.");
        }
        NativeBattleOilShot136Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Oil Shot reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, roots.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
        if (roots.Survivors == 0 && current.Effect.Pools.Length == 6)
        {
            foreach (uint descriptor in new[] { MovingSpriteDescriptor, EmitterDescriptor, SimpleSpriteDescriptor, LargeParticleDescriptor })
            {
                NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(current.Memory, current.Effect, descriptor);
                current = current with { Memory = transfer.Memory, Effect = transfer.Effect };
            }
        }
        return current;
    }

    public static (NativeBattleOilShot136Frame Frame, int Result) AdvanceRoot(NativeBattleOilShot136Frame frame, uint address, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint working = Working(frame);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1)
        {
            frame = Write(frame, address, Read(frame, address).WriteWord(14, 1));
            NativeBattleEffectTaskPool main = ResetPool(frame, MainDescriptor, working + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool moving = ResetPool(frame, MovingSpriteDescriptor, working + MovingSpriteOffset, MovingSpriteStride, MovingSpriteCount);
            NativeBattleEffectTaskPool emitter = ResetPool(frame, EmitterDescriptor, working + EmitterOffset, EmitterStride, EmitterCount);
            NativeBattleEffectTaskPool simple = ResetPool(frame, SimpleSpriteDescriptor, working + SimpleSpriteOffset, SimpleSpriteStride, SimpleSpriteCount);
            NativeBattleEffectTaskPool large = ResetPool(frame, LargeParticleDescriptor, working + LargeParticleOffset, LargeParticleStride, LargeParticleCount);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, moving, emitter, simple, large] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, moving, emitter, simple, large]) };
            (frame, uint child) = Append(frame, 1, MainCallback);
            if (child == 0)
            {
                throw new NotSupportedException("Oil Shot exhausted its single main task.");
            }
            uint target = ModelAddress(inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.ActorIndex);
            NativeBattleModulePoolTask task = Read(frame, child).WriteBytes(12, new byte[1120]).WriteDword(16, target);
            NativeBattleOilShot136AnchorStep anchor = inputs.ReadAnchor571400(frame, target);
            frame = anchor.Frame;
            task = Vector(task, 28, new(anchor.Anchor.X, anchor.Anchor.Y, anchor.Anchor.Z)).WriteWord(34, unchecked((ushort)anchor.Anchor.W));
            task = InitializeWorkspace(task, 0x2C, 0xC8, resources.MainAct);
            task = InitializeWorkspace(task, 0xF4, 0x1F4, resources.SecondaryAct);
            task = InitializeWorkspace(task, 0x2E8, 0x184, resources.ImpactAct);
            frame = Write(frame, child, task);
            frame = frame with { State = frame.State.WriteMesh(NativeBattleOilShot136Mesh.Preprocess6AD430(frame.State.ReadMesh())) };
        }
        if (Read(frame, address).ReadWord(14) != 0)
        {
            int mainSurvivors = 0;
            for (int index = 1; index < 6; index++)
            {
                int owner = index;
                NativeBattleOilShot136Frame current = frame;
                NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, slot) =>
                {
                    current = WithPool(current, owner, pool);
                    uint child = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                    (current, int result) = pool.Slots[slot].Callback switch
                    {
                        MainCallback when owner == 1 => AdvanceMain(current, child, resources, inputs),
                        MovingSpriteCallback when owner == 2 => AdvanceMovingSprite(current, child, resources, inputs),
                        EmitterCallback when owner == 3 => AdvanceEmitter(current, child, resources, inputs),
                        SimpleSpriteCallback when owner == 4 => AdvanceSimpleSprite(current, child, resources, inputs),
                        LargeParticleCallback when owner == 5 => AdvanceLargeParticle(current, child, resources, inputs),
                        _ => throw new NotSupportedException("Oil Shot reached an unsupported child callback.")
                    };
                    return new(current.Effect.Pools[owner], result);
                });
                frame = WithPool(current, owner, advanced.Pool);
                if (owner == 1)
                {
                    mainSurvivors = advanced.Survivors;
                }
            }
            if (mainSurvivors == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }
}
