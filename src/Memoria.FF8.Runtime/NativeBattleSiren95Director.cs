namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95VibrationStep(NativeBattleSiren95Frame Frame, short Handle);
public sealed record NativeBattleSiren95DirectorServices(NativeBattleActionPacketsState Packets, Func<NativeBattleSiren95Frame, uint, int, int, NativeBattleSiren95VibrationStep> StartVibration, Func<NativeBattleSiren95Frame, short, NativeBattleSiren95Frame> StopVibration, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> SpawnStage, Func<NativeBattleSiren95Frame, uint, int, int, NativeBattleSiren95Frame> PlaySound, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleSiren95Frame, int, NativeBattleGraphicsModel, NativeBattleSiren95Frame> WriteModel);

public static class NativeBattleSiren95Director
{
    public static NativeBattleSiren95Frame Initialize73A6E0(NativeBattleSiren95Frame frame, Func<int, NativeBattleGraphicsModel> readModel)
    {
        uint director = Director(frame);
        NativeBattleSiren95State state = State(frame).Write(director, new byte[0x54]).Write(unchecked(director + 4), BitConverter.GetBytes((ushort)0xCC0));
        short minX = 0;
        short maxX = 0;
        short minZ = 0;
        short maxZ = 0;
        int count = 0;
        for (int index = 3; index < 7; index++)
        {
            NativeBattleGraphicsModel model = readModel(index);
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            NativeBattleVector point = (model.Transform ?? throw new NotSupportedException("Native Siren boot requires current party positions.")).Position28;
            short x = unchecked((short)point.X);
            short z = unchecked((short)point.Z);
            if (count == 0)
            {
                minX = maxX = x;
                minZ = maxZ = z;
            }
            else
            {
                if (x < minX)
                {
                    minX = x;
                }
                else if (x > maxX)
                {
                    maxX = x;
                }
                if (z < minZ)
                {
                    minZ = z;
                }
                else if (z > maxZ)
                {
                    maxZ = z;
                }
            }
            count++;
        }
        state = state.Write(unchecked(director + 8), BitConverter.GetBytes(unchecked((short)((maxX + minX) / 2))));
        state = state.Write(unchecked(director + 0xA), new byte[2]);
        state = state.Write(unchecked(director + 0xC), BitConverter.GetBytes(unchecked((short)((maxZ + minZ) / 2))));
        state = state.Write(unchecked(director + 8), new byte[2]);
        return frame with { Memory = frame.Memory with { Siren95 = state } };
    }

