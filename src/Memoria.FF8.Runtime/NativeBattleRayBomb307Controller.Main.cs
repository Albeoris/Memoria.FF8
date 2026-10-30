using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static NativeBattleRayBomb307TaskStep AdvanceMain(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        byte caster = checked((byte)frame.State.ReadDword(0x2242574));
        NativeBattleActionMatrix world = inputs.ReadModel(caster).GeometryWorldMatrix ?? throw new NotSupportedException("Ray-Bomb requires its caster's current world matrix.");
        NativeBattleActionMatrix bone = inputs.ReadCurrentBoneMatrix(frame, caster, 0xF0).ReadMatrix();
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bone);
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(composed, new(3500, 0, -1000));
        frame = frame with { State = frame.State.WriteWord(0x2242580, unchecked((ushort)(composed.X + offset.X))).WriteWord(0x2242582, unchecked((ushort)(composed.Y + offset.Y))).WriteWord(0x2242584, unchecked((ushort)(composed.Z + offset.Z))) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307BindCaster(caster, SavedBindingAddress, CasterDatAddress));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307RingSequence(caster, 3));
        }
        if (age == 15 || age == 32)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags(caster, age == 32 ? (ushort)8 : (ushort)0, age == 15 ? (ushort)8 : (ushort)0));
        }
        if (age == 0)
        {
            NativeBattleRayBomb307PointStep center = inputs.ReadTargetCenter5020A0(frame, Parameters(frame).TargetMask7A);
            frame = center.Frame with { State = center.Frame.State.WriteWord(0x2242568, unchecked((ushort)center.Position.X)).WriteWord(0x224256A, unchecked((ushort)center.Position.Y)).WriteWord(0x224256C, unchecked((ushort)center.Position.Z)) };
            int scale = inputs.ReadModel(caster).Transform?.Scale38 ?? throw new NotSupportedException("Ray-Bomb requires its caster's current scale.");
            frame = Write(frame, address, Read(frame, address).WriteWord(0x16, unchecked((ushort)(unchecked(scale * 1900) >> 12))));
        }
        if (age == 12)
        {
            for (int index = 0; index < 2; index++)
            {
                (frame, uint child) = Append(frame, 1, 0x5A24D0);
                frame = DrawRandom(frame, out ushort delay);
                frame = DrawRandom(frame, out ushort angle);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(delay % 6 + index)).WriteWord(0x16, Read(frame, address).ReadWord(0x16)).WriteWord(0x1C, 2560).WriteWord(0x1E, 64).WriteWord(0x20, 0).WriteWord(0x18, (ushort)(angle % 800 + 400)));
            }
        }
        if (age is >= 12 and < 30)
        {
            for (int bank = 0; bank < 2; bank++)
            {
                for (int birth = 0; birth < 2; birth++)
                {
                    int index = FindParticle(frame, 100);
                    if (index is < 0 or >= 80)
                    {
                        continue;
                    }
                    int row = index * 24;
                    frame = DrawRandom(frame, out ushort scale);
                    frame = SlotDword(frame, row, 1);
                    frame = SlotWord(frame, row + 4, 0);
                    frame = SlotWord(frame, row + 6, (short)(scale % 768 + 512));
                    frame = frame with { State = frame.State with { Particles = NativeBattleEffectNodeContextMemory.Copy(frame.State.ReadView(0x2242580, 8), 0, frame.State.Particles, row + 8, 8) } };
                    NativeBattleModuleAnchor anchor = resources.BirthAnchors[bank];
                    NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(world, new(anchor.X, anchor.Y, anchor.Z));
                    frame = SlotWord(frame, row + 8, unchecked((short)(SlotWord(frame, row + 8) + point.X)));
                    frame = SlotWord(frame, row + 10, unchecked((short)(SlotWord(frame, row + 10) + point.Y)));
                    frame = SlotWord(frame, row + 12, unchecked((short)(SlotWord(frame, row + 12) + point.Z)));
                    NativeBattleModuleAnchor direction = resources.BirthDirections[bank];
                    point = NativeBattleGfMatrices.RotateShort56C4F0(world, new(direction.X, direction.Y, direction.Z));
                    var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(point.X, point.Y, point.Z);
                    frame = DrawRandom(frame, out ushort speed);
                    int magnitude = speed % 100 + 60;
                    frame = SlotWord(frame, row + 16, unchecked((short)(normalized.X * magnitude >> 12)));
                    frame = SlotWord(frame, row + 18, unchecked((short)(normalized.Y * magnitude >> 12)));
                    frame = SlotWord(frame, row + 20, unchecked((short)(normalized.Z * magnitude >> 12)));
                    if (inputs.CapturePresentation)
                    {
                        NativeBattleRayBomb307Visuals.Born(frame, index);
                    }
                }
            }
        }
        if (age == 27)
        {
            (frame, uint child) = Append(frame, 1, 0x5A23E0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(0x16, Read(frame, address).ReadWord(0x16)).WriteWord(0x1C, 0x3333).WriteWord(0x1E, 0x3F0));
        }
        if (age == 37)
        {
            (frame, uint child) = Append(frame, 1, 0x5A1EE0);
            NativeBattleVector delta = new(unchecked((short)(frame.State.ReadWord(0x2242568) - frame.State.ReadWord(0x2242580))), 0, unchecked((short)(frame.State.ReadWord(0x224256C) - frame.State.ReadWord(0x2242584))));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(new(0, -1024, 0)), delta);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(0x10, unchecked((ushort)(rotated.X + frame.State.ReadWord(0x2242580)))).WriteWord(0x12, frame.State.ReadWord(0x224256A)).WriteWord(0x14, unchecked((ushort)(rotated.Z + frame.State.ReadWord(0x2242584)))).WriteWord(0x18, 70).WriteWord(0x1A, 160).WriteWord(0x20, 2048));
        }
        if (age is >= 62 and < 68)
        {
            for (int index = 0; index < 5; index++)
            {
                frame = DrawRandom(frame, out ushort countDraw);
                int count = (countDraw & 1) + 1;
                for (int birth = 0; birth < count; birth++)
                {
                    (frame, uint child) = Append(frame, 1, 0x5A1D90);
                    NativeBattleModulePoolTask born = CopyTask(Read(frame, child), 0x10, frame.State.ReadView(0x2242568, 8), 8).WriteWord(12, 0).WriteWord(0x12, 0);
                    frame = DrawRandom(frame, out ushort delay);
                    frame = DrawRandom(frame, out ushort x);
                    frame = DrawRandom(frame, out ushort z);
                    frame = DrawRandom(frame, out ushort angle);
                    frame = DrawRandom(frame, out ushort scale);
                    frame = DrawRandom(frame, out ushort life);
                    born = born.WriteWord(14, (ushort)((delay & 7) + count)).WriteWord(0x10, unchecked((ushort)(born.ReadWord(0x10) + x % 800 + resources.SpawnX[index] - 400))).WriteWord(0x14, unchecked((ushort)(born.ReadWord(0x14) + z % 800 + resources.SpawnZ[index] - 300))).WriteWord(0x18, (ushort)(angle & 4095)).WriteWord(0x1C, (ushort)(scale % 3584 + 2048)).WriteWord(0x20, (ushort)((life & 3) + 4));
                    frame = Write(frame, child, born);
                }
            }
        }
        if (age == 84)
        {
            (frame, uint child) = Append(frame, 1, 0x5A1C50);
            frame = Write(frame, child, CopyTask(Read(frame, child), 0x10, frame.State.ReadView(0x2242568, 8), 8).WriteWord(12, 0).WriteWord(0x1C, 0x3924).WriteWord(0x20, 0).WriteWord(0x22, 0x1C92));
        }
        if (age is >= 84 and < 88)
        {
            for (int index = 5; index < 10; index++)
            {
                frame = DrawRandom(frame, out ushort countDraw);
                int count = (countDraw & 1) + 2;
                for (int birth = 0; birth < count; birth++)
                {
                    (frame, uint child) = Append(frame, 1, 0x5A1AE0);
                    NativeBattleModulePoolTask born = CopyTask(Read(frame, child), 0x10, frame.State.ReadView(0x2242568, 8), 8).WriteWord(12, 0);
                    frame = DrawRandom(frame, out ushort life);
                    frame = DrawRandom(frame, out ushort x);
                    frame = DrawRandom(frame, out ushort y);
                    frame = DrawRandom(frame, out ushort z);
                    frame = DrawRandom(frame, out ushort velocity);
                    frame = DrawRandom(frame, out ushort shape);
                    frame = DrawRandom(frame, out ushort scale);
                    frame = DrawRandom(frame, out ushort angleSpeed);
                    born = born.WriteWord(14, (ushort)((life & 7) + birth + 11)).WriteWord(0x10, unchecked((ushort)(born.ReadWord(0x10) + x % 3600 + resources.SpawnX[index] - 1800))).WriteWord(0x12, unchecked((ushort)-(y % 60))).WriteWord(0x14, unchecked((ushort)(born.ReadWord(0x14) + z % 3600 + resources.SpawnZ[index] - 1800))).WriteWord(0x16, unchecked((ushort)(-30 - velocity % 500))).WriteWord(0x18, (ushort)(shape & 1)).WriteWord(0x1A, (ushort)(scale % 1280 + 256)).WriteWord(0x1C, (ushort)(count * 400));
                    born = born.WriteWord(0x1E, born.ReadWord(0x16)).WriteWord(0x20, (ushort)(angleSpeed % 100 + 40)).WriteWord(0x22, (ushort)((angleSpeed % 100 + 40) >> 2));
                    frame = Write(frame, child, born);
                }
            }
        }
        if (age == 62)
        {
            (frame, uint child) = Append(frame, 0, TextureCallback);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 85)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Ray-Bomb requires its actual contact pointer.");
            for (int index = 0; index < header.ReadByte(16); index++)
            {
                (frame, uint child) = Append(frame, 1, 0x5A25E0);
                frame = DrawRandom(frame, out ushort direction);
                frame = DrawRandom(frame, out ushort distance);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, inputs.Packets.ReadContact(first + index).Primary.ActorIndex).WriteWord(0x1A, (ushort)index).WriteWord(0x18, (ushort)((direction % 3 + 1) << 12)).WriteWord(0x20, unchecked((ushort)(-3500 - distance % 9000))));
            }
        }
        if (age <= 8 || age >= 127)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307StageFade(unchecked((short)((age <= 8 ? age : 135 - age) << 8)), 0));
        }
        if (age == 10 || age == 81)
        {
            NativeBattleRayBomb307SoundResource sound = resources.Sounds[age == 10 ? 0 : 1];
            frame = inputs.Execute(frame, new NativeBattleRayBomb307SpatialSound(sound.Address, sound.Value, age == 10 ? 0u : 0x100u, frame.State.ReadView(age == 10 ? 0x2242580u : 0x2242568u, 8)));
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) > 135)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307StageFade(0, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }
}
