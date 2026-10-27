using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88ProgramNode(NativeBattleActionMatrix Context, NativeBattleGfAnimationNode Node, NativeBattleModulePoolTask Task, uint MorphScratch);
public sealed record NativeBattleRinoa88ProgramInputs(Func<uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleRinoa88Frame, uint, byte, NativeBattleVector> ReadBodyBone502170, Func<NativeBattleRinoa88Frame, NativeBattleVector> ReadPartyMidpoint769A90, Func<NativeBattleRinoa88Frame, NativeBattleGfAnimationSource, NativeBattleRinoa88ProgramNode, NativeBattleRinoa88Frame> Draw76A380, NativeBattleActionMatrix View);

/// <summary>772C50/76A180's embedded701970 workspaces and76A250 context construction.</summary>
public static class NativeBattleRinoa88Program
{
    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88ProgramInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback is not (0x772C50 or 0x76A180))
        {
            throw new NotSupportedException("Native Rinoa88 programme requires one of its original1140-byte wrappers.");
        }
        NativeBattleGfAnimationSource source = inputs.ReadSource(task.ReadDword(116));
        if (source.Address != task.ReadDword(116))
        {
            throw new InvalidDataException("Native76A250 programme source changed physical owner.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                if (task.Callback == 0x772C50 || unchecked((short)task.ReadWord(36)) >= 4)
                {
                    NativeBattleGfAnimationWorkspace workspace = Workspace(task).Initialize7016B0(source, unchecked((int)task.ReadDword(120)));
                    task = StoreWorkspace(task, workspace);
                    NativeBattleVector anchor = task.Callback == 0x772C50 ? inputs.ReadBodyBone502170(frame, task.ReadDword(1112), 241) : inputs.ReadPartyMidpoint769A90(frame);
                    task = task.WriteWord(28, unchecked((ushort)anchor.X)).WriteWord(30, task.Callback == 0x772C50 ? (ushort)0 : (ushort)256).WriteWord(32, unchecked((ushort)anchor.Z)).WriteDword(80, 4096).WriteDword(84, 4096).WriteDword(88, 4096);
                    if (task.Callback == 0x76A180)
                    {
                        task = task.WriteWord(136, unchecked((ushort)-2048));
                    }
                    Draw();
                    Next();
                }
                break;
            case 1:
                bool end = task.Callback == 0x76A180 && NativeBattleRinoa88Scene.Read(frame).Word(64) >= 5;
                if (end || Draw() == 0)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
                    Next();
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Rinoa88 programme signed phase exceeds its three callbacks.");
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        if (task.Callback == 0x76A180)
        {
            task = task.WriteWord(142, (ushort)((task.ReadByte(142) - 2) & 127));
        }
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
            NativeBattleActionMatrix context = Compose76A250(task, inputs.View);
            NativeBattleModulePoolTask captured = task;
            Store();
            NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, frame.State.Counter25A6BBC, (current, node) =>
            {
                frame = inputs.Draw76A380(frame, source, new(context, node, captured, 0x25AC750));
                return current;
            });
            task = StoreWorkspace(Read(), advanced.Workspace);
            return advanced.Remaining;
        }
    }

    public static NativeBattleActionMatrix Compose76A250(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
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
    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace) => task.WriteBytes(148, workspace.Data.AsSpan());
}
