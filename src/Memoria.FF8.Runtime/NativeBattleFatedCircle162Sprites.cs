namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5FA0E0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll) => AdvanceLinearSprites(frame, pool, slot, inputs, view, projection, cameraRoll, 0x5FA0E0);
    public static NativeBattleFatedCircle162ChildStep Advance5FC850(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll) => AdvanceLinearSprites(frame, pool, slot, inputs, view, projection, cameraRoll, 0x5FC850);
    public static NativeBattleFatedCircle162ChildStep Advance5FCB00(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll) => AdvanceLinearSprites(frame, pool, slot, inputs, view, projection, cameraRoll, 0x5FCB00);

    private static NativeBattleFatedCircle162ChildStep AdvanceLinearSprites(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, uint callback)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool initial = callback == 0x5FA0E0;
        bool smoke = callback == 0x5FC850;
        bool impact = callback == 0x5FCB00;
        uint bank = smoke ? 0x23CE650u : 0x23CEA40u;
        int capacity = smoke ? 40 : 80;
        uint sprite = initial ? 0xDA1D74u : smoke ? 0xDA19B4u : 0xDA219Cu;
        int alive = 0;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        for (int index = 0; index < capacity; index++)
        {
            uint address = checked(bank + (uint)index * 24);
            if ((ReadDword(frame.Staging, address) & 1) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, address + 4);
            NativeBattleVector position = new(ReadWord(frame.Staging, address + 8), ReadWord(frame.Staging, address + 10), ReadWord(frame.Staging, address + 12));
            (frame, bool ended) = DrawSprite(frame, inputs, sprite, unchecked((ushort)age), position, ReadWord(frame.Staging, address + 6), impact ? (ushort)8 : (ushort)0, view, projection, cameraRoll, NativeBattleSquallFinisherVisualIdentity.Particle(162, task, address, sprite));
            if (paused)
            {
                continue;
            }
            age = unchecked((short)(age + 1));
            frame = frame with { Staging = WriteWord(frame.Staging, address + 4, unchecked((ushort)age)) };
            bool release = smoke ? age >= 16 : ended;
            if (release)
            {
                frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                continue;
            }
            if (impact)
            {
                short scale = ReadWord(frame.Staging, address + 6);
                frame = frame with { Staging = WriteWord(frame.Staging, address + 6, unchecked((ushort)(scale - (scale >> 5)))) };
            }
            else
            {
                AdvanceAxis(0, 3);
                if (smoke)
                {
                    AdvanceAxis(1, 3);
                }
                AdvanceAxis(2, smoke ? 4 : 3);
            }
            alive++;

            void AdvanceAxis(int axis, int shift)
            {
                uint component = checked((uint)axis * 2);
                short velocity = ReadWord(frame.Staging, address + 16 + component);
                frame = frame with { Staging = WriteWord(WriteWord(frame.Staging, address + 8 + component, unchecked((ushort)(ReadWord(frame.Staging, address + 8 + component) + velocity))), address + 16 + component, unchecked((ushort)(velocity - (velocity >> shift)))) };
            }
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        short counter = Word(task, 12);
        bool spawn = initial ? counter is >= 1 and <= 2 : smoke ? task.ReadWord(12) <= 22 : task.ReadWord(12) <= 6;
        if (spawn)
        {
            int requested = initial ? 10 : smoke ? 2 : 5;
            for (int created = 0; created < requested; created++)
            {
                int free = FreeParticle(frame.Staging, bank, capacity);
                if (free < 0)
                {
                    break;
                }
                uint address = checked(bank + (uint)free * 24);
                NativeBattleSquallFinisherVisualIdentity.Allocated(162, task, address);
                NativeBattleFileStaging staging = WriteDword(frame.Staging, address, 1);
                staging = WriteWord(staging, address + 4, 0);
                int scale = initial ? Rand() % 1280 + 2304 : smoke ? Rand() % 4096 + 2304 : Rand() % 3584 + 2048;
                staging = WriteWord(staging, address + 6, unchecked((ushort)scale));
                staging = staging.Write(address + 8, task.ReadRawPayloadSpan().Slice(4, 8));
                int x = Word(task, 16) + (initial ? Rand() % 400 - 200 : smoke ? Rand() % 2000 - 1000 : Rand() % 1400 - 700);
                staging = WriteWord(staging, address + 8, unchecked((ushort)x));
                int y = Word(task, 18);
                if (!initial)
                {
                    y += smoke ? -200 - Rand() % 125 : Rand() % 1400 - 700;
                    staging = WriteWord(staging, address + 10, unchecked((ushort)y));
                }
                int z = Word(task, 20) + (initial ? Rand() % 400 - 200 : smoke ? -300 - Rand() % 2000 : Rand() % 1400 - 700);
                staging = WriteWord(staging, address + 12, unchecked((ushort)z));
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)x) - Word(task, 16), initial ? 0 : unchecked((short)y) - Word(task, 18), unchecked((short)z) - Word(task, 20));
                int speed = initial ? Rand() % 90 + 30 : smoke ? Rand() % 120 + 180 : Rand() % 190 + 150;
                staging = WriteWord(staging, address + 16, unchecked((ushort)(unchecked(direction.X * speed) >> 12)));
                staging = WriteWord(staging, address + 18, unchecked((ushort)(unchecked(direction.Y * speed) >> 12)));
                staging = WriteWord(staging, address + 20, unchecked((ushort)(unchecked(direction.Z * speed) >> 12)));
                frame = frame with { Staging = staging };
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) < 16 || alive != 0 ? 0 : 2);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static int FreeParticle(NativeBattleFileStaging staging, uint bank, int count)
    {
        for (int index = 0; index < count; index++)
        {
            if (ReadDword(staging, checked(bank + (uint)index * 24)) == 0)
            {
                return index;
            }
        }
        return -1;
    }

    private static (NativeBattleFatedCircle162Frame Frame, bool Ended) DrawSprite(NativeBattleFatedCircle162Frame frame, NativeBattleFatedCircle162Inputs inputs, uint address, ushort phase, NativeBattleVector position, short scale, ushort flags, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleSearchSpriteResource sprite = frame.Memory.FatedCircle162?.RetainedResources?.Sprites.Single(resource => resource.Address == address) ?? throw new NotSupportedException("Native Fated Circle particles require resources retained by actual entry.");
        NativeBattleRenzokuken160SpritePackets draw = NativeBattleRenzokuken160Drawing.DrawSprite571C80(new(sprite, phase, position, scale, null, flags), projection, view, cameraRoll, pilotIdentity);
        return (inputs.Execute(frame, new NativeBattleFatedCircle162Sprite(draw.Packets)), draw.AnimationEnded);
    }
}
