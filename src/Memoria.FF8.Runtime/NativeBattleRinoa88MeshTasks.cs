using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa88MeshTasks
{
    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, Func<NativeBattleRinoa88Frame, uint, byte, NativeBattleVector> readBodyBone502170, Func<NativeBattleRinoa88Frame, uint, NativeBattleRinoa88Frame> draw)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        bool cameraSpace = task.Callback == 0x771660;
        if (!cameraSpace && task.Callback != 0x772D90)
        {
            throw new InvalidDataException("Native Rinoa88 mesh task requires its actual156-byte callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(41));
        if (cameraSpace)
        {
            switch (phase)
            {
                case 0:
                    ushort selector = task.ReadWord(152);
                    NativeBattleMoogleCameraState camera = Scene().Camera ?? throw new NotSupportedException("Native7717A0 requires the current original camera owner.");
                    short distance = BinaryPrimitives.ReadInt16LittleEndian(camera.Data.AsSpan().Slice(224));
                    switch (selector)
                    {
                        case 0:
                            task = task.WriteDword(76, IoPointer(328)).WriteWord(82, 2048).WriteWord(32, unchecked((ushort)(2 * distance)));
                            break;
                        case 1:
                            task = task.WriteDword(76, 0x154EF74).WriteWord(82, 1792).WriteWord(28, 96).WriteWord(30, unchecked((ushort)-96)).WriteWord(32, unchecked((ushort)(2 * distance))).WriteWord(124, 20);
                            break;
                        case 2:
                            task = task.WriteDword(76, 0x154F404).WriteWord(82, 1536).WriteWord(28, unchecked((ushort)-96)).WriteWord(30, 96).WriteWord(32, unchecked((ushort)(2 * distance))).WriteWord(124, unchecked((ushort)-32));
                            break;
                        default:
                            throw new NotSupportedException("Native7717A0 requires its original three mesh selectors.");
                    }
                    task = task.WriteWord(154, unchecked((ushort)(16 * task.ReadWord(28)))).WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096);
                    Next();
                    break;
                case 1:
                    Add(154, Signed(124));
                    task = task.WriteWord(28, unchecked((ushort)(Signed(154) / 16)));
                    if (Scene().Word(64) >= 4)
                    {
                        Finish();
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Native771660 signed phase exceeds its three callbacks.");
            }
        }
        else
        {
            switch (phase)
            {
                case 0:
                    task = task.WriteDword(76, IoPointer(324)).WriteDword(48, 2048).WriteDword(56, 2048).WriteDword(52, 4096).WriteWord(82, unchecked((ushort)-512)).WriteWord(80, 4096);
                    NativeBattleVector anchor = readBodyBone502170(frame, task.ReadDword(140), 241);
                    task = task.WriteWord(28, unchecked((ushort)anchor.X)).WriteWord(30, 0).WriteWord(32, unchecked((ushort)anchor.Z));
                    Next();
                    break;
                case 1:
                    Add(80, -1024);
                    if (Signed(80) <= 0)
                    {
                        task = task.WriteWord(80, 0);
                        Next();
                    }
                    break;
                case 2:
                    if (Scene().Word(64) >= 3)
                    {
                        Finish();
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new InvalidDataException("Native772D90 signed phase exceeds its four callbacks.");
            }
            task = task.WriteWord(70, (ushort)((task.ReadWord(70) + 256) & 4095));
            NativeBattleVector point = readBodyBone502170(frame, task.ReadDword(140), 29);
            task = task.WriteDword(52, unchecked((uint)-point.Y));
        }
        Store();
        frame = draw(frame, address);
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        NativeBattleRinoa88SceneMemory Scene() => NativeBattleRinoa88Scene.Read(frame);
        uint IoPointer(int offset) => BinaryPrimitives.ReadUInt32LittleEndian((frame.State.Io ?? throw new NotSupportedException("Native Rinoa88 mesh requires its initialized IO directory.")).Control.AsSpan().Slice(offset));
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Add(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)(Signed(offset) + value)));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Finish()
        {
            task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5));
            Next();
        }
        void Store()
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        }
    }
}
