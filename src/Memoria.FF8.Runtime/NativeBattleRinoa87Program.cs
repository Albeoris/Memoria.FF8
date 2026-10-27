using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87ProgramNode(NativeBattleActionMatrix Context, NativeBattleGfAnimationNode Node, NativeBattleModulePoolTask Task, uint MorphScratch);
public sealed record NativeBattleRinoa87ProgramInputs(Func<uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleRinoa87Frame, uint, byte, NativeBattleVector> ReadBodyBone502170, Func<NativeBattleRinoa87Frame, byte, short> ReadTargetScale38, Func<NativeBattleRinoa87Frame, NativeBattleGfAnimationSource, NativeBattleRinoa87ProgramNode, NativeBattleRinoa87Frame> Draw7766B0, NativeBattleActionMatrix View);

/// <summary>7764B0/77E6C0's embedded701970 workspaces and776580 context construction.</summary>
public static class NativeBattleRinoa87Program
{
    public static NativeBattleRinoa87LeafStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87ProgramInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback is not (0x7764B0 or 0x77E6C0))
        {
            throw new NotSupportedException("Native Rinoa87 programme requires one of its original604-byte wrappers.");
        }
        NativeBattleGfAnimationSource source = inputs.ReadSource(task.ReadDword(116));
        if (source.Address != task.ReadDword(116))
        {
            throw new InvalidDataException("Native776580 programme source changed physical owner.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                NativeBattleGfAnimationWorkspace workspace = Workspace(task).Initialize7016B0(source, unchecked((int)task.ReadDword(120)));
                task = StoreWorkspace(task, workspace);
                NativeBattleVector anchor = inputs.ReadBodyBone502170(frame, task.ReadDword(576), 241);
                task = task.WriteWord(28, unchecked((ushort)anchor.X)).WriteWord(30, 0).WriteWord(32, unchecked((ushort)anchor.Z));
                int scale = task.Callback == 0x7764B0 ? task.ReadWord(138) == 0 ? 3072 : 4096 : Scale77E780(inputs.ReadTargetScale38(frame, task.ReadByte(45)));
                task = task.WriteDword(80, unchecked((uint)scale)).WriteDword(84, unchecked((uint)scale)).WriteDword(88, unchecked((uint)scale));
                Draw();
                Next();
                break;
            case 1:
                if (Draw() == 0)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
                    Next();
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Rinoa87 programme signed phase exceeds its three callbacks.");
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void Store() => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        int Draw()
        {
            NativeBattleActionMatrix context = Compose776580(task, inputs.View);
            NativeBattleModulePoolTask captured = task;
            Store();
            NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, frame.State.Counter25AECD4, (current, node) =>
            {
                frame = inputs.Draw7766B0(frame, source, new(context, node, captured, 0x25B46A8));
                return current;
            });
            task = StoreWorkspace(Read(), advanced.Workspace);
            return advanced.Remaining;
        }
    }

    public static short Scale77E780(short targetScale)
    {
        if (targetScale < 2048)
        {
            return 4096;
        }
        int scale = (targetScale - 2048) / 2 + 4096;
        return unchecked((short)scale) > 8192 ? (short)8192 : unchecked((short)scale);
    }

    public static NativeBattleActionMatrix Compose776580(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, unchecked((short)task.ReadWord(98)));
        local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, unchecked((short)task.ReadWord(96)));
        local = NativeBattleCuragaMatrices.RotateZ8DD960(local, unchecked((short)task.ReadWord(100)));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(80)), unchecked((int)task.ReadDword(84)), unchecked((int)task.ReadDword(88)));
        local = local with { X = unchecked((short)task.ReadWord(28)), Y = unchecked((short)task.ReadWord(30)), Z = unchecked((short)task.ReadWord(32)) };
        return NativeBattlePoseMatrices.Compose56C2F0(view, local);
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task) => new(task.ReadRawPayloadSpan().Slice(148 - 12).ToArray().ToImmutableArray());
    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace) => workspace.CopyTo(task, 148, retainUnknown: false);
}
