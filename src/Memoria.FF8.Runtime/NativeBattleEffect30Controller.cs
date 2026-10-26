using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect30Controller
{
    public static NativeBattleEffect30Frame Initialize(NativeBattleEffect30Frame frame, NativeBattleEffect30Resources resources, NativeBattleEffect30Inputs inputs)
    {
        if (frame.Effect.Binding.Effect != 30 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Effect30 requires its original entry binding.");
        }
        resources.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect30 requires its captured action parameters.");
        int source = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect30 requires its executed texture initializer.");
        _ = ArenaOffset(checked(ArenaAddress + (uint)source), 2 * DrawStep + EmitterBytes + GeometryBytes);
        NativeBattleCureFamilyPool owner = Pools[0];
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(owner.Descriptor, owner.Bank, owner.Stride, owner.Capacity)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, RootCallback, 100);
        NativeBattleModulePoolTask root = NativeBattleEffectRootTask.Initialize(appended.Hierarchy.Read(appended.Address), parameters, inputs.Packets);
        frame = frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Write(appended.Address, root).Pools, ReturnedDescriptor = owner.Descriptor, DrawArenaOffset = null }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)source)).WriteDword(0x26D6A30, 0).WriteDword(0x26D476C, 0) };
        if ((parameters.Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect30RegisterCamera(resources.Camera));
            frame = inputs.Execute(frame, new NativeBattleEffect30EnqueueTim(source));
        }
        uint start = checked(ArenaAddress + (uint)source);
        return frame with { State = frame.State.WriteDword(0x26D6A0C, start).WriteDword(0x26D5E04, start + DrawStep).WriteDword(0x26D6A08, start + DrawStep).WriteDword(0x26D6A34, start + 2 * DrawStep).WriteDword(0x26D5E00, start + 2 * DrawStep), Effect = frame.Effect with { Pools = frame.Effect.Pools.AddRange(Pools.Skip(1).Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity))) } };
    }

    public static NativeBattleEffect30Frame Advance(NativeBattleEffect30Frame frame, NativeBattleEffect30Resources resources, NativeBattleEffect30Inputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleEffect30Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(0, pool) } };
            NativeBattleEffect30TaskStep step = Advance(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            current = step.Frame;
            return new(current.Effect.Pools[0], step.Result);
        });
        return current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(0, roots.Pool), ReturnedDescriptor = roots.Survivors == 0 ? 0u : Pools[0].Descriptor } };
    }

    public static NativeBattleEffect30TaskStep Advance(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        TargetCallback => AdvanceTarget(frame, address, resources, inputs),
        NodeCallback => inputs.AdvanceNodes(frame, address),
        BeamCallback => AdvanceBeam(frame, address, inputs),
        ParticleCallback => inputs.AdvanceParticles96(frame, address),
        FadeInCallback or FadeOutCallback => AdvanceStage(frame, address, inputs),
        _ => throw new NotSupportedException("Effect30 has an unsupported hierarchical callback replacement.")
    };

    private static NativeBattleEffect30TaskStep AdvanceRoot(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30Inputs inputs)
    {
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadCamera(frame) }, State = frame.State.WriteDword(0x26D6A28, 0x2793E58).WriteDword(0x26D40DC, 0x2793E58) };
        NativeBattleModulePoolTask task = Read(frame, address);
        bool odd = (task.ReadByte(0x5C) & 1) != 0;
        uint drawStart = frame.State.ReadDword(odd ? 0x26D6A0Cu : 0x26D6A08u);
        frame = frame with { State = frame.State.WriteDword(0x26D40BC, drawStart).WriteDword(0x26D40D8, frame.State.ReadDword(odd ? 0x26D5E04u : 0x26D5E00u)), Effect = frame.Effect with { DrawArenaOffset = ArenaOffset(drawStart, 0) } };
        frame = inputs.RefreshRoot(frame, address);
        task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
            case 1:
            case 7:
            case 8:
                frame = NextPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint emitters = frame.State.ReadDword(0x26D6A34);
                    uint geometry = checked(emitters + EmitterBytes);
                    NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect30 requires its allocated effect arena.");
                    arena = arena.Write(ArenaOffset(emitters, EmitterBytes), new byte[EmitterBytes]).Write(ArenaOffset(geometry, GeometryBytes), new byte[GeometryBytes]);
                    frame = frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State.WriteDword(0x26D6A14, emitters).WriteDword(0x26D6A2C, geometry).WriteDword(0x26D6A34, checked(geometry + GeometryBytes)).WriteWord(0x26D4768, 0).WriteWord(0x26D6A20, 0).WriteWord(0x26D6A10, 0).WriteWord(0x26D40C0, 0) };
                    (frame, _) = Append(frame, 5, FadeInCallback, address, inputs);
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = Append(frame, 1, TargetCallback, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)task.ReadByte(0x2A)) < Signed(task, 0x58))
                    {
                        task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                        frame = Write(frame, address, task);
                    }
                    else
                    {
                        frame = NextPhase(Write(frame, address, task.WriteWord(0x60, 20)), address);
                    }
                }
                break;
            case 5:
                short remaining = unchecked((short)(Signed(task, 0x60) - 1));
                frame = Write(frame, address, task.WriteWord(0x60, unchecked((ushort)remaining)));
                if (remaining <= 0)
                {
                    (frame, _) = Append(frame, 5, FadeOutCallback, address, inputs);
                    frame = NextPhase(frame, address);
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                frame = NextPhase(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect30 root phase leaves its original eleven-entry table.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x26D6A22, 0).WriteWord(0x26D6A12, 0) };
        for (int pool = 1; pool < Pools.Length; pool++)
        {
            NativeBattleEffectTaskStep children = frame.Effect.Pools[pool].Advance((owner, slot) =>
            {
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(pool, owner) } };
                NativeBattleEffect30TaskStep child = Advance(frame, checked(owner.BankAddress + (uint)(slot * owner.Stride)), resources, inputs);
                frame = child.Frame;
                return new(frame.Effect.Pools[pool], child.Result);
            });
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(pool, children.Pool) } };
            frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, unchecked((ushort)(Read(frame, address).ReadWord(0x5E) + children.Survivors))));
        }
        task = Read(frame, address);
        return Finish(Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1)))), address);
    }

    private static NativeBattleEffect30TaskStep AdvanceTarget(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30Inputs inputs)
    {
        frame = inputs.PrepareTarget(frame, address);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                (frame, uint nodes) = Append(frame, 2, NodeCallback, address, inputs);
                frame = Write(frame, nodes, Read(frame, nodes).WriteDword(0x30, SourceAddress).WriteWord(0x298, 0).WriteWord(0x29E, 45).WriteWord(0x29C, 0));
                (frame, _) = Append(frame, 3, BeamCallback, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (task.ReadWord(0x24) == 10)
                {
                    uint root = task.ReadDword(0x10);
                    frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (Signed(task, 0x24) >= 10)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect30Contact(address));
                    task = Read(frame, address);
                    frame = NextPhase(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Effect30 target phase leaves its original four-entry table.");
        }
        if (Read(frame, address).ReadWord(0x24) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect30Sound(resources.Sound, 0, 128));
        }
        return Finish(frame, address);
    }
}
