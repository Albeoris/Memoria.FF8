using System.Collections.Immutable;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87BindActor(byte Actor, uint Resource, uint SavedModel) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87ActorSequence(byte Actor, short Sequence) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87Sound(uint Descriptor, short Argument, short Volume) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87Emit(uint Body, int Mode) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87GroundParticle(uint Body) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87CaptureGround(uint Body) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87PublishWorld(ImmutableArray<byte> Matrix) : NativeBattleRinoa87Operation;
public sealed record NativeBattleRinoa87BodyInputs(NativeBattleRinoa87Inputs Controller, NativeBattleEffectOwnedModelResource Model, ImmutableArray<short> TargetCurve, Func<NativeBattleRinoa87Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRinoa87Frame, int, NativeBattleGraphicsModel, NativeBattleRinoa87Frame> WriteModel, Func<NativeBattleRinoa87Frame, int, int> Minimum501FF0, Func<NativeBattleRinoa87Frame, byte, byte, NativeBattleVector> ReadBone502170, Func<NativeBattleRinoa87Frame, NativeBattleRinoa87Operation, NativeBattleRinoa87Frame> Execute, Func<NativeBattleRinoa87Frame, uint, NativeBattleRinoa87Frame> Draw775330);

/// <summary>7751A0's original seventeen body phases and native target save/restore ordering.</summary>
public static class NativeBattleRinoa87Body
{
    public static NativeBattleRinoa87LeafStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87BodyInputs inputs)
    {
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != 0x7751A0)
        {
            throw new InvalidDataException("Native7751A0 requires its actual376-byte body task.");
        }
        NativeBattleRinoa87SceneMemory scene = NativeBattleRinoa87Scene.Read(frame);
        byte actor = task.ReadByte(44);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                scene = scene with { Settings = scene.Settings.Write(24, BitConverter.GetBytes(address)) };
                frame = frame with { State = frame.State with { Scene = scene } };
                Execute(new NativeBattleRinoa87BindActor(actor, 0x1560550, 0x25B4B28));
                Execute(new NativeBattleRinoa87ActorSequence(actor, 1));
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 4)).WriteBytes(76, scene.Settings.Read(0, 8)).WriteWord(62, scene.Word(74));
                int targetIndex = Minimum();
                NativeBattleGraphicsModel target = inputs.ReadModel(frame, targetIndex);
                NativeBattleModelTransform targetTransform = Transform(target);
                task = task.WriteWord(336, Raw(target.Rotation12)).WriteWord(338, Raw(target.Rotation14)).WriteWord(340, Raw(target.Rotation16));
                task = target.RotationPadding18 is { } padding ? task.WriteWord(342, unchecked((ushort)padding)) : task.MarkPayloadUnknown(342, 2);
                task = CopyPosition(task, 344, targetTransform).WriteDword(276, 4096).WriteDword(280, 4096).WriteDword(284, 4096);
                frame = frame with { State = frame.State with { TargetRotationPadding18 = target.RotationPadding18 } };
                Store();
                frame = inputs.WriteModel(frame, targetIndex, target with { Rotation14 = (short)((scene.Word(74) - 2048) & 4095) });
                task = Read();
                Next();
                break;
            case 1:
                if (Signed(36) >= 15)
                {
                    NativeBattleGraphicsModel enemy = inputs.ReadModel(frame, Minimum());
                    NativeBattleActionMatrix basis = TargetYaw(enemy);
                    NativeBattleVector delta = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, unchecked((short)(MinimumZ(enemy) / 2 - 1024))));
                    task = CopyPosition(task, 328, Transform(enemy));
                    Add(328, delta.X);
                    Set(330, delta.Y);
                    Add(332, delta.Z);
                    task = task.WriteDword(320, task.ReadDword(328)).WriteDword(324, task.ReadDword(332));
                    delta = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, -8192));
                    Add(320, delta.X);
                    Add(322, delta.Y);
                    Add(324, delta.Z);
                    task = task.WriteDword(76, task.ReadDword(320)).WriteDword(80, task.ReadDword(324)).WriteByte(38, (byte)(task.ReadByte(38) & ~4));
                    Set(360, 0);
                    Set(362, 187);
                    Set(364, 0);
                    Set(366, 0);
                    Set(368, -512);
                    Set(370, 46);
                    Select(0);
                    Next();
                }
                break;
            case 2:
                if (frame.State.Counter25B4B4C == 0)
                {
                    Arc();
                    Move();
                    Animate(true);
                    if (Signed(310) >= 20)
                    {
                        Select(10);
                        Next();
                    }
                }
                break;
            case 3:
                Arc();
                Move();
                if (Animate(false))
                {
                    Select(1);
                    AppendProgram(0x7764B0, 0x16FD248, 176, 0, 0);
                    Execute(new NativeBattleRinoa87CaptureGround(address));
                    Execute(new NativeBattleRinoa87Sound(0x1557D2C, 0, 128));
                    Next();
                }
                break;
            case 4:
                if (Animate(false))
                {
                    Select(2);
                    CopyCurrentToStart();
                    NativeBattleGraphicsModel enemy = inputs.ReadModel(frame, Minimum());
                    NativeBattleVector bone = inputs.ReadBone502170(frame, checked((byte)Minimum()), 241);
                    task = task.WriteWord(328, unchecked((ushort)bone.X)).WriteWord(330, unchecked((ushort)bone.Y)).WriteWord(332, unchecked((ushort)bone.Z));
                    NativeBattleVector delta = NativeBattleGfMatrices.RotateShort56C4F0(TargetYaw(enemy), new(0, 512, unchecked((short)(MinimumZ(enemy) / 2))));
                    Add(328, delta.X);
                    Add(330, delta.Y);
                    Add(332, delta.Z);
                    Set(360, 0);
                    Set(362, 2049);
                    Set(364, 0);
                    Next();
                }
                break;
            case 5:
                Move();
                if (task.ReadWord(310) == 1)
                {
                    for (ushort index = 0; index < 4; index++)
                    {
                        AppendLargeProgram(index);
                    }
                }
                if (Animate(false))
                {
                    Select(3);
                    Next();
                }
                break;
            case 6:
                if (Animate(false))
                {
                    Select(4);
                    Add(78, -512);
                    CopyCurrentToStart();
                    task = task.WriteDword(328, task.ReadDword(76)).WriteDword(332, task.ReadDword(80));
                    NativeBattleModelTransform enemy = Transform(inputs.ReadModel(frame, Minimum()));
                    Set(352, enemy.Position28.X - Signed(76));
                    Set(354, enemy.Position28.Y - Signed(78));
                    Set(356, enemy.Position28.Z - Signed(80));
                    Set(330, -6144);
                    Set(360, 0);
                    Set(372, 0);
                    Set(60, 3072);
                    NativeBattleActionMatrix inverse = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), unchecked((short)(4096 - Signed(62))));
                    NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(inverse, new(Signed(352), Signed(354), Signed(356)));
                    Set(352, offset.X);
                    Set(354, offset.Y);
                    Set(356, offset.Z);
                    AppendMesh(0x77DFC0, true);
                    AppendProgram(0x7764B0, 0x16FD248, 176, 0, 1);
                    Set(308, 8);
                    MoveTarget(true);
                    Next();
                }
                break;
            case 7:
                MoveTarget(true);
                Animate(true);
                Add(308, -1);
                if (Signed(308) <= 0)
                {
                    Set(308, 0);
                    Next();
                }
                break;
            case 8:
                Set(374, Math.Max(-768, Signed(374) - 128));
                Add(60, Signed(374));
                if (Signed(60) < 0)
                {
                    Add(308, 1);
                }
                Set(60, task.ReadWord(60) & 4095);
                if (Signed(308) < 4)
                {
                    MoveTarget(true);
                }
                else
                {
                    Set(60, 0);
                    Select(5);
                    uint start0 = task.ReadDword(320);
                    uint start1 = task.ReadDword(324);
                    task = task.WriteDword(320, task.ReadDword(328)).WriteDword(324, task.ReadDword(332)).WriteDword(328, start0).WriteDword(332, start1);
                    Add(356, 256);
                    Set(330, 0);
                    Set(360, 0);
                    Set(362, 0);
                    Set(364, 192);
                    Next();
                }
                Animate(true);
                break;
            case 9:
                Animate(true);
                MoveTarget(false);
                if (task.ReadWord(360) == 4096)
                {
                    Select(6);
                    NativeBattleRinoa87IoState io = frame.State.Io ?? throw new NotSupportedException("Native77E600 requires its initialized IO control pointer.");
                    AppendProgram(0x77E6C0, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(296)), 428, 1, null);
                    AppendMesh(0x77E800, false);
                    Execute(new NativeBattleRinoa87Sound(0x1557D30, 0, 128));
                    Next();
                }
                break;
            case 10:
                if (Animate(false))
                {
                    Select(11);
                    CopyCurrentToStart();
                    task = CopyPosition(task, 328, Transform(inputs.ReadModel(frame, actor)));
                    Set(360, 0);
                    Set(362, 293);
                    Set(364, 0);
                    Set(366, 0);
                    Set(368, -512);
                    Set(370, 73);
                    Next();
                }
                break;
            case 11:
                Arc();
                Move();
                if (Animate(false))
                {
                    Select(7);
                    Next();
                }
                break;
            case 12:
                Arc();
                Move();
                Animate(true);
                if (Signed(310) >= 10)
                {
                    Select(12);
                    Next();
                }
                break;
            case 13:
                Arc();
                Move();
                if (Animate(false))
                {
                    int selected = Minimum();
                    NativeBattleGraphicsModel enemy = inputs.ReadModel(frame, selected);
                    NativeBattleModelTransform transform = Transform(enemy) with { Position28 = new(Signed(344), Signed(346), Signed(348)), Vertical34 = Signed(350) };
                    Store();
                    frame = inputs.WriteModel(frame, selected, enemy with { Rotation12 = Signed(336), Rotation14 = Signed(338), Rotation16 = Signed(340), RotationPadding18 = frame.State.TargetRotationPadding18, Transform = transform });
                    task = Read();
                    Select(8);
                    Execute(new NativeBattleRinoa87ActorSequence(actor, 2));
                    Next();
                }
                break;
            case 14:
                if (Animate(false))
                {
                    Select(9);
                    task = NativeBattleRinoa87Motion.Velocity77EFD0(task, 256);
                    Next();
                }
                break;
            case 15:
                task = NativeBattleRinoa87Motion.TurnTowardZero77F070(task);
                task = NativeBattleRinoa87Motion.Velocity77EFD0(task, 256);
                Animate(true);
                if (Signed(310) >= 18)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5));
                    Next();
                }
                break;
            case 16:
                break;
            default:
                throw new InvalidDataException("Native7751A0 phase exceeds its original seventeen callbacks.");
        }
        Add(76, Signed(292));
        Add(78, Signed(294));
        Add(80, Signed(296));
        if ((task.ReadByte(38) & 4) == 0)
        {
            Store();
            frame = inputs.Draw775330(frame, address);
            task = Read();
            if ((task.ReadByte(38) & 4) == 0)
            {
                byte animation = task.ReadByte(156);
                byte tick = task.ReadByte(162);
                if ((animation == 1 || animation == 8) && tick <= 5)
                {
                    Execute(new NativeBattleRinoa87GroundParticle(address));
                }
                else if (animation == 1 && tick >= 16)
                {
                    Execute(new NativeBattleRinoa87Emit(address, (tick & 1) != 0 ? 2 : 3));
                }
            }
        }
        Add(36, 1);
        Execute(new NativeBattleRinoa87PublishWorld(Enumerable.Range(112, 32).Select(task.ReadByte).ToImmutableArray()));
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
        void Set(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)value));
        void Add(int offset, int value) => Set(offset, Signed(offset) + value);
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Store() => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        int Minimum() => inputs.Minimum501FF0(frame, task.ReadByte(45));
        void Move() => task = NativeBattleRinoa87Motion.Move7762D0(task);
        void Arc()
        {
            Add(368, Signed(370));
            Set(366, Math.Min(0, (int)unchecked((short)(Signed(366) + Signed(368)))));
        }
        void MoveTarget(bool curve)
        {
            NativeBattleRinoa87TargetMotion moved = NativeBattleRinoa87Motion.MoveTarget77E260(task, Enumerable.Range(0, 7).Select(index => inputs.ReadModel(frame, index)).ToImmutableArray(), Minimum(), curve, inputs.TargetCurve);
            task = moved.Task;
            Store();
            for (int index = 0; index < moved.Models.Length; index++)
            {
                if (moved.Models[index] != inputs.ReadModel(frame, index))
                {
                    frame = inputs.WriteModel(frame, index, moved.Models[index]);
                }
            }
            task = Read();
        }
        void Execute(NativeBattleRinoa87Operation operation)
        {
            Store();
            frame = inputs.Execute(frame, operation);
            task = Read();
        }
        void Select(short animation)
        {
            NativeBattleEffectOwnedModelStep step = NativeBattleEffectOwnedModel.Select8DD190(task, RequireModel(), inputs.Model, animation);
            task = step.Task;
            frame = frame with { State = frame.State with { BodyModel = step.State } };
        }
        bool Animate(bool loop)
        {
            NativeBattleEffectOwnedModelStep step = loop ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, RequireModel(), inputs.Model) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, RequireModel(), inputs.Model);
            task = step.Task;
            frame = frame with { State = frame.State with { BodyModel = step.State } };
            return step.Completed;
        }
        NativeBattleEffectOwnedModelState RequireModel() => frame.State.BodyModel ?? throw new NotSupportedException("Native7751A0 requires its actual8DD0F0 model owner.");
        NativeBattleEffectTaskActors SelectActors(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, inputs.Controller.Packets, pointer, group, target);
        void CopyCurrentToStart() => task = task.WriteDword(320, task.ReadDword(76)).WriteDword(324, task.ReadDword(80));
        void AppendMesh(uint callback, bool bodyPointer)
        {
            Store();
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(6, callback, 152, address, SelectActors);
            NativeBattleModulePoolTask mesh = child.Hierarchy.Read(child.Address);
            mesh = bodyPointer ? mesh.WriteDword(140, checked(address + 48)) : mesh.WriteDword(28, task.ReadDword(76)).WriteDword(32, task.ReadDword(80)).WriteWord(30, 0);
            frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, mesh).Pools } };
            task = Read();
        }
        void AppendProgram(uint callback, uint source, uint workspace, ushort mode, ushort? variant)
        {
            Store();
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(4, callback, 604, address, SelectActors);
            NativeBattleModulePoolTask program = child.Hierarchy.Read(child.Address).WriteWord(128, mode).WriteDword(116, source).WriteDword(120, workspace).WriteWord(130, 0).WriteDword(576, checked(address + 48));
            if (variant is { } value)
            {
                program = program.WriteWord(138, value);
            }
            frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, program).Pools } };
            task = Read();
        }
        void AppendLargeProgram(ushort index)
        {
            Store();
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(3, 0x779050, 676, address, SelectActors);
            NativeBattleModulePoolTask program = child.Hierarchy.Read(child.Address).WriteDword(48, 0x16FBE40).WriteWord(664, 0).WriteWord(668, 4).WriteWord(670, 5).WriteWord(672, index);
            frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, program).Pools } };
            task = Read();
        }
    }

    private static ushort Raw(short? value) => unchecked((ushort)(value ?? throw new NotSupportedException("Native776010 requires the target's actual rotation WORDs.")));
    private static NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Native Rinoa87 body requires the model's actual position and fourth WORD.");
    private static short MinimumZ(NativeBattleGraphicsModel model) => (model.GeometryBounds52 ?? throw new NotSupportedException("Native Rinoa87 body requires target bounds WORD56.")).Minimum.Z;
    private static NativeBattleActionMatrix TargetYaw(NativeBattleGraphicsModel model) => NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), unchecked((short)Raw(model.Rotation14)));
    private static NativeBattleModulePoolTask CopyPosition(NativeBattleModulePoolTask task, int offset, NativeBattleModelTransform transform) => task.WriteWord(offset, unchecked((ushort)transform.Position28.X)).WriteWord(offset + 2, unchecked((ushort)transform.Position28.Y)).WriteWord(offset + 4, unchecked((ushort)transform.Position28.Z)).WriteWord(offset + 6, unchecked((ushort)transform.Vertical34));
}
