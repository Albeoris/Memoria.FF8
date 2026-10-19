namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90VibrationStep(NativeBattleTonberry90Frame Frame, short Handle);
public sealed record NativeBattleTonberry90DirectorServices(NativeBattleActionPacketsState Packets, Func<NativeBattleTonberry90Frame, uint, int, int, NativeBattleTonberry90VibrationStep> StartVibration4A29A0, Func<NativeBattleTonberry90Frame, short, NativeBattleTonberry90Frame> StopVibration4A2940, Func<NativeBattleTonberry90Frame, uint, uint, int, NativeBattleTonberry90Frame> InitializeProgram8DD0F0);

public static class NativeBattleTonberry90Director
{
    public static NativeBattleTonberry90Frame Initialize762BB0(NativeBattleTonberry90Frame frame, uint childAddress, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource)
    {
        uint director = Director(frame);
        frame = Put(frame, director, new byte[0x54]);
        int target = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(childAddress).ReadByte(0x2D);
        NativeBattleGraphicsModel model = readModel(target);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Tonberry boot requires its current target transform.");
        NativeBattleVector point = (model.Flags0 & 2) == 0 ? transform.Position28 : NativeBattlePoseMatrices.ReadBone(model, readPoseSource(target), 0xF0, 0);
        frame = Word(frame, unchecked(director + 8), unchecked((ushort)point.X));
        frame = Word(frame, unchecked(director + 0xA), unchecked((ushort)point.Y));
        frame = Word(frame, unchecked(director + 0xC), unchecked((ushort)point.Z));
        frame = Word(frame, unchecked(director + 0xA), 0);
        frame = Dword(frame, director, State(frame).ReadDword(unchecked(director + 8)));
        frame = Dword(frame, unchecked(director + 4), State(frame).ReadDword(unchecked(director + 0xC)));
        frame = Word(frame, unchecked(director + 4), unchecked((ushort)(State(frame).ReadWord(unchecked(director + 4)) + 0xC00)));
        int x = unchecked((short)(State(frame).ReadWord(unchecked(director + 8)) - State(frame).ReadWord(director)));
        int z = unchecked((short)(State(frame).ReadWord(unchecked(director + 0xC)) - State(frame).ReadWord(unchecked(director + 4))));
        frame = Word(frame, unchecked(director + 0x4A), unchecked((ushort)((NativeWorldScriptedPathMath.NativeAngle(x, z) - 0x800) & 0xFFF)));
        return Word(frame, unchecked(director + 0x4E), unchecked((ushort)(readModel(target).Transform ?? throw new NotSupportedException("Native Tonberry boot requires its current target scale WORD.")).Scale38));
    }

    public static NativeBattleTonberry90TaskStep Advance762C60(NativeBattleTonberry90Frame frame, uint address, NativeBattleTonberry90DirectorServices services)
    {
        NativeBattleModulePoolTask task = Task();
        if (task.Callback != 0x762C60)
        {
            throw new InvalidDataException("Native Tonberry director requires its actual callback task.");
        }
        uint director = Director(frame);
        frame = Word(frame, unchecked(director + 0x46), unchecked((ushort)(State(frame).ReadWord(unchecked(director + 0x46)) + 1)));
        if (State(frame).ReadWord(unchecked(director + 0x40)) != State(frame).ReadWord(unchecked(director + 0x42)))
        {
            frame = Word(frame, unchecked(director + 0x40), State(frame).ReadWord(unchecked(director + 0x42)));
            frame = Word(frame, unchecked(director + 0x46), 0);
            frame = Spawn763D60(frame, address, services);
        }
        director = Director(frame);
        if (State(frame).ReadWord(unchecked(director + 0x42)) != State(frame).ReadWord(unchecked(director + 0x44)))
        {
            frame = Word(frame, unchecked(director + 0x42), State(frame).ReadWord(unchecked(director + 0x44)));
        }
        switch (unchecked((sbyte)Task().ReadByte(0x29)))
        {
            case 0:
                NativeBattleTonberry90VibrationStep vibration = services.StartVibration4A29A0(frame, 0x16D7CD0, 1, 128);
                frame = vibration.Frame;
                frame = Word(frame, unchecked(Director(frame) + 0x52), unchecked((ushort)vibration.Handle));
                Append(0x259F098, 0x762DA0, 0x7C);
                Next();
                break;
            case 1:
                Request(1);
                break;
            case 2:
                Wait(1);
                break;
            case 3:
                Wait(3);
                break;
            case 4:
                var released = NativeBattleTonberry90Io.Release763C10(frame);
                frame = released.Frame;
                if (released.Released)
                {
                    Next();
                }
                break;
            case 5:
                if (State(frame).ReadWord(unchecked(State(frame).ReadDword(0x25A3DEC) + 0x166)) == 0)
                {
                    Next();
                }
                break;
            case 6:
                Request(5);
                break;
            case 7:
                if (Spawned(7))
                {
                    Store(Task().WriteWord(0x44, 44));
                    Next();
                }
                break;
            case 8:
                ushort remaining = unchecked((ushort)(Task().ReadWord(0x44) - 1));
                Store(Task().WriteWord(0x44, remaining));
                if (unchecked((short)remaining) <= 0)
                {
                    frame = Word(frame, unchecked(Director(frame) + 0x48), 1);
                    Next();
                }
                break;
            case 9:
                var requested = NativeBattleTonberry90Programme.RequestPhase763940(frame, 9);
                frame = requested.Frame;
                if (requested.Accepted)
                {
                    frame = services.StopVibration4A2940(frame, unchecked((short)State(frame).ReadWord(unchecked(Director(frame) + 0x52))));
                    Next();
                }
                break;
            case 10:
                Store(Task().WriteByte(0x26, (byte)(Task().ReadByte(0x26) | 1)));
                Next();
                break;
            case 11:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry signed director phase {unchecked((sbyte)Task().ReadByte(0x29))} is unsupported.");
        }
        Store(Task().WriteWord(0x24, unchecked((ushort)(Task().ReadWord(0x24) + 1))));
        return new(frame, (Task().ReadByte(0x26) & 1) != 0 && Task().ReadByte(0x28) == 0);

        NativeBattleModulePoolTask Task() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void Store(NativeBattleModulePoolTask value) => frame = WriteTask(frame, address, value);
        void Next() => Store(Task().WriteByte(0x29, unchecked((byte)(Task().ReadByte(0x29) + 1))));
        bool Spawned(short phase) => unchecked((short)State(frame).ReadWord(unchecked(Director(frame) + 0x40))) >= phase;
        void Wait(short phase)
        {
            if (Spawned(phase))
            {
                Next();
            }
        }
        void Request(short phase)
        {
            var requested = NativeBattleTonberry90Programme.RequestPhase763940(frame, phase);
            frame = requested.Frame;
            if (requested.Accepted)
            {
                Next();
            }
        }
        void Append(uint descriptor, uint callback, int size) => frame = AppendTask(frame, descriptor, callback, size, address, services.Packets).Frame;
    }

