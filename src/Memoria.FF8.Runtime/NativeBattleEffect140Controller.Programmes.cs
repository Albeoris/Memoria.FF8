namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect140Controller
{
    private static NativeBattleEffect140Frame AdvanceProgrammes6A6C65(NativeBattleEffect140Frame frame, uint task, int age, int center, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        uint scratch = frame.State.ReadDword(0x2517B28) + 0x878;
        bool paused = frame.State.ReadDword(0x2517AAC) != 0;
        if ((uint)(age - 101) < 50)
        {
            int relative = age - 101;
            if (relative == 0)
            {
                frame = NativeBattleEffect140Programme.Initialize(frame, task + 0x110, 0x11C6C54, 0x178, resources);
                frame = InitializePool(frame, 0x2517AC8, frame.State.ReadDword(0x2517AA4), 0x1C, 64);
            }
            if ((relative & 3) == 0)
            {
                frame = NativeBattleEffect140Particles.Spawn6A7CE0(frame, center, 1024, 2048, 0, 1024, resources, inputs).Frame;
            }
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(-1024) with { X = 0, Y = 0, Z = center };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            NativeBattleEffectArena caller = Caller40(composed, -128, unchecked((short)(relative << 4)), scratch, null);
            frame = NativeBattleEffect140Programme.Advance(frame, task + 0x110, 0x178, caller, resources, inputs).Frame;
        }
        if ((uint)(age - 151) < 44)
        {
            int relative = age - 151;
            if (relative == 0)
            {
                frame = NativeBattleEffect140Programme.Initialize(frame, task + 0x110, 0x11C8FDC, 0x288, resources);
                frame = NativeBattleEffect140Programme.Initialize(frame, task + 0x398, 0x11CCBA0, 0x4E0, resources);
                frame = InitializePool(frame, 0x2517AB8, frame.State.ReadDword(0x2517AA0), 0x18, 4);
            }
            if (!paused && (relative & 3) == 0)
            {
                (frame, uint particle) = Append(frame, 0x2517AB8, 0x6A7BF0);
                if (particle != 0)
                {
                    ushort x;
                    ushort z;
                    ushort y;
                    ushort angle;
                    ushort speed;
                    NativeCrtRandomState random = frame.Random.Draw(out x).Draw(out z).Draw(out y).Draw(out angle).Draw(out speed);
                    frame = frame with { Random = random };
                    NativeBattleModulePoolTask slot = Read(frame, particle).WriteWord(0xC, unchecked((ushort)((x & 511) - 256))).WriteWord(0x10, unchecked((ushort)((z & 511) + center - 456))).WriteWord(0xE, unchecked((ushort)(-512 - (y & 511)))).WriteWord(0x12, 0).WriteWord(0x14, angle).WriteWord(0x16, unchecked((ushort)((speed & 1023) - 512)));
                    frame = Write(frame, particle, slot);
                }
                frame = NativeBattleEffect140Particles.Spawn6A7CE0(frame, center - 200, 512, 1024, 0, 512, resources, inputs).Frame;
            }
            NativeBattleActionMatrix matrix = NativeBattlePandemona291Restoration.Rotation701270(0) with { Z = 4096 };
            NativeBattleEffectArena caller = Caller40(matrix, -1024, unchecked((short)(relative * 8)), scratch, 0);
            if (relative > 30 && !paused)
            {
                NativeBattleCameraPose pose = inputs.ReadCameraPose(frame);
                pose = pose with { Eye = V(pose.Eye.X + 48, pose.Eye.Y + 48, pose.Eye.Z - 96) };
                frame = inputs.Execute(frame, new NativeBattleEffect140PublishCamera(pose, inputs.ReadEyePadding(frame), inputs.ReadTargetPadding(frame)));
            }
            frame = NativeBattleEffect140Programme.Advance(frame, task + 0x110, 0x288, caller, resources, inputs).Frame;
            caller = caller.Write(20, BitConverter.GetBytes(256)).Write(24, BitConverter.GetBytes(512)).Write(0x22, BitConverter.GetBytes((short)32));
            frame = NativeBattleEffect140Programme.Advance(frame, task + 0x398, 0x4E0, caller, resources, inputs).Frame;
        }
        if ((uint)(age - 195) < 60)
        {
            int relative = age - 195;
            for (int index = 0; index < 4; index++)
            {
                frame = NativeBattleEffect140Particles.Spawn6A7CE0(frame, unchecked((int)frame.State.ReadDword(0x2517B10)) - 1000, 2048, 512, 0, 2048, resources, inputs).Frame;
            }
            if (relative == 0)
            {
                frame = NativeBattleEffect140Programme.Initialize(frame, task + 0x110, 0x11CEF60, 0xF8, resources);
            }
            if (!paused)
            {
                frame = Write(frame, task, Read(frame, task).WriteDword(0xBC, unchecked(Read(frame, task).ReadDword(0xBC) - (uint)(relative << 4))));
            }
            frame = NativeBattleEffect140Screen.Fade(frame, 180, 180, 120, relative, 16, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(140, Read(frame, task), unchecked((int)0x7F000000)) : null);
            if (relative > 12)
            {
                int current = relative <= 16 ? 4 - (NativeBattleCameraMath.Sine((relative - 12) << 8) >> 10) : relative - 12;
                frame = NativeBattleEffect140Screen.Fade(frame, 150, 120, 75, current, relative <= 16 ? 4 : 44, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(140, Read(frame, task), unchecked((int)0x7F000000)) : null);
            }
            NativeBattleEffectArena caller = Caller40(NativeBattlePandemona291Restoration.Rotation701270(0) with { Z = 4096 }, -1024, unchecked((short)(-14 * relative)), scratch, 0);
            frame = NativeBattleEffect140Programme.Advance(frame, task + 0x110, 0xF8, caller, resources, inputs).Frame;
            if (relative >= 40)
            {
                uint angle = unchecked((uint)((relative - 40) << 11));
                angle /= 20;
                int sine = NativeBattleCameraMath.Sine(unchecked((int)angle));
                int amplitude = unchecked((int)((long)0x94F2095 * sine >> 32)) >> 4;
                amplitude += (int)((uint)amplitude >> 31);
                frame = NativeBattleEffect140Screen.Scroll(frame, amplitude, relative << 7, 17, resources, inputs);
                frame = TintRecipients(frame, task, 4096 - sine, resources, inputs);
            }
        }
        return frame;
    }

    private static NativeBattleEffectArena Caller40(NativeBattleActionMatrix matrix, short depth, short phase, uint scratch, ushort? padding) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, padding)).Write(0x20, BitConverter.GetBytes(depth)).Write(0x22, BitConverter.GetBytes(phase)).Write(0x24, BitConverter.GetBytes(scratch));
}
