namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88BodyInputs(NativeBattleEffectOwnedModelResource Resource, Func<NativeBattleRinoa88Frame, byte, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRinoa88Frame, uint, NativeBattleRinoa88Frame> Draw7719A0);

/// <summary>7718D0's twelve phases over its own simultaneous352-byte model task.</summary>
public static class NativeBattleRinoa88SecondaryBody
{
    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88BodyInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != 0x7718D0 || inputs.Resource.Address != 0x155563C)
        {
            throw new InvalidDataException("Native7718D0 requires its actual secondary model task and source.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                NativeBattleModelTransform transform = inputs.ReadModel(frame, task.ReadByte(44)).Transform ?? throw new NotSupportedException("Native771B80 requires its actual caster position owner.");
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 4)).WriteWord(76, unchecked((ushort)transform.Position28.X)).WriteWord(78, unchecked((ushort)transform.Position28.Y)).WriteWord(80, unchecked((ushort)transform.Position28.Z)).WriteWord(82, unchecked((ushort)transform.Vertical34)).WriteDword(276, 4096).WriteDword(280, 4096).WriteDword(284, 4096);
                Next();
                break;
            case 1:
                if (Signed(36) >= 12)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~4));
                    Select(0);
                    Next();
                }
                break;
            case 2:
                if (Animate(false))
                {
                    task = task.WriteWord(296, 0);
                    Select(1);
                    Next();
                }
                break;
            case 3:
                Animate(true);
                if (Signed(310) >= 20)
                {
                    Select(2);
                    Next();
                }
                break;
            case 4:
                if (Animate(false))
                {
                    Select(5);
                    Next();
                }
                break;
            case 5:
                Animate(true);
                if (Scene().Word(64) >= 3)
                {
                    Select(5);
                    Next();
                }
                break;
            case 6:
                Animate(true);
                if (Signed(310) >= 6)
                {
                    Select(3);
                    Next();
                }
                break;
            case 7:
                Animate(false);
                if (Scene().Word(64) >= 4)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 4));
                    Next();
                }
                break;
            case 8:
                if (Scene().Word(64) >= 5)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~4));
                    Select(4);
                    Next();
                }
                break;
            case 9:
                if (Animate(false))
                {
                    Next();
                }
                break;
            case 10:
                if (Animate(false))
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5));
                    Next();
                }
                break;
            case 11:
                break;
            default:
                throw new InvalidDataException("Native7718D0's signed phase exceeds its twelve callbacks.");
        }
        Store();
        if ((task.ReadByte(38) & 4) == 0)
        {
            frame = inputs.Draw7719A0(frame, address);
            task = Read();
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        NativeBattleRinoa88SceneMemory Scene() => NativeBattleRinoa88Scene.Read(frame);
        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Store()
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
        NativeBattleEffectOwnedModelState Model() => frame.State.OwnedModels.TryGetValue(address, out NativeBattleEffectOwnedModelState? model) ? model : throw new NotSupportedException("Native7718D0 requires its actual initialized owned model.");
        void Select(short animation)
        {
            Apply(NativeBattleEffectOwnedModel.Select8DD190(task, Model(), inputs.Resource, animation));
        }
        bool Animate(bool loop)
        {
            NativeBattleEffectOwnedModelStep step = loop ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, Model(), inputs.Resource) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, Model(), inputs.Resource);
            Apply(step);
            return step.Completed;
        }
        void Apply(NativeBattleEffectOwnedModelStep step)
        {
            task = step.Task;
            frame = frame with { State = frame.State with { OwnedModels = frame.State.OwnedModels.SetItem(address, step.State) } };
        }
    }
}