    public static NativeBattleTonberry90Frame Spawn763D60(NativeBattleTonberry90Frame frame, uint parent, NativeBattleTonberry90DirectorServices services)
    {
        short phase = unchecked((short)State(frame).ReadWord(unchecked(Director(frame) + 0x40)));
        switch (phase)
        {
            case 1:
                frame = Spawn763E60(frame, parent, 0x763EB0, 0x16D7DEC, 0x48C, 0, 0, services.Packets).Frame;
                frame = AppendTask(frame, 0x259F098, 0x7681B0, 0x7C, parent, services.Packets).Frame;
                break;
            case 2:
                var appended = AppendTask(frame, 0x25A3F90, 0x7674E0, 0x148, parent, services.Packets);
                frame = services.InitializeProgram8DD0F0(appended.Frame, appended.Address, 0x1547514, 3);
                frame = AppendTask(frame, 0x259F098, 0x7670E0, 0x7C, parent, services.Packets).Frame;
                break;
            case 6:
                frame = AppendTask(frame, 0x259F098, 0x766FB0, 0x7C, parent, services.Packets).Frame;
                break;
            case 7:
                frame = AppendTask(frame, 0x259F098, 0x7667B0, 0x7C, parent, services.Packets).Frame;
                break;
            case 8:
                frame = AppendTask(frame, 0x259F098, 0x7682A0, 0x7C, parent, services.Packets).Frame;
                break;
        }
        return frame;
    }

    public static (NativeBattleTonberry90Frame Frame, uint Address) Spawn763E60(NativeBattleTonberry90Frame frame, uint parent, uint callback, uint source, short size, ushort argument5, ushort argument6, NativeBattleActionPacketsState packets)
    {
        var appended = AppendTask(frame, 0x25A2B60, callback, 0x520, parent, packets);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(appended.Frame.Effect.Pools).Read(appended.Address);
        task = task.WriteWord(0x80, argument5).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)(int)size)).WriteWord(0x82, argument6);
        return (WriteTask(appended.Frame, appended.Address, task), appended.Address);
    }

    private static (NativeBattleTonberry90Frame Frame, uint Address) AppendTask(NativeBattleTonberry90Frame frame, uint descriptor, uint callback, int size, uint parent, NativeBattleActionPacketsState packets)
    {
        int pool = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Tonberry append requires its current action parameters.");
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Native Tonberry exhausted a required task bank.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    private static NativeBattleTonberry90Frame WriteTask(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static NativeBattleTonberry90State State(NativeBattleTonberry90Frame frame) => frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry director process owner is missing.");
    private static uint Director(NativeBattleTonberry90Frame frame) => State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry current director pointer is unknown.");
    private static NativeBattleTonberry90Frame Put(NativeBattleTonberry90Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(address, bytes) } };
    private static NativeBattleTonberry90Frame Word(NativeBattleTonberry90Frame frame, uint address, ushort value) => Put(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleTonberry90Frame Dword(NativeBattleTonberry90Frame frame, uint address, uint value) => Put(frame, address, BitConverter.GetBytes(value));
}
