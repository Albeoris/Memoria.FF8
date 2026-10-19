using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88BindActor(byte Actor, uint Resource, uint SavedOwner) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88ActorSequence(byte Actor, short Sequence) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88Sound(uint Descriptor, short Argument, short Volume) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88BodyEmission(uint Body, uint Callback) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88Pause(short Value) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88Deform(uint Body, short Argument) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88PublishWorld(ImmutableArray<byte> Matrix) : NativeBattleRinoa88Operation;
public sealed record NativeBattleRinoa88PrimaryBodyInputs(NativeBattleEffectOwnedModelResource Resource, Func<NativeBattleRinoa88Frame, byte, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRinoa88Frame, uint, NativeBattleRinoa88Frame> Draw7719A0, Func<NativeBattleRinoa88Frame, NativeBattleRinoa88Operation, NativeBattleRinoa88Frame> Execute, NativeBattleRinoa88Inputs Controller);

/// <summary>771DB0's seventeen phases and original post-draw position/world publication order.</summary>
public static class NativeBattleRinoa88PrimaryBody
{
    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88PrimaryBodyInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != 0x771DB0 || inputs.Resource.Address != 0x154F91C)
        {
            throw new InvalidDataException("Native771DB0 requires its actual primary model task and source.");
        }
        byte actor = task.ReadByte(44);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                NativeBattleRinoa88SceneMemory scene = Scene();
                frame = frame with { State = frame.State with { Scene = scene with { Settings = scene.Settings.Write(24, BitConverter.GetBytes(address)) } } };
                Execute(new NativeBattleRinoa88BindActor(actor, 0x15560C0, 0x25ACD10));
                Execute(new NativeBattleRinoa88ActorSequence(actor, 1));
                CopyCaster();
                Add(80, 4608);
                Select(0);
                task = task.WriteDword(276, 4096).WriteDword(280, 4096).WriteDword(284, 4096).WriteWord(296, unchecked((ushort)-256));
                Next();
                break;
            case 1:
                Animate(false);
                if (Signed(36) >= 11)
                {
                    Execute(new NativeBattleRinoa88ActorSequence(actor, 2));
                    Next();
                }
                break;
            case 2:
                if (Animate(false))
                {
                    task = task.WriteWord(296, 0);
                    Select(1);
                    CopyCaster();
                    Next();
                }
                break;
            case 3:
                if (task.ReadWord(310) == 8)
                {
                    Execute(new NativeBattleRinoa88Sound(0x154ECE4, 0, 128));
                }
                if (Animate(false))
                {
                    Select(2);
                    Append(4, 0x772C50, 1140, child => child.WriteWord(128, 0).WriteDword(116, 0x16F1F8C).WriteDword(120, 176).WriteWord(130, 0).WriteDword(1112, address + 48));
                    Next();
                }
                break;
            case 4:
                Animate(true);
                if (Signed(310) >= 20)
                {
                    Select(3);
                    Append(6, 0x772D90, 156, child => child.WriteDword(140, address + 48));
                    Next();
                }
                break;
            case 5:
                if (Animate(false))
                {
                    Select(8);
                    Next();
                }
                break;
            case 6:
                Animate(true);
                if (Signed(310) >= 6)
                {
                    task = task.WriteWord(308, 9);
                    Append(7, 0x773090, 64, child => child.WriteDword(52, 0x1557CAC).WriteWord(56, 0));
                    Next();
                }
                break;
            case 7:
                Animate(true);
                Add(308, -1);
                if (Signed(308) <= 0)
                {
                    Next();
                }
                break;
            case 8:
                Animate(true);
                scene = Scene();
                if (scene.CanRequestPhase(3))
                {
                    frame = frame with { State = frame.State with { Scene = scene.WriteWord(68, 3) } };
                    Execute(new NativeBattleRinoa88Pause(0));
                    Next();
                }
                else
                {
                    Execute(new NativeBattleRinoa88Pause(1));
                }
                break;
            case 9:
                Animate(true);
                if (Scene().Word(64) >= 3)
                {
                    Execute(new NativeBattleRinoa88Sound(0x154ECE8, 1, 128));
                    Select(8);
                    task = task.WriteWord(314, 2048);
                    Execute(new NativeBattleRinoa88Deform(address, 0));
                    Next();
                }
                break;
            case 10:
                Animate(true);
                if (Signed(310) >= 6)
                {
                    Select(4);
                    task = task.WriteWord(314, 2048);
                    Execute(new NativeBattleRinoa88Deform(address, 0));
                    Next();
                }
                break;
            case 11:
                Animate(false);
                if (Scene().Word(64) >= 4)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 4));
                    Next();
                }
                break;
            case 12:
                if (Scene().Word(64) >= 5)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~4)).WriteWord(314, 0);
                    Execute(new NativeBattleRinoa88Deform(address, 0));
                    Select(5);
                    Execute(new NativeBattleRinoa88ActorSequence(actor, 4));
                    CopyCaster();
                    Next();
                }
                break;
            case 13:
                if (Animate(false))
                {
                    Select(6);
                    Next();
                }
                break;
            case 14:
                if (Animate(false))
                {
                    Select(7);
                    task = task.WriteWord(296, 256);
                    Next();
                }
                break;
            case 15:
                Animate(true);
                if (Signed(310) >= 18)
                {
                    Select(3);
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5));
                    Next();
                }
                break;
            case 16:
                break;
            default:
                throw new InvalidDataException("Native771DB0's signed phase exceeds its seventeen callbacks.");
        }
        Add(76, Signed(292));
        Add(78, Signed(294));
        Add(80, Signed(296));
        Store();
        if ((task.ReadByte(38) & 4) == 0)
        {
            frame = inputs.Draw7719A0(frame, address);
            task = Read();
            if ((task.ReadByte(38) & 4) == 0)
            {
                byte animation = task.ReadByte(156);
                byte current = task.ReadByte(162);
                if ((animation == 1 && current >= 12 && current <= 17) || (animation == 5 && current >= 2 && current <= 7))
                {
                    Execute(new NativeBattleRinoa88BodyEmission(address, 0x771F50));
                }
                else if (animation == 3 && current >= 2 && current <= 5)
                {
                    Execute(new NativeBattleRinoa88BodyEmission(address, 0x772880));
                }
            }
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        Execute(new NativeBattleRinoa88PublishWorld(task.ReadBytes(112, 32).ToImmutableArray()));
        task = task.WriteDword(132, unchecked((uint)(int)Signed(76))).WriteDword(136, unchecked((uint)(int)Signed(78))).WriteDword(140, unchecked((uint)(int)Signed(80)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        void Add(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)(Signed(offset) + value)));
        void CopyCaster()
        {
            NativeBattleModelTransform transform = inputs.ReadModel(frame, actor).Transform ?? throw new NotSupportedException("Native771DB0 requires its actual caster position owner.");
            task = task.WriteWord(76, unchecked((ushort)transform.Position28.X)).WriteWord(78, unchecked((ushort)transform.Position28.Y)).WriteWord(80, unchecked((ushort)transform.Position28.Z)).WriteWord(82, unchecked((ushort)transform.Vertical34));
        }
        void Execute(NativeBattleRinoa88Operation operation)
        {
            Store();
            frame = inputs.Execute(frame, operation);
            task = Read();
        }
        void Append(int pool, uint callback, ushort stride, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
        {
            Store();
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, stride, address, (pointer, group, target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, inputs.Controller.Packets, pointer, group, target));
            hierarchy = appended.Hierarchy.Write(appended.Address, initialize(appended.Hierarchy.Read(appended.Address)));
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
            task = Read();
        }
        NativeBattleRinoa88SceneMemory Scene() => NativeBattleRinoa88Scene.Read(frame);
        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Store()
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
        NativeBattleEffectOwnedModelState Model() => frame.State.OwnedModels.TryGetValue(address, out NativeBattleEffectOwnedModelState? model) ? model : throw new NotSupportedException("Native771DB0 requires its actual initialized owned model.");
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
