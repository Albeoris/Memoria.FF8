using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleTonberry90State
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectOwnedModelState? CreatureModelState { get; init; }
}

public sealed record NativeBattleTonberry90CreatureDraw(NativeBattleTonberry90Frame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleTonberry90CreatureServices(Func<NativeBattleTonberry90Frame, uint> ReadTransitionColor, Func<NativeBattleTonberry90Frame, uint, byte, uint, NativeBattleTonberry90Frame> PlaySound, Func<NativeBattleTonberry90Frame, uint, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleTonberry90CreatureDraw> Draw)
{
    public NativeBattleActionPacketsState? Packets { get; init; }
    public Func<NativeBattleTonberry90Frame, uint>? ReadDefaultColor { get; init; }
    public Func<NativeBattleTonberry90Frame, NativeBattleTonberry90Frame>? PlayStream { get; init; }
}

public static partial class NativeBattleTonberry90Creature
{
    public const uint ProjectedAddress767610 = 0x25A2D00;
    public const int ProjectedWorkLength767610 = 0x1000;

    public static NativeBattleTonberry90Frame PublishProjected767610(NativeBattleTonberry90Frame frame, ImmutableArray<byte> projected, int bytesWritten)
    {
        if (bytesWritten < 0 || bytesWritten > ProjectedWorkLength767610 || (bytesWritten & 7) != 0 || projected.IsDefault || bytesWritten > projected.Length)
        {
            throw new InvalidDataException("Native Tonberry projected stores leave their supplied static work window.");
        }
        if (bytesWritten == 0)
        {
            return frame;
        }
        return frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(ProjectedAddress767610, projected.AsSpan().Slice(0, bytesWritten)) } };
    }

    public static NativeBattleTonberry90Frame Initialize8DD0F0(NativeBattleTonberry90Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, ushort mode, uint defaultColor44)
    {
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(Read(frame, address), address, resource, mode, defaultColor44);
        NativeBattleTonberry90State shared = State(frame) with { CreatureModelState = initialized.State };
        return Write(frame with { Memory = frame.Memory with { Tonberry90 = shared } }, address, initialized.Task);
    }

    public static NativeBattleTonberry90TaskStep Advance7674E0(NativeBattleTonberry90Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, NativeBattleTonberry90CreatureServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectOwnedModelState model = State(frame).CreatureModelState ?? throw new NotSupportedException("Native Tonberry creature requires its actual initialized model owner.");
        NativeBattleEffectOwnedModel.Validate(task, model, resource);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                uint director = State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry creature requires its current director pointer.");
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteByte(0x30, (byte)(task.ReadByte(0x30) | 0x20)).WriteDword(0x4C, State(frame).ReadDword(director)).WriteDword(0x50, State(frame).ReadDword(director + 4)).WriteDword(0x60, checked(address + 0x114)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                frame = frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(0x25A2B78, new byte[2]) } };
                Select(3);
                frame = Write(frame, address, task);
                frame = services.PlaySound(frame, 0x154716C, 128, 127);
                frame = services.PlaySound(frame, 0x16D7DE8, 128, 127);
                task = Read(frame, address);
                task = Next(task);
                break;
            case 1:
                if (Word(task, 0x24) >= 28)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)).WriteWord(0x13A, 4096));
                }
                task = Recolor7678D0(task.WriteWord(0x13C, task.ReadWord(0x13A)), Word(task, 0x13A), 0, services.ReadTransitionColor(frame));
                break;
            case 2:
                if (Word(task, 0x24) >= 40)
                {
                    task = task.WriteWord(0x13A, 0);
                    Select(0);
                    task = Next(task);
                }
                else
                {
                    task = task.WriteWord(0x13A, unchecked((ushort)(task.ReadWord(0x13A) - 341)));
                    if (Word(task, 0x13A) < 0)
                    {
                        task = task.WriteWord(0x13A, 0);
                    }
                }
                task = Recolor7678D0(task.WriteWord(0x13C, task.ReadWord(0x13A)), Word(task, 0x13A), 0, services.ReadTransitionColor(frame));
                break;
            case 3:
                if (Word(task, 0x136) is >= 77 and <= 84)
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort fraction) };
                    var appended = NativeBattleTonberry90Director.AppendTask(Write(frame, address, task), 0x259F098, 0x767C70, 0x7C, address, RequirePackets());
                    frame = appended.Frame;
                    task = Read(frame, address);
                    NativeBattleModulePoolTask child = Read(frame, appended.Address);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = frame with { Random = frame.Random.Draw(out ushort random) };
                        child = child.WriteWord(0x1C + axis * 2, unchecked((ushort)((random & 255) - 128)));
                    }
                    frame = Write(frame, appended.Address, child);
                    NativeBattleVector anchor = NativeBattlePoseMatrices.ReadEffectOwnedBone(resource.PoseSource, model.DecodedPose ?? throw new NotSupportedException("Native Tonberry particle anchor requires its current decoded pose."), ReadWorld(task), 13, (short)(fraction & 0xFFF));
                    child = child.WriteWord(0x1C, unchecked((ushort)anchor.X)).WriteWord(0x1E, unchecked((ushort)anchor.Y)).WriteWord(0x20, unchecked((ushort)anchor.Z));
                    frame = Write(frame, appended.Address, child);
                }
                if (task.ReadWord(0x136) == 90)
                {
                    frame = Write(frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(0x25A2B78, new byte[] { 0, 255 }) } }, address, task);
                    frame = NativeBattleTonberry90Director.Spawn763E60(frame, address, 0x767B40, 0x16F0F40, 20, 1, 0, RequirePackets()).Frame;
                    task = Read(frame, address);
                }
                NativeBattleEffectOwnedModelStep advanced = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resource);
                task = advanced.Task;
                model = advanced.State;
                if (advanced.Completed)
                {
                    NativeBattleVector velocity = Rotate764020(Word(task, 0x3E), -304);
                    task = task.WriteWord(0x124, unchecked((ushort)velocity.X)).WriteWord(0x126, unchecked((ushort)velocity.Y)).WriteWord(0x128, unchecked((ushort)velocity.Z));
                    Select(1);
                    task = Next(task);
                }
                break;
            case 4:
                Loop();
                if (Spawned(3))
                {
                    task = Next(task.WriteWord(0x30, (ushort)(task.ReadWord(0x30) & ~0x20)));
                }
                break;
            case 5:
                Loop();
                if (Word(task, 0x138) >= 3)
                {
                    task = Next(task.WriteDword(0x140, task.ReadDword(0x124)).WriteDword(0x144, task.ReadDword(0x128)));
                }
                break;
            case 6:
                var requestedSix = NativeBattleTonberry90Programme.RequestPhase763940(frame, 6);
                frame = requestedSix.Frame;
                if (requestedSix.Accepted)
                {
                    Loop();
                    frame = SetFrozen767ED0(frame, false);
                    task = Next(task.WriteDword(0x124, task.ReadDword(0x140)).WriteDword(0x128, task.ReadDword(0x144)));
                }
                else
                {
                    frame = SetFrozen767ED0(frame, true);
                    task = task.WriteWord(0x128, 0).WriteWord(0x126, 0).WriteWord(0x124, 0);
                }
                break;
            case 7:
                Loop();
                if (Spawned(6))
                {
                    uint targetDirector = DirectorAddress();
                    task = task.WriteDword(0x4C, State(frame).ReadDword(targetDirector + 8)).WriteDword(0x50, State(frame).ReadDword(targetDirector + 12)).WriteWord(0x3E, State(frame).ReadWord(targetDirector + 0x4A));
                    NativeBattleVector approach = Rotate764020(Word(task, 0x3E), -304);
                    task = task.WriteWord(0x124, unchecked((ushort)approach.X)).WriteWord(0x126, unchecked((ushort)approach.Y)).WriteWord(0x128, unchecked((ushort)approach.Z));
                    foreach (short distance in new short[] { -384, -380 })
                    {
                        NativeBattleVector offset = Rotate764020(Word(task, 0x3E), distance);
                        task = task.WriteWord(0x4C, unchecked((ushort)(task.ReadWord(0x4C) - offset.X))).WriteWord(0x4E, unchecked((ushort)(task.ReadWord(0x4E) - offset.Y))).WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - offset.Z)));
                    }
                    task = Next(task);
                }
                break;
            case 8:
                Loop();
                if (Word(task, 0x138) >= 4)
                {
                    task = task.WriteWord(0x128, 0).WriteWord(0x126, 0).WriteWord(0x124, 0);
                    Select(2);
                    task = Next(task);
                }
                break;
            case 9:
                if (task.ReadWord(0x136) == 30)
                {
                    frame = Write(frame with { Memory = frame.Memory with { Tonberry90 = State(frame) with { CreatureModelState = model } } }, address, task);
                    frame = (services.PlayStream ?? throw new NotSupportedException("Native Tonberry creature requires its current summon-stream playback service."))(frame);
                    task = Read(frame, address);
                    model = State(frame).CreatureModelState ?? throw new NotSupportedException("Native Tonberry stream playback lost its current model owner.");
                }
                NativeBattleEffectOwnedModelStep finished = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resource);
                task = finished.Task;
                model = finished.State;
                if (finished.Completed)
                {
                    task = Next(task.WriteWord(0x134, 60));
                }
                break;
            case 10:
                var requestedEight = NativeBattleTonberry90Programme.RequestPhase763940(frame, 8);
                frame = requestedEight.Frame;
                if (requestedEight.Accepted)
                {
                    task = Next(task.WriteWord(0x134, 10));
                }
                break;
            case 11:
                task = task.WriteWord(0x134, unchecked((ushort)(task.ReadWord(0x134) - 1)));
                if (Word(task, 0x134) <= 0)
                {
                    task = Next(task);
                }
                break;
            case 12:
                task = task.WriteWord(0x13A, unchecked((ushort)(task.ReadWord(0x13A) + 256)));
                if (Word(task, 0x13A) >= 4096)
                {
                    task = Next(task.WriteWord(0x13A, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    frame = frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(0x25A4C10, new byte[] { 255, 127 }) } };
                }
                task = Recolor7678D0(task.WriteWord(0x13C, task.ReadWord(0x13A)), Word(task, 0x13A), 1, services.ReadTransitionColor(frame));
                uint shadowColor = (services.ReadDefaultColor ?? throw new NotSupportedException("Native Tonberry shadow fade requires its canonical default color owner."))(frame);
                for (int channel = 0; channel < 3; channel++)
                {
                    int value = (byte)(shadowColor >> (8 * channel));
                    task = task.WriteByte(0x5C + channel, unchecked((byte)(value - unchecked(value * Word(task, 0x13C)) / 4096)));
                }
                break;
            case 13:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry creature signed phase {unchecked((sbyte)task.ReadByte(0x29))} requires its complete current state contract.");
        }
        for (int axis = 0; axis < 3; axis++)
        {
            int delta = Word(task, 0x124 + 2 * axis) / 16;
            task = task.WriteWord(0x4C + 2 * axis, unchecked((ushort)(task.ReadWord(0x4C + 2 * axis) + delta)));
        }
        frame = Write(frame with { Memory = frame.Memory with { Tonberry90 = State(frame) with { CreatureModelState = model } } }, address, task);
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            NativeBattleTonberry90CreatureDraw draw = services.Draw(frame, address, task, model);
            frame = draw.Frame;
            task = draw.Task;
            NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Native Tonberry creature anchor requires its current decoded pose.");
            NativeBattleVector anchor = NativeBattlePoseMatrices.ReadEffectOwnedBone(resource.PoseSource, pose, ReadWorld(task), 24, 0x9FC);
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, anchor.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), anchor.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), anchor.Z);
            frame = frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(0x25A4C10, xyz) } };
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Write(frame, address, task);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        NativeBattleActionPacketsState RequirePackets() => services.Packets ?? throw new NotSupportedException("Native Tonberry creature spawning requires its actual action packet owner.");
        uint DirectorAddress() => State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry creature requires its current director pointer.");
        bool Spawned(short phase) => unchecked((short)State(frame).ReadWord(DirectorAddress() + 0x40)) >= phase;

        void Loop()
        {
            NativeBattleEffectOwnedModelStep loop = NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, model, resource);
            task = loop.Task;
            model = loop.State;
        }

        void Select(short animation)
        {
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(task, model, resource, animation);
            task = selected.Task;
            model = selected.State;
            frame = Write(frame with { Memory = frame.Memory with { Tonberry90 = State(frame) with { CreatureModelState = model } } }, address, task);
        }
    }

    public static NativeBattleTonberry90TaskStep Advance7670E0(NativeBattleTonberry90Frame frame, uint address, NativeBattleTonberry90ParticleServices? services = null)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (Word(task, 0x24) >= 41)
                {
                    uint director = State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry companion requires its actual director pointer.");
                    task = Next(task.WriteWord(0x78, 64).WriteWord(0x74, 0).WriteDword(0x1C, State(frame).ReadDword(director)).WriteDword(0x20, State(frame).ReadDword(director + 4)));
                    task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + 0x120)));
                }
                break;
            case 1:
                Spawn(0x154EC70, 32);
                task = task.WriteWord(0x78, unchecked((ushort)(task.ReadWord(0x78) + 8))).WriteWord(0x74, unchecked((ushort)(task.ReadWord(0x74) + 1)));
                if (Word(task, 0x78) > 192)
                {
                    task = task.WriteWord(0x78, 192);
                }
                break;
            case 2:
                if (Word(task, 0x24) >= 82)
                {
                    task = Next(task.WriteWord(0x78, 128).WriteWord(0x74, 0));
                }
                break;
            case 3:
                if (Word(task, 0x24) is >= 92 and <= 100)
                {
                    task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) - 16)));
                }
                Spawn(0x154EC90, 128);
                task = task.WriteWord(0x74, unchecked((ushort)(task.ReadWord(0x74) + 1)));
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry companion signed phase {unchecked((sbyte)task.ReadByte(0x29))} requires its complete particle contract.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Write(frame, address, task);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        void Spawn(uint schedule, short divisor)
        {
            NativeBattleTonberry90ParticleServices actual = services ?? throw new NotSupportedException("Native Tonberry companion requires its authenticated particle schedule and task services.");
            frame = Write(frame, address, task);
            var spawned = Spawn7671D0(frame, address, schedule, Word(task, 0x74), divisor, actual);
            frame = spawned.Frame;
            task = Read(frame, address);
            if (!spawned.Active)
            {
                task = Next(task);
                if (schedule == 0x154EC90)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                }
            }
        }
    }

    public static NativeBattleModulePoolTask Recolor7678D0(NativeBattleModulePoolTask task, short coefficient, short mode, uint transitionColor)
    {
        if (coefficient == 0)
        {
            return task.WriteDword(0x58, transitionColor & 0xFFFFFF).WriteByte(0x37, 0);
        }
        if (mode is 1 or 2 or 3)
        {
            task = task.WriteByte(0x5B, 2);
        }
        if (mode == 3)
        {
            int reduction = unchecked(255 * coefficient) / 4096;
            task = task.WriteByte(0x37, (byte)(unchecked((byte)(255 - reduction)) >> 2));
        }
        for (int channel = 0; channel < 3; channel++)
        {
            int value = (byte)(transitionColor >> (8 * channel));
            if (mode == 1)
            {
                value >>= 1;
            }
            int reduction = unchecked(value * coefficient) / 4096;
            task = task.WriteByte(0x58 + channel, unchecked((byte)(value - reduction)));
        }
        return task;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleActionMatrix ReadWorld(NativeBattleModulePoolTask task)
    {
        short[] rotation = new short[9];
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = Word(task, 0x70 + index * 2);
        }
        return new(rotation.ToImmutableArray(), unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
    }

    private static NativeBattleVector Rotate764020(short angle, short magnitude) => NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), angle), new(0, 0, magnitude));
    private static NativeBattleTonberry90Frame SetFrozen767ED0(NativeBattleTonberry90Frame frame, bool frozen)
    {
        NativeBattleTonberry90State state = State(frame);
        uint current = state.ReadDword(0x25A4C18);
        if ((!frozen && current == 1) || (frozen && current == 0))
        {
            uint value = frozen ? 1u : 0u;
            return frame with { Memory = frame.Memory with { Tonberry90 = state.WriteDword(0x25A4C18, value).WriteDword(0x25A2118, value) } };
        }
        return frame;
    }
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleTonberry90State State(NativeBattleTonberry90Frame frame) => frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry creature requires its current module owner.");
    private static NativeBattleModulePoolTask Read(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90Frame Write(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
