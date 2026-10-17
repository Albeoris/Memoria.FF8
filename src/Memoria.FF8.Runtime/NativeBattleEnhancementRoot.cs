using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnhancementFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, int? DrawCursor);
public sealed record NativeBattleEnhancementChildStep(NativeBattleEnhancementFrame Frame, NativeCrtRandomState Random, int Result);
public sealed record NativeBattleEnhancementRootInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEnhancementFrame, uint, NativeCrtRandomState, NativeBattleEnhancementChildStep> AdvanceChild);
public sealed record NativeBattleEnhancementRootStep(NativeBattleEnhancementFrame Frame, NativeCrtRandomState Random);

/// <summary>The identical8D5180/89DFF0 root walk, with typed private callbacks supplied by each real family.</summary>
public static class NativeBattleEnhancementRoot
{
    public static NativeBattleEnhancementRootStep Advance(NativeBattleEnhancementFrame frame, NativeBattleEnhancementProfile profile, NativeBattleEnhancementRootInputs inputs, NativeCrtRandomState random)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, random);
        }
        if (frame.Effect.Binding.Entry != profile.Entry || frame.Effect.ReturnedDescriptor != profile.Pools[0].Descriptor || frame.Effect.Pools.Length != profile.Pools.Length || !frame.Effect.Pools.Select(pool => pool.DescriptorAddress).SequenceEqual(profile.Pools.Select(pool => pool.Descriptor)))
        {
            throw new InvalidDataException("Native enhancement root requires its original six physical descriptors.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native enhancement root lost its50AFC0 parameters.");
        NativeBattleEnhancementFrame current = frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyStep root = hierarchy.Advance(0, (state, address) =>
        {
            NativeBattleModulePoolTask task = state.Read(address);
            if (task.Callback != profile.RootCallback)
            {
                throw new InvalidDataException("Native enhancement root callback contradicts its explicit profile.");
            }
            current = current with { DrawCursor = (task.ReadByte(0x5C) & 1) != 0 ? 0 : profile.DrawBufferStride };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, Models(inputs), inputs.ReadPoseSource);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    task = Increment(task);
                    break;
                case 1:
                    state = state.Write(address, task.WriteByte(0x63, 1));
                    state = state.Append8DC540(1, profile.TargetCallback, profile.Pools[1].Stride, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target)).Hierarchy;
                    task = Increment(state.Read(address));
                    break;
                case 2:
                    if (task.ReadByte(0x63) == 0)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                        task = group < unchecked((short)task.ReadWord(0x58)) ? task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x29, 1) : Increment(task);
                    }
                    break;
                case 3:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        task = Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native enhancement root signed phase exceeds its five callbacks.");
            }
            state = state.Write(address, task);
            int survivors = 0;
            for (int pool = 1; pool < profile.Pools.Length; pool++)
            {
                NativeBattleEffectHierarchyStep children = state.Advance(pool, (childState, childAddress) =>
                {
                    current = current with { Effect = current.Effect with { Pools = childState.Pools } };
                    NativeBattleEnhancementChildStep child = inputs.AdvanceChild(current, childAddress, random);
                    current = child.Frame;
                    random = child.Random;
                    return new(new(current.Effect.Pools), child.Result);
                });
                state = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = state.Read(address).WriteWord(0x5E, (ushort)survivors);
            task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
            state = state.Write(address, task);
            bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
            return new(complete ? state.Complete8DC530(address) : state, complete ? 2 : 0);
        });
        current = current with { Effect = current.Effect with { Pools = root.Hierarchy.Pools, ReturnedDescriptor = root.Survivors == 0 ? 0u : profile.Pools[0].Descriptor } };
        return new(current, random);
    }

    private static NativeBattleGraphicsModel[] Models(NativeBattleEnhancementRootInputs inputs) => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
    private static NativeBattleModulePoolTask Increment(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
}
