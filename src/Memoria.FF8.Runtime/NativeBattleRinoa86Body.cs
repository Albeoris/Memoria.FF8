using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86BindActor(byte Actor, uint Resource, uint SavedModel) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86ActorSequence(byte Actor, short Sequence) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86ActorFlags(byte Actor, ushort Flags) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86Sound(uint Descriptor, short Argument, short Volume) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86Emit(uint Body, int Mode) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86PublishWorld(ImmutableArray<byte> Matrix) : NativeBattleRinoa86Operation;
public sealed record NativeBattleRinoa86BodyInputs(NativeBattleRinoa86Inputs Controller, NativeBattleRinoa86Resources Resources, Func<NativeBattleRinoa86Frame, byte, ushort> ReadActorFlags, Func<NativeBattleRinoa86Frame, NativeBattleRinoa86Operation, NativeBattleRinoa86Frame> Execute, Func<NativeBattleRinoa86Frame, uint, NativeBattleRinoa86Frame> Draw780130, Func<NativeBattleRinoa86Frame, uint, byte, NativeBattleVector> ReadBone502170);

/// <summary>77FF90 and its eleven original body phases, with physical child pools and retained pose ownership.</summary>
public static class NativeBattleRinoa86Body
{
    public static NativeBattleRinoa86LeafStep Advance(NativeBattleRinoa86Frame frame, uint address, NativeBattleRinoa86BodyInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != 0x77FF90)
        {
            throw new InvalidDataException("Native77FF90 requires its original352-byte body task.");
        }
        NativeBattleRinoa86SceneMemory scene = NativeBattleRinoa86Scene.Read(frame);
        byte actor = task.ReadByte(44);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                Execute(new NativeBattleRinoa86BindActor(actor, 0x156AA60, 0x25BC1F8));
                Execute(new NativeBattleRinoa86ActorSequence(actor, 1));
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 4));
                for (int index = 0; index < 4; index++)
                {
                    task = task.WriteWord(76 + index * 2, scene.Word(index * 2));
                }
                task = task.WriteWord(80, unchecked((ushort)(task.ReadWord(80) + 4608))).WriteWord(78, 0).WriteDword(276, 4096).WriteDword(280, 4096).WriteDword(284, 4096);
                Select(0);
                ushort flags = inputs.ReadActorFlags(frame, actor);
                Execute(new NativeBattleRinoa86ActorFlags(actor, (ushort)(flags | 0x200)));
                task = task.WriteDword(336, flags).WriteWord(308, 0);
                Next();
                break;
            case 1:
                task = task.WriteWord(308, unchecked((ushort)(task.ReadWord(308) + 1)));
                if (task.ReadWord(308) == 4)
                {
                    Execute(new NativeBattleRinoa86Sound(0x1561774, 1, 128));
                }
                if (scene.Word(64) >= 2)
                {
                    AppendBodyLayer(0x783350);
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~4)).WriteWord(296, unchecked((ushort)-256));
                    Next();
                }
                break;
            case 2:
                Animate(true);
                if (Signed(310) >= 18)
                {
                    Select(1);
                    Execute(new NativeBattleRinoa86ActorSequence(actor, 2));
                    task = task.WriteWord(296, 0);
                    Next();
                }
                break;
            case 3:
                Animate(false);
                if (task.ReadWord(310) == 12)
                {
                    AppendProgram(0x7836E0, 0x1706398, 56, 0, true);
                    Next();
                }
                break;
            case 4:
                if (Animate(false))
                {
                    if (scene.Word(64) >= 3)
                    {
                        SetCounters(0);
                        Execute(new NativeBattleRinoa86Sound(0x1561778, 1, 128));
                        Select(2);
                        task = task.WriteDword(320, task.ReadDword(76)).WriteDword(324, task.ReadDword(80));
                        task = task.WriteDword(328, (uint)(scene.Word(8) | scene.Word(10) << 16)).WriteDword(332, (uint)(scene.Word(12) | scene.Word(14) << 16)).WriteWord(340, 0).WriteWord(342, 513);
                        AppendProgram(0x783C90, 0x1706CC0, 416, 1, true);
                        AppendBodyLayer(0x783DC0);
                        Next();
                    }
                    else
                    {
                        SetCounters(1);
                    }
                }
                break;
            case 5:
                Animate(true);
                task = Move784070(task);
                if (task.ReadWord(340) == 4096)
                {
                    Select(3);
                    AppendProgram(0x783F80, 0x1709404, 932, 2, false);
                    ushort currentFlags = inputs.ReadActorFlags(frame, actor);
                    Execute(new NativeBattleRinoa86ActorFlags(actor, (ushort)(currentFlags ^ ((currentFlags ^ task.ReadWord(336)) & 0x200))));
                    Next();
                }
                break;
            case 6:
                if (Animate(false))
                {
                    Select(4);
                    uint from0 = task.ReadDword(320);
                    uint from1 = task.ReadDword(324);
                    task = task.WriteDword(320, task.ReadDword(328)).WriteDword(324, task.ReadDword(332)).WriteDword(328, from0).WriteDword(332, from1);
                    task = task.WriteWord(340, 0).WriteWord(344, 0).WriteWord(342, 274).WriteWord(346, unchecked((ushort)-512)).WriteWord(348, 64);
                    Next();
                }
                break;
            case 7:
                task = task.WriteWord(346, unchecked((ushort)(task.ReadWord(346) + task.ReadWord(348))));
                task = task.WriteWord(344, unchecked((ushort)(task.ReadWord(344) + task.ReadWord(346))));
                if (Signed(344) > 0)
                {
                    task = task.WriteWord(344, 0);
                }
                Animate(true);
                task = Move784070(task);
                if (task.ReadWord(340) == 4096)
                {
                    Select(5);
                    Execute(new NativeBattleRinoa86ActorSequence(actor, 3));
                    Next();
                }
                break;
            case 8:
                if (Animate(false))
                {
                    Select(6);
                    task = task.WriteWord(296, 256);
                    Next();
                }
                break;
            case 9:
                Animate(true);
                if (Signed(310) >= 18)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5));
                    Next();
                }
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Native77FF90 body phase exceeds its eleven callbacks.");
        }
        task = task.WriteWord(80, unchecked((ushort)(task.ReadWord(80) + task.ReadWord(296)))).WriteWord(78, unchecked((ushort)(task.ReadWord(78) + task.ReadWord(294))));
        if ((task.ReadByte(38) & 4) == 0)
        {
            Store();
            frame = inputs.Draw780130(frame, address);
            task = Read();
            if ((task.ReadByte(38) & 4) == 0)
            {
                if (task.ReadByte(156) == 0)
                {
                    switch (task.ReadByte(162))
                    {
                        case 1:
                            Execute(new NativeBattleRinoa86Emit(address, 2));
                            break;
                        case 2:
                            Execute(new NativeBattleRinoa86Emit(address, 1));
                            Execute(new NativeBattleRinoa86Emit(address, 3));
                            break;
                        case 4:
                            Execute(new NativeBattleRinoa86Emit(address, 0));
                            break;
                    }
                }
                else if (task.ReadByte(156) == 1 && task.ReadByte(162) <= 5)
                {
                    Execute(new NativeBattleRinoa86Emit(address, 4));
                    Execute(new NativeBattleRinoa86Emit(address, 5));
                }
                else if (task.ReadByte(156) == 5 && task.ReadByte(162) <= 5)
                {
                    Execute(new NativeBattleRinoa86Emit(address, 6));
                }
            }
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Execute(new NativeBattleRinoa86PublishWorld(Enumerable.Range(112, 32).Select(task.ReadByte).ToImmutableArray()));
        task = task.WriteDword(132, unchecked((uint)Signed(76))).WriteDword(136, unchecked((uint)Signed(78))).WriteDword(140, unchecked((uint)Signed(80)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Store() => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Execute(NativeBattleRinoa86Operation operation)
        {
            Store();
            frame = inputs.Execute(frame, operation);
            task = Read();
        }
        void Select(short animation)
        {
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(task, RequireModel(), inputs.Resources.Model, animation);
            task = selected.Task;
            frame = frame with { State = frame.State with { BodyModel = selected.State } };
        }
        bool Animate(bool loop)
        {
            NativeBattleEffectOwnedModelStep advanced = loop ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, RequireModel(), inputs.Resources.Model) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, RequireModel(), inputs.Resources.Model);
            task = advanced.Task;
            frame = frame with { State = frame.State with { BodyModel = advanced.State } };
            return advanced.Completed;
        }
        NativeBattleEffectOwnedModelState RequireModel() => frame.State.BodyModel ?? throw new NotSupportedException("Native77FF90 requires its actual8DD0F0 model owner.");
        void SetCounters(int value)
        {
            if (value != 0 && frame.State.Counter25BC218 == 0 || value == 0 && frame.State.Counter25BC218 == 1)
            {
                frame = frame with { State = frame.State with { Counter25BC218 = value, Counter25B7080 = value } };
            }
        }
        NativeBattleEffectTaskActors SelectActors(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, inputs.Controller.Packets, pointer, group, target);
        void AppendBodyLayer(uint callback)
        {
            Store();
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, callback, 152, address, SelectActors);
            frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteDword(140, checked(address + 48))).Pools } };
            task = Read();
        }
        void AppendProgram(uint callback, uint source, uint workspace, ushort mode, bool useBone)
        {
            Store();
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(3, callback, 1104, address, SelectActors);
            NativeBattleModulePoolTask program = child.Hierarchy.Read(child.Address).WriteWord(128, mode).WriteDword(116, source).WriteDword(120, workspace).WriteWord(130, 0);
            if (useBone)
            {
                NativeBattleVector point = inputs.ReadBone502170(frame, address, 241);
                program = program.WriteWord(28, unchecked((ushort)point.X)).WriteWord(30, unchecked((ushort)point.Y)).WriteWord(32, unchecked((ushort)point.Z));
            }
            frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, program).Pools } };
            task = Read();
        }
    }

    public static NativeBattleModulePoolTask Move784070(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(340, unchecked((ushort)(task.ReadWord(340) + task.ReadWord(342))));
        short progress = unchecked((short)task.ReadWord(340));
        if (progress < 4096)
        {
            for (int index = 0; index < 3; index++)
            {
                short from = unchecked((short)task.ReadWord(320 + index * 2));
                short to = unchecked((short)task.ReadWord(328 + index * 2));
                task = task.WriteWord(76 + index * 2, unchecked((ushort)(from + progress * (to - from) / 4096)));
            }
        }
        else
        {
            task = task.WriteDword(76, task.ReadDword(328)).WriteDword(80, task.ReadDword(332)).WriteWord(340, 4096);
        }
        return task.WriteWord(78, unchecked((ushort)(task.ReadWord(78) + task.ReadWord(344))));
    }
}