    public static NativeBattleSiren95TaskStep Advance73A7B0(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95DirectorServices services)
    {
        if (Task().Callback != 0x73A7B0)
        {
            throw new InvalidDataException("Native Siren director requires its actual callback task.");
        }
        Word(0x46, unchecked((ushort)(Value(0x46) + 1)));
        if (Value(0x40) != Value(0x42))
        {
            Word(0x40, Value(0x42));
            Word(0x46, 0);
            frame = services.SpawnStage(frame, address);
        }
        if (Value(0x42) != Value(0x44))
        {
            Word(0x42, Value(0x44));
        }
        switch (unchecked((sbyte)Task().ReadByte(0x29)))
        {
            case 0:
            case 8:
            case 17:
                var released = NativeBattleSiren95Io.Release73A950(frame);
                frame = released.Frame;
                if (released.Released)
                {
                    Next();
                }
                break;
            case 1:
                if (unchecked((short)Task().ReadWord(0x24)) >= 4)
                {
                    NativeBattleSiren95VibrationStep vibration = services.StartVibration(frame, 0x16A3FB0, 1, 128);
                    frame = vibration.Frame;
                    Word(0x52, unchecked((ushort)vibration.Handle));
                    Append(0x257FA90, 0x73A9E0, 0xE0);
                    Next();
                }
                break;
            case 2:
            case 6:
            case 14:
            case 20:
            case 24:
                if (Request(frame, unchecked((short)(Task().ReadByte(0x29) switch { 2 => 1, 6 => 2, 14 => 4, 20 => 7, _ => 8 })), out NativeBattleSiren95Frame requested))
                {
                    frame = requested;
                    if (Task().ReadByte(0x29) == 24)
                    {
                        frame = services.StopVibration(frame, unchecked((short)Value(0x52)));
                    }
                    Next();
                }
                break;
            case 3:
            case 7:
            case 12:
            case 15:
            case 16:
            case 21:
            case 25:
                short required = Task().ReadByte(0x29) switch { 3 => 1, 7 => 2, 12 => 3, 15 => 5, 16 => 6, 21 => 7, _ => 8 };
                if (unchecked((short)Value(0x40)) >= required)
                {
                    switch (Task().ReadByte(0x29))
                    {
                        case 3:
                            frame = services.PlaySound(frame, 0x1533014, 1, 128);
                            break;
                        case 12:
                            SavePartyFlags();
                            break;
                        case 15:
                            RestorePartyFlags();
                            break;
                        case 16:
                            frame = services.PlaySound(frame, 0x1533020, 1, 128);
                            break;
                    }
                    Next();
                }
                break;
            case 4:
            case 18:
                WaitForIo(29);
                break;
            case 5:
                if (unchecked((short)Value(0x46)) >= 30)
                {
                    Next();
                }
                break;
            case 9:
                if (unchecked((short)Value(0x46)) > 25)
                {
                    Store(Task().WriteWord(0x44, 6));
                    uint child = Append(0x258BE20, 0x8DDC30, 0x40);
                    NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
                    frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(child, hierarchy.Read(child).WriteDword(0x34, 0x1533E10).WriteWord(0x38, 0)).Pools } };
                    Next();
                }
                break;
            case 10:
                Store(Task().WriteWord(0x44, unchecked((ushort)(Task().ReadWord(0x44) - 1))));
                if (unchecked((short)Task().ReadWord(0x44)) <= 0)
                {
                    WaitForIo(0);
                }
                break;
            case 11:
                if (Request(frame, 3, out NativeBattleSiren95Frame third))
                {
                    frame = services.PlaySound(third, 0x1533018, 1, 128);
                    Next();
                }
                break;
            case 13:
                if (unchecked((short)Value(0x46)) > 45)
                {
                    Next();
                }
                break;
            case 19:
                if (unchecked((short)Value(0x46)) > 30)
                {
                    Next();
                }
                break;
            case 22:
                if (unchecked((short)Value(0x46)) > 1)
                {
                    Word(0x48, 1);
                    Store(Task().WriteWord(0x44, 8));
                    Next();
                }
                break;
            case 23:
                Store(Task().WriteWord(0x44, unchecked((ushort)(Task().ReadWord(0x44) - 1))));
                if (unchecked((short)Task().ReadWord(0x44)) <= 0)
                {
                    Next();
                }
                break;
            case 26:
                if (Request(frame, 9, out NativeBattleSiren95Frame ninth))
                {
                    frame = ninth;
                    Store(Task().WriteByte(0x26, (byte)(Task().ReadByte(0x26) | 1)));
                    Next();
                }
                break;
            case 27:
                break;
            default:
                throw new NotSupportedException($"Native Siren signed director phase {unchecked((sbyte)Task().ReadByte(0x29))} is unsupported.");
        }
        Store(Task().WriteWord(0x24, unchecked((ushort)(Task().ReadWord(0x24) + 1))));
        return new(frame, (Task().ReadByte(0x26) & 1) != 0 && Task().ReadByte(0x28) == 0);

        NativeBattleModulePoolTask Task() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        ushort Value(uint offset) => State(frame).ReadWord(unchecked(Director(frame) + offset));
        void Word(uint offset, ushort value) => frame = frame with { Memory = frame.Memory with { Siren95 = State(frame).Write(unchecked(Director(frame) + offset), BitConverter.GetBytes(value)) } };
        void Store(NativeBattleModulePoolTask task) => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        void Next() => Store(Task().WriteByte(0x29, unchecked((byte)(Task().ReadByte(0x29) + 1))));
        uint Append(uint pool, uint callback, int size)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Siren append requires its action parameters.");
            int poolIndex = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == pool);
            NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(poolIndex, callback, size, address, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, services.Packets, pointer, header, target));
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native Siren exhausted a required task bank.");
            }
            frame = frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
            return appended.Address;
        }
        void WaitForIo(short threshold)
        {
            uint control = State(frame).ReadDword(0x258EAA0);
            if (State(frame).ReadWord(unchecked(control + 0x166)) == 0)
            {
                Freeze(false);
                Next();
            }
            else if (unchecked((short)Value(0x46)) >= threshold)
            {
                Freeze(true);
            }
        }
        void Freeze(bool enabled)
        {
            uint old = State(frame).ReadDword(0x258FB70);
            if ((!enabled && old == 1) || (enabled && old == 0))
            {
                uint value = enabled ? 1u : 0u;
                frame = frame with { Memory = frame.Memory with { Siren95 = State(frame).WriteDword(0x258FB70, value).WriteDword(0x2585E38, value) } };
            }
        }
        void SavePartyFlags()
        {
            for (int index = 3; index < 7; index++)
            {
                NativeBattleGraphicsModel model = services.ReadModel(index);
                if ((model.Flags0 & 2) != 0)
                {
                    frame = frame with { Memory = frame.Memory with { Siren95 = State(frame).WriteDword(unchecked(Director(frame) + 0x2C + (uint)(4 * (index - 3))), model.Flags0) } };
                    frame = services.WriteModel(frame, index, model with { Flags0 = (ushort)(model.Flags0 | 4) });
                }
            }
        }
        void RestorePartyFlags()
        {
            for (int index = 3; index < 7; index++)
            {
                NativeBattleGraphicsModel model = services.ReadModel(index);
                if ((model.Flags0 & 2) != 0)
                {
                    uint saved = State(frame).ReadDword(unchecked(Director(frame) + 0x2C + (uint)(4 * (index - 3))));
                    frame = services.WriteModel(frame, index, model with { Flags0 = (ushort)(model.Flags0 ^ ((saved ^ model.Flags0) & 4)) });
                }
            }
        }
    }

    public static bool Request(NativeBattleSiren95Frame frame, short phase, out NativeBattleSiren95Frame updated)
    {
        updated = frame;
        uint director = Director(frame);
        ushort requested = State(frame).ReadWord(unchecked(director + 0x44));
        if (requested != State(frame).ReadWord(unchecked(director + 0x40)) || unchecked((short)requested) != phase - 1)
        {
            return false;
        }
        updated = frame with { Memory = frame.Memory with { Siren95 = State(frame).Write(unchecked(director + 0x44), BitConverter.GetBytes(unchecked((ushort)phase))) } };
        return true;
    }

    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren requires its retained module state.");
    private static uint Director(NativeBattleSiren95Frame frame) => State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren director pointer is unknown.");
}
