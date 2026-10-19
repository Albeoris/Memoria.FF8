namespace Memoria.FF8.Runtime;

public static partial class NativeBattleClash26Controller
{
    private sealed partial class Execution
    {
        private void Charge(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            byte actor = task.ReadByte(0x2D);
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native889410 requires its target's actual transform WORDs.");
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    Execute(new NativeBattleClash26Sequence(actor, 6));
                    model = inputs.ReadModel(actor);
                    transform = model.Transform ?? throw new NotSupportedException("Native889480 lost its target transform.");
                    NativeBattleVector position = transform.Position28;
                    uint xy = unchecked((ushort)position.X | (uint)(ushort)position.Y << 16);
                    uint zz = unchecked((ushort)position.Z);
                    ushort padding = unchecked((ushort)transform.Vertical34);
                    zz |= (uint)padding << 16;
                    task = task.WriteDword(0x50, xy).WriteDword(0x54, zz).WriteDword(0x48, xy).WriteDword(0x4C, zz).WriteDword(0x58, model.Flags0);
                    short facing = model.Rotation14 ?? throw new NotSupportedException("Native889480 requires its target yaw WORD.");
                    task = task.WriteWord(0x42, unchecked((ushort)facing));
                    task = task.WriteWord(0x30, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(facing) << 8) / 4096))).WriteWord(0x34, unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(facing) << 8) / 4096)));
                    byte identity = model.ModelId4 ?? throw new NotSupportedException("Native889480 requires its target model BYTE4.");
                    task = task.WriteWord(0x32, identity == 0 ? (ushort)0xFEB0 : identity == 1 ? (ushort)0xFE80 : (ushort)0xFE40).WriteWord(0x3A, 0x30).WriteWord(0x5E, identity == 0 ? (ushort)12 : identity == 1 ? (ushort)14 : (ushort)16);
                    task = Next(task);
                    Execute(new NativeBattleClash26Model(model with { Flags0 = (ushort)(model.Flags0 | 0x1000) }));
                    break;
                case 1:
                    task = Add(task, 0x40, -4096 / Word(task, 0x5E));
                    short spin = Word(task, 0x40);
                    model = model with { Rotation12 = spin <= -4096 ? (short)0 : (short)(spin & 0xFFF) };
                    task = Add(task, 0x32, task.ReadWord(0x3A));
                    task = Add(Add(Add(task, 0x1C, task.ReadWord(0x30)), 0x1E, task.ReadWord(0x32)), 0x20, task.ReadWord(0x34));
                    short ground = transform.Angle36;
                    Write(address, task);
                    if (Word(task, 0x1E) >= ground)
                    {
                        task = task.WriteWord(0x1E, unchecked((ushort)ground));
                        Write(address, task);
                        Execute(new NativeBattleClash26Model(model));
                        Execute(new NativeBattleClash26Sequence(actor, 6));
                        model = inputs.ReadModel(actor);
                        Append(address, 0x8897A0, 0x6C, 4);
                        ushort angle = (ushort)(Rand() & 0xFFF);
                        LandingSpark(address, 0, angle);
                        LandingSpark(address, 2, unchecked((ushort)(angle + 0x500)));
                        LandingSpark(address, 4, unchecked((ushort)(angle + 0xA00)));
                        task = Next(Read(address));
                    }
                    NativeBattleVector offset = NativeBattlePoseMatrices.RotateVector(new(model.Rotation12 ?? throw new NotSupportedException("Native889580 lost target pitch."), model.Rotation14 ?? throw new NotSupportedException("Native889580 requires target yaw."), model.Rotation16 ?? throw new NotSupportedException("Native889580 requires target roll.")), new(0, 0x200, 0));
                    NativeBattleVector targetPosition = new(unchecked((short)(task.ReadWord(0x1C) + offset.X)), unchecked((short)(task.ReadWord(0x1E) + offset.Y - 0x200)), unchecked((short)(task.ReadWord(0x20) + offset.Z)));
                    Execute(new NativeBattleClash26Model(model with { Transform = transform with { Position28 = targetPosition } }));
                    break;
                case 2:
                    if ((model.Sequence ?? throw new NotSupportedException("Native889A70 requires target animation BYTE0.")) != 6)
                    {
                        Execute(new NativeBattleClash26Sequence(actor, 0x1A));
                        model = inputs.ReadModel(actor);
                        short reverse = unchecked((short)(((model.Rotation14 ?? throw new NotSupportedException("Native889A70 requires target facing.")) - 0x800) & 0xFFF));
                        task = Next(task.WriteWord(0x5C, 16).WriteWord(0x30, unchecked((ushort)((Word(task, 0x48) - transform.Position28.X) / 16))).WriteWord(0x34, unchecked((ushort)((Word(task, 0x4C) - transform.Position28.Z) / 16))));
                        Execute(new NativeBattleClash26Model(model with { Rotation14 = reverse }));
                    }
                    break;
                case 3:
                    NativeBattleVector moved = transform.Position28 with { X = unchecked((short)(transform.Position28.X + task.ReadWord(0x30))), Z = unchecked((short)(transform.Position28.Z + task.ReadWord(0x34))) };
                    task = Add(task, 0x5C, -1);
                    model = model with { Transform = transform with { Position28 = moved } };
                    if (Word(task, 0x5C) <= 0)
                    {
                        ushort bit = (ushort)((task.ReadWord(0x58) ^ model.Flags0) & 0x1000);
                        model = model with { Flags0 = (ushort)(model.Flags0 ^ bit) };
                        Execute(new NativeBattleClash26Model(model));
                        Execute(new NativeBattleClash26Sequence(actor, model.SequenceState?.Default2 ?? throw new NotSupportedException("Native889B00 requires its target idle BYTE2.")));
                        model = inputs.ReadModel(actor);
                        moved = moved with { X = Word(task, 0x48), Z = Word(task, 0x4C) };
                        model = model with { Rotation14 = unchecked((short)(((model.Rotation14 ?? throw new NotSupportedException("Native889B00 requires target facing.")) - 0x800) & 0xFFF)), Transform = transform with { Position28 = moved } };
                        Execute(new NativeBattleClash26Model(model));
                        NativeBattleEffectParameters parameters = Frame.Effect.Parameters!;
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native889B00 requires its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        Execute(new NativeBattleClash26Contact(contact));
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    else
                    {
                        Execute(new NativeBattleClash26Model(model));
                    }
                    Write(address, task);
                    if ((task.ReadByte(0x24) & 3) == 0)
                    {
                        uint dust = Append(address, 0x889BF0, 0x70, 3);
                        Write(dust, Read(dust).WriteWord(0x6E, (ushort)((Rand() & 0x1FF) + 0x400)));
                        task = Read(address);
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native889410 signed phase exceeds its five original callbacks.");
            }
            Write(address, task);
        }

        private void LandingSpark(uint parent, ushort delay, ushort angle)
        {
            uint child = Append(parent, 0x8898B0, 0x70, 3);
            NativeBattleModulePoolTask spark = Read(child).WriteWord(0x68, delay);
            int scale = (Rand() & 0x3FF) + 0x600;
            spark = spark.WriteWord(0x6C, 0xC0).WriteWord(0x6E, (ushort)scale).WriteWord(0x6A, angle);
            spark = spark.WriteWord(0x6A, (ushort)(((Rand() & 0x1FF) + angle) & 0xFFF));
            Write(child, spark);
        }
    }
}
