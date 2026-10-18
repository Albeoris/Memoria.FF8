using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanState([property: JsonRequired] int? CurrentDrawOffset);
public sealed record NativeBattleScanFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleScanState State);
public sealed record NativeBattleScanChildStep(NativeBattleScanFrame Frame, int Result);
public sealed record NativeBattleScanControllerInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleScanFrame, NativeBattleScanFrame> Begin50A730, Func<NativeBattleScanFrame, int, NativeBattleScanFrame> SetMode4A8480);

/// <summary>84D030/84D110's Scan entry and root scheduling; child callbacks and their completion remain caller-owned.</summary>
public static class NativeBattleScanController
{
    public static NativeBattleScanFrame Initialize84D030(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native84D030 requires50AFC0 parameters.");
        if (effect.Binding.Effect != 40 || effect.Binding.Entry != 0x84D030)
        {
            throw new InvalidDataException("Native Scan requires its original40/84D030 binding.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x269ABA8, 0x269AAE0, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x269CE50, 0x269CE60, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x269CFC8, 0x269CFD8, 0x78, 20),
            NativeBattleEffectTaskPool.Initialize(0x269CE18, 0x269ABB8, 0x58, 100),
            NativeBattleEffectTaskPool.Initialize(0x269AA68, 0x269A1F8, 0x6C, 20)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x84D110, 0x64);
        hierarchy = root.Hierarchy;
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, 0, 0);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x2D, actors.Target).WriteWord(0x5A, header.ReadByte(0x10)).WriteWord(0x58, header.ReadByte(0x11));
        hierarchy = hierarchy.Write(root.Address, task);
        if ((parameters.Flags79 & 1) == 0)
        {
            int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x269A160)?.ArenaOffset ?? throw new NotSupportedException("Native84D030 requires its original269A160 initializer publication.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native84D030 requires its retained505E30 texture owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native84D030 requires its loaded TIM arena."), source);
            memory = memory with { Textures = textures };
        }
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x269ABA8 }, new(null));
    }

    public static NativeBattleScanFrame Advance84D110(NativeBattleScanFrame frame, NativeBattleScanControllerInputs inputs, Func<NativeBattleScanFrame, uint, NativeBattleScanChildStep> advanceChild)
    {
        frame.Effect.Validate();
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        RequireController(frame.Effect);
        inputs.Packets.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters!;
        NativeBattleScanFrame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep main = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != 0x84D110)
            {
                throw new NotSupportedException("Native Scan root requires its84D110 callback.");
            }
            //571B50 returns the fixed effect-arena base, independently of571B80's allocation cursor.
            current = current with { State = current.State with { CurrentDrawOffset = (task.ReadWord(0x5C) & 1) != 0 ? 0 : 0x4000 } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            state = state.Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    state = IncrementPhase(state, address);
                    break;
                case 1:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, 0x84D230, 0x58, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target)).Hierarchy;
                    current = current with { Effect = current.Effect with { Pools = state.Pools } };
                    current = inputs.Begin50A730(current);
                    current = inputs.SetMode4A8480(current, 0);
                    state = IncrementPhase(new(current.Effect.Pools), address);
                    break;
                case 2:
                    if (task.ReadByte(0x63) == 0)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                        task = group < unchecked((short)task.ReadWord(0x58))
                            ? task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)))
                            : task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                        state = state.Write(address, task);
                    }
                    break;
                case 3:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        current = current with { Effect = current.Effect with { Pools = state.Pools } };
                        current = inputs.SetMode4A8480(current, 1);
                        state = new(current.Effect.Pools);
                        task = state.Read(address);
                        state = state.Write(address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                        state = IncrementPhase(state, address);
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native84D110 signed phase exceeds its five original callbacks.");
            }
            int survivors = 0;
            for (int pool = 1; pool < 5; pool++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleScanChildStep child = advanceChild(current, childAddress);
                    current = child.Frame;
                    return new(new(current.Effect.Pools), child.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = state.Read(address);
            task = task.WriteWord(0x5E, (ushort)survivors).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        return current with { Effect = current.Effect with { Pools = main.Hierarchy.Pools, ReturnedDescriptor = main.Survivors == 0 ? 0u : 0x269ABA8u } };
    }

    private static NativeBattleEffectHierarchy IncrementPhase(NativeBattleEffectHierarchy hierarchy, uint address)
    {
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        return hierarchy.Write(address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static void RequireController(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding.Effect != 40 || effect.Binding.Entry != 0x84D030 || effect.Parameters is null || effect.ReturnedDescriptor != 0x269ABA8 || effect.Pools.Length != 5)
        {
            throw new InvalidDataException("Native Scan requires its executed84D030 entry and five original task banks.");
        }
        (uint Descriptor, uint Bank, ushort Stride, int Capacity)[] banks = [(0x269ABA8, 0x269AAE0, 0x64, 2), (0x269CE50, 0x269CE60, 0x58, 4), (0x269CFC8, 0x269CFD8, 0x78, 20), (0x269CE18, 0x269ABB8, 0x58, 100), (0x269AA68, 0x269A1F8, 0x6C, 20)];
        for (int index = 0; index < banks.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.DescriptorAddress != banks[index].Descriptor || pool.BankAddress != banks[index].Bank || pool.Stride != banks[index].Stride || pool.Slots.Length != banks[index].Capacity)
            {
                throw new InvalidDataException("Native Scan retained task bank differs from its84D030 descriptor, address, stride or capacity.");
            }
        }
        new NativeBattleEffectHierarchy(effect.Pools).Validate();
    }
}
