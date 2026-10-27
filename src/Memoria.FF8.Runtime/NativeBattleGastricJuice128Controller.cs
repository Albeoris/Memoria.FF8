using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGastricJuice128Controller
{
    public static NativeBattleGastricJuice128Frame Initialize(NativeBattleGastricJuice128Frame frame, NativeBattleGastricJuice128Resources resources, NativeBattleGastricJuice128Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        _ = FirstContact(frame, inputs);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Gastric Juice requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Gastric Juice requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        NativeBattleGastricJuice128State state = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleGastricJuice128Frame Advance(NativeBattleGastricJuice128Frame frame, NativeBattleGastricJuice128Resources resources, NativeBattleGastricJuice128Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Gastric Juice requires its original root and arena task pools.");
        }
        NativeBattleGastricJuice128Frame current = frame;
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Gastric Juice reached an unsupported root callback.");
            }
            (current, int result) = Root(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor } };
    }

    public static (NativeBattleGastricJuice128Frame Frame, int Result) Root(NativeBattleGastricJuice128Frame frame, int slot, NativeBattleGastricJuice128Resources resources, NativeBattleGastricJuice128Inputs inputs)
    {
        NativeBattleModulePoolTask root = Task(frame, 0, slot);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(Working(frame) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                frame = Put(frame, 0, slot, Task(frame, 0, slot).WriteByte(15, 1));
                uint working = Working(frame);
                NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + 0x1280, 616, 1);
                NativeBattleEffectTaskPool trails = NativeBattleEffectTaskPool.Initialize(CurveDescriptor, working + 0x1000, 80, 8);
                NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, working, 32, 128);
                frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], targets, trails, particles] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [targets, trails, particles]) };
                for (int index = 0; index < PointCount; index++)
                {
                    frame = WriteArena(frame, working + PointsOffset + (uint)(index * PointStride) + 6, BitConverter.GetBytes((ushort)0xFFFF));
                }
            }
            int ordinal = Task(frame, 0, slot).ReadByte(14);
            if (ordinal <= inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(17))
            {
                int actor = inputs.Packets.ReadContact(FirstContact(frame, inputs) + ordinal).Primary.ActorIndex;
                (frame, uint address) = Append(frame, 1, MainCallback);
                if (address == 0)
                {
                    throw new NotSupportedException("Gastric Juice reached a full original target task bank.");
                }
                NativeBattleModulePoolTask target = Read(frame, address).WriteBytes(12, new byte[604]).WriteWord(14, (ushort)ordinal).WriteDword(16, ModelAddress(actor));
                NativeBattleGastricJuice128AnchorStep anchor = inputs.ReadAnchor571400(frame, actor);
                frame = anchor.Frame;
                target = target.WriteWord(0x16, unchecked((ushort)anchor.Anchor.X)).WriteWord(0x18, unchecked((ushort)anchor.Anchor.Y)).WriteWord(0x1A, unchecked((ushort)anchor.Anchor.Z)).WriteWord(0x1C, unchecked((ushort)anchor.Anchor.W));
                NativeBattleGastricJuice128AnchorStep caster = inputs.ReadAnchor571400(frame, Actor(frame.State.ReadDword(CasterGlobal)));
                frame = caster.Frame;
                int angle = NativePrecision53.RoundInteger(Math.Atan2(anchor.Anchor.X - caster.Anchor.X, anchor.Anchor.Z - caster.Anchor.Z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
                target = target.WriteWord(0x14, unchecked((ushort)angle));
                NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(target.Payload.Slice(0x20 - 12, 0x248)).Initialize7016B0(resources.MainAct, 0x248);
                frame = Write(frame, address, target.WriteBytes(0x20, workspace.Data.AsSpan()));
                frame = Put(frame, 0, slot, Task(frame, 0, slot).WriteByte(14, unchecked((byte)(ordinal + 1))));
                frame = inputs.Execute(frame, new NativeBattleGastricJuice128SnapshotBody((byte)actor, Working(frame) + BodyCacheOffset));
            }

        }
        if (Task(frame, 0, slot).ReadByte(15) != 0)
        {
            frame = NormalizeView(frame, inputs.ReadView(frame));
            int survivors = 0;
            for (int poolIndex = 1; poolIndex < 4; poolIndex++)
            {
                int currentPool = poolIndex;
                NativeBattleGastricJuice128Frame current = frame;
                NativeBattleEffectTaskStep scanned = frame.Effect.Pools[poolIndex].Advance((pool, taskSlot) =>
                {
                    current = WithPool(current, currentPool, pool);
                    uint address = checked(pool.BankAddress + (uint)(taskSlot * pool.Stride));
                    (current, int result) = currentPool switch
                    {
                        1 => AdvanceMain(current, address, resources, inputs),
                        2 => NativeBattleGastricJuice128Trail.Advance6B8DC0(current, address, resources, inputs),
                        _ => AdvanceParticle(current, address, inputs)
                    };
                    return new(current.Effect.Pools[currentPool], result);
                });
                frame = WithPool(current, poolIndex, scanned.Pool);
                survivors += scanned.Survivors;
            }
            if (survivors == 0)
            {
                return (frame, 2);
            }
        }
        ushort next = unchecked((ushort)(Task(frame, 0, slot).ReadWord(12) + 1));
        if (unchecked((short)next) >= 10)
        {
            next = 0;
        }
        return (Put(frame, 0, slot, Task(frame, 0, slot).WriteWord(12, next)), 0);
    }

    private static NativeBattleGastricJuice128Frame NormalizeView(NativeBattleGastricJuice128Frame frame, NativeBattlePackedMatrix32 packed)
    {
        NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(packed.ReadMatrix());
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!packed.Known[index])
            {
                view = view.MarkUnknown(index, 1);
            }
        }
        for (int index = 0; index < 9; index++)
        {
            view = view.Write(index * 2, BitConverter.GetBytes(normalized.Rotation[index]));
        }
        return frame with { State = frame.State.WriteView(CurrentMatrixAddress, view, 32) };
    }
}
