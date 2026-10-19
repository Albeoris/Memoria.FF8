using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleClash26Controller
{
    private sealed partial class Execution
    {
        public int Advance(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            switch (task.Callback)
            {
                case 0x8887F0:
                    Emitter(address);
                    break;
                case 0x889410:
                    Charge(address);
                    break;
                case 0x888F80:
                    Flash(address);
                    break;
                case 0x8889F0:
                case 0x889080:
                case 0x889170:
                case 0x8897A0:
                    Prim(address);
                    break;
                case 0x888BF0:
                case 0x889250:
                case 0x8898B0:
                case 0x889BF0:
                    Sprite(address);
                    break;
                default:
                    throw new NotSupportedException($"Native Clash callback{task.Callback:X} requires its original task body.");
            }
            return Finish(address);
        }

        private void Emitter(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(Read(address), Models(), inputs.ReadPoseSource);
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            NativeBattleDecodedPose pose = model.SequenceState?.DecodedPose ?? throw new NotSupportedException("Native8887F0 requires its target's actual current pose.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Write(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    Write(address, Next(CopyAnchor(task)));
                    break;
                case 1:
                    if (Word(task, 0x24) >= 4)
                    {
                        Append(address, 0x889080, 0x6C, 4);
                        Append(address, 0x889170, 0x6C, 4);
                        Append(address, 0x889410, 0x60, 2);
                        for (int index = 0; index < 4; index++)
                        {
                            uint dust = Append(address, 0x889250, 0x70, 3);
                            Write(dust, Read(dust).WriteWord(0x68, (ushort)(2 * index)).WriteWord(0x6E, (ushort)((Rand() & 0x3FF) + 0x600)));
                        }
                        if (task.ReadByte(0x2B) == 0)
                        {
                            uint flash = Append(address, 0x888F80, 0x3C, 5);
                            Write(flash, Read(flash).WriteDword(0x30, 0x1610774));
                        }
                        Write(address, Next(Read(address)));
                    }
                    break;
                case 2:
                    if (Word(task, 0x24) > 30)
                    {
                        Write(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native8887F0 signed phase exceeds its four original callbacks.");
            }
            task = Read(address);
            if (task.ReadByte(0x2B) == 0)
            {
                ushort tick = task.ReadWord(0x24);
                if (tick == 4)
                {
                    Execute(new NativeBattleClash26Sound(resources.Sound));
                }
                if (tick is 4 or 40)
                {
                    Burst(address, 0x34, true, 0);
                    Burst(address, 0x35, true, 0x26E);
                    Burst(address, 0x36, true, 0x26E);
                    Burst(address, 0x33, true, 0x26E);
                }
                else if (tick is 17 or 20)
                {
                    Burst(address, tick == 17 ? (byte)0x36 : (byte)0x34, false, 0);
                }
            }
        }

        private void Burst(uint address, byte bone, bool ring, ushort padding)
        {
            int actor = Frame.Effect.Parameters!.Actor78;
            NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), bone, 0x1800);
            uint first = unchecked((ushort)point.X);
            uint second = unchecked((ushort)point.Z | (uint)padding << 16);
            if (ring)
            {
                uint child = Append(address, 0x8889F0, 0x6C, 4);
                Write(child, Read(child).WriteDword(0x1C, first).WriteDword(0x20, second));
            }
            int angle = Rand() & 0xFFF;
            for (int index = 0; index < 8; index++)
            {
                uint child = Append(address, 0x888BF0, 0x70, 3);
                NativeBattleModulePoolTask spark = Read(child).WriteDword(0x1C, first).WriteDword(0x20, second).WriteWord(0x68, (ushort)(Rand() & 3));
                int scale = (Rand() & 0x3FF) + 0x600;
                spark = spark.WriteWord(0x6C, 0x100).WriteWord(0x6A, (ushort)angle).WriteWord(0x6E, (ushort)scale);
                int jitter = Rand() & 0xFF;
                angle = unchecked(angle + 0x200);
                spark = spark.WriteWord(0x6A, (ushort)((unchecked((ushort)(jitter + spark.ReadWord(0x6A))) - 0x80) & 0xFFF));
                Write(child, spark);
            }
        }

        private void Prim(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            int phase = unchecked((sbyte)task.ReadByte(0x29));
            bool ring = task.Callback is 0x8889F0 or 0x8897A0;
            bool growingWave = task.Callback == 0x889080;
            if (phase == 0)
            {
                if (task.Callback == 0x8897A0)
                {
                    task = CopyAnchor(task);
                }
                int scale = ring ? 0x100 : 0x1000;
                uint mesh = ring ? 0x16104B4u : growingWave ? 0x160FA94u : 0x160FF3Cu;
                task = task.WriteDword(0x30, (uint)scale).WriteDword(0x34, (uint)scale).WriteDword(0x38, (uint)scale).WriteDword(0x4C, mesh);
                if (!ring)
                {
                    task = Add(task, 0x1E, -0x300);
                }
                task = Next(task);
            }
            else if (phase == 1)
            {
                if (ring || growingWave)
                {
                    short fade = ring ? resources.Fade[Math.Clamp((int)unchecked((short)(task.ReadWord(0x24) - 3)), 0, 11)] : unchecked((short)(task.ReadWord(0x50) + 0x200));
                    int step = ring ? task.Callback == 0x8889F0 ? 0x300 : 0x200 : 0x400;
                    for (int lane = 0; lane < 3; lane++)
                    {
                        task = task.WriteDword(0x30 + 4 * lane, unchecked(task.ReadDword(0x30 + 4 * lane) + (uint)step));
                    }
                    task = task.WriteWord(0x50, unchecked((ushort)fade));
                    if (fade >= 0x1000)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteWord(0x50, 0x1000));
                    }
                }
                else
                {
                    int scale = unchecked((int)task.ReadDword(0x30) - 0x200);
                    task = task.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked(task.ReadDword(0x34) - 0x200)).WriteDword(0x38, unchecked(task.ReadDword(0x38) - 0x200));
                    if (scale <= 0)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteDword(0x30, 0).WriteDword(0x34, 0).WriteDword(0x38, 0));
                    }
                }
            }
            else if (phase != 2)
            {
                throw new InvalidDataException("Native Clash prim signed phase exceeds its three original callbacks.");
            }
            Write(address, task);
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                NativeBattleStaticMeshResource mesh = resources.Meshes.Single(resource => resource.Address == task.ReadDword(0x4C));
                NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(Word(task, 0x44), Word(task, 0x46), Word(task, 0x48))) with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
                world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native888A50 requires its actual view matrix."), world);
                NativeBattleEffectMeshPacketContext context = new(0xF0, task.ReadDword(0x40), Word(task, 0x50), null, null, 0, false, null, 17, 2);
                NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitBatch(mesh.Bytes, matrix, inputs.Projection, 0xF0, null, (group, face, corner, offset) => group switch
                {
                    0 or 1 => NativeBattleEffectMeshPackets.EmitFlat(face, corner, group == 1, context, inputs.Colors, offset),
                    2 or 3 => NativeBattleEffectMeshPackets.EmitTextured(face, corner, group == 3, false, context, inputs.Colors, offset),
                    4 or 5 => NativeBattleEffectMeshPackets.EmitGouraud(face, corner, group == 5, context, inputs.Colors, offset),
                    _ => NativeBattleEffectMeshPackets.EmitTextured(face, corner, group == 7, true, context, inputs.Colors, offset)
                });
                Execute(new NativeBattleClash26Packets(batch));
            }
        }

        private void Sprite(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            bool spark = task.Callback is 0x888BF0 or 0x8898B0;
            bool walk = task.Callback == 0x889BF0;
            int phase = unchecked((sbyte)task.ReadByte(0x29));
            if (phase == 0)
            {
                if (task.Callback != 0x888BF0)
                {
                    task = CopyAnchor(task);
                }
                if (spark)
                {
                    NativeBattleVector initial = Point(task);
                    task = task.WriteDword(0x58, task.ReadDword(0x1C)).WriteDword(0x5C, task.ReadDword(0x20));
                    int distance = Rand() % Word(task, 0x6C);
                    int angle = Word(task, 0x6A);
                    task = Add(task, 0x1C, unchecked(NativeBattleCameraMath.Sine(angle) * distance) / 4096);
                    task = Add(task, 0x20, unchecked(NativeBattleCameraMath.Cosine(angle) * distance) / 4096);
                    NativeBattleVector moved = Point(task);
                    int divisor = task.Callback == 0x888BF0 ? 8 : 16;
                    short[] speeds = [unchecked((short)((moved.X - initial.X) / divisor)), unchecked((short)((moved.Y - initial.Y) / divisor)), unchecked((short)((moved.Z - initial.Z) / divisor))];
                    for (int lane = 0; lane < 3; lane++)
                    {
                        task = task.WriteWord(0x58 + lane * 2, unchecked((ushort)speeds[lane])).WriteWord(0x60 + lane * 2, unchecked((ushort)(-speeds[lane] / 16)));
                    }
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteDword(0x4C, 0x160F7A8).WriteWord(0x52, 15);
                }
                else if (walk)
                {
                    task = task.WriteDword(0x4C, 0x160F7A8).WriteWord(0x52, 15);
                }
                else
                {
                    task = Add(task, 0x1E, -0x300);
                    int yaw = Rand() & 0xFFF;
                    int roll = Rand() & 0xFFF;
                    int distance = Rand() % 0x180;
                    NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)yaw), (short)roll);
                    NativeBattleVector offset = NativeBattleGfMatrices.Transform460840(rotation, new(0, 0, (short)distance));
                    task = Add(Add(Add(task, 0x1C, offset.X), 0x1E, offset.Y), 0x20, offset.Z);
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteDword(0x4C, 0x160F6CC).WriteWord(0x52, 7).WriteWord(0x54, 0xFE00);
                }
                task = Next(task);
            }
            else if (phase == 1 && !walk)
            {
                task = Add(task, 0x68, -1);
                if (Word(task, 0x68) <= 0)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & 0xFB)));
                }
            }
            else if (phase == (walk ? 1 : 2))
            {
                if (spark)
                {
                    for (int lane = 0; lane < 3; lane++)
                    {
                        task = Add(task, 0x58 + lane * 2, task.ReadWord(0x60 + lane * 2));
                        task = Add(task, 0x1C + lane * 2, task.ReadWord(0x58 + lane * 2));
                    }
                }
                task = Add(task, 0x50, 1);
                if (Word(task, 0x50) > Word(task, 0x52))
                {
                    task = Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
            }
            else if (phase != (walk ? 2 : 3))
            {
                throw new InvalidDataException("Native Clash sprite signed phase exceeds its original callbacks.");
            }
            Write(address, task);
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == task.ReadDword(0x4C));
                ushort index = task.ReadWord(0x50);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(index), Phase = index, Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0), Scale = task.ReadWord(0x6E), DepthOffset = Word(task, 0x54), SpriteFlags = 0 };
                Execute(new NativeBattleClash26Sprite(NativeBattleModulePackets.EmitSprite571DC0(draw, inputs.Projection, inputs.CameraRoll, 17, 2)));
            }
        }

        private void Flash(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (task.ReadDword(0x30) != 0x1610774)
            {
                throw new NotSupportedException("Native888F80 requires its retained original color script.");
            }
            short index = Word(task, 0x38);
            uint entry = resources.FlashScript[index];
            byte marker = (byte)(entry >> 24);
            if (marker == 0xFF)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            }
            else
            {
                if (marker == 0xFE)
                {
                    if (Word(task, 0x24) >= (short)((entry >> 16) & 0xFF))
                    {
                        task = Add(task, 0x38, 1);
                        task = task.WriteDword(0x34, resources.FlashScript[Word(task, 0x38)]);
                    }
                }
                else
                {
                    task = Add(task.WriteDword(0x34, entry), 0x38, 1);
                }
                uint color = task.ReadDword(0x34) & 0xFFFFFF;
                NativeBattleGeometryPrimitive quad = new(7, ImmutableArray.Create(0x05000000u, color | 0x2A000000, 0u, 0x140u, 0xD80000u, 0xD80140u), [0, 0, 0, 0]);
                NativeBattleGeometryPrimitive mode = new(7, ImmutableArray.Create(0x02000000u, 0xE100002Au, 0u), [0, 0, 0, 0]);
                Execute(new NativeBattleClash26Sprite(new([new(quad, 0, 0), new(mode, 0x18, 0)], 0x24)));
            }
            Write(address, task);
        }

        private static NativeBattleVector Point(NativeBattleModulePoolTask task) => new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20));
    }
}

