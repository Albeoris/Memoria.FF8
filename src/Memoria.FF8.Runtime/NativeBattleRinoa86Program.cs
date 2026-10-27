using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86ProgramNode(NativeBattleActionMatrix Context, NativeBattleGfAnimationNode Node, short Mode, short ScrollU, short DepthBias, uint MorphScratch);
public sealed record NativeBattleRinoa86ProgramInputs(Func<uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleRinoa86Frame, NativeBattleGfAnimationSource, NativeBattleRinoa86ProgramNode, NativeBattleRinoa86Frame> Draw7838C0, NativeBattleActionMatrix View);

/// <summary>7836E0/783C90/783F80's embedded701970 workspaces and original783790 callback context.</summary>
public static class NativeBattleRinoa86Program
{
    public static NativeBattleRinoa86LeafStep Advance(NativeBattleRinoa86Frame frame, uint address, NativeBattleRinoa86ProgramInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback is not (0x7836E0 or 0x783C90 or 0x783F80))
        {
            throw new NotSupportedException("Native Rinoa86 programme requires one of its three original wrappers.");
        }
        NativeBattleGfAnimationSource source = inputs.ReadSource(task.ReadDword(116));
        if (source.Address != task.ReadDword(116))
        {
            throw new InvalidDataException("Native783790 programme source changed physical owner.");
        }
        int phase = unchecked((sbyte)task.ReadByte(41));
        switch (phase)
        {
            case 0:
                NativeBattleGfAnimationWorkspace workspace = Workspace(task).Initialize7016B0(source, unchecked((int)task.ReadDword(120)));
                task = StoreWorkspace(task, workspace).WriteDword(80, 4096).WriteDword(84, 4096).WriteDword(88, 4096);
                if (task.Callback == 0x7836E0)
                {
                    task = task.WriteWord(136, unchecked((ushort)-256));
                }
                else if (task.Callback == 0x783C90)
                {
                    task = task.WriteWord(136, unchecked((ushort)-512));
                }
                else
                {
                    NativeBattleRinoa86SceneMemory scene = NativeBattleRinoa86Scene.Read(frame);
                    task = task.WriteDword(28, (uint)(scene.Word(8) | scene.Word(10) << 16)).WriteDword(32, (uint)(scene.Word(12) | scene.Word(14) << 16));
                }
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
                throw new InvalidDataException("Native Rinoa86 programme signed phase exceeds its three callbacks.");
        }
        if (task.Callback == 0x783F80)
        {
            task = task.WriteWord(140, (ushort)((task.ReadByte(140) + 16) & 127));
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
            NativeBattleActionMatrix context = Compose783790(task, inputs.View);
            short mode = unchecked((short)task.ReadWord(128));
            short scroll = unchecked((short)task.ReadWord(140));
            short palette = unchecked((short)task.ReadWord(136));
            Store();
            NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, frame.State.Counter25B7080, (current, node) =>
            {
                frame = inputs.Draw7838C0(frame, source, new(context, node, mode, scroll, palette, 0x25BBF60));
                return current;
            });
            task = StoreWorkspace(Read(), advanced.Workspace);
            return advanced.Remaining;
        }
    }

    public static NativeBattleActionMatrix Compose783790(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, unchecked((short)task.ReadWord(98)));
        local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, unchecked((short)task.ReadWord(96)));
        local = NativeBattleCuragaMatrices.RotateZ8DD960(local, unchecked((short)task.ReadWord(100)));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(80)), unchecked((int)task.ReadDword(84)), unchecked((int)task.ReadDword(88)));
        local = local with { X = unchecked((short)task.ReadWord(28)), Y = unchecked((short)task.ReadWord(30)), Z = unchecked((short)task.ReadWord(32)) };
        return NativeBattlePoseMatrices.Compose56C2F0(view, local);
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task)
    {
        return NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (148 - 12), task.PayloadLength - (148 - 12));
    }

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 148, workspace);
    }
}
