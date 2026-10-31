using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    private static (NativeBattleEffect303Frame Frame, int Result) AdvanceRoot(NativeBattleEffect303Frame frame, uint address, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(arena + (uint)(alternate ? FirstPacketOffset : SecondPacketOffset)));
        frame = Write(frame, address, Read(frame, address).WriteDword(16, alternate ? 0u : 1u));
        int age = Signed(root, 12);
        if (age == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, Pools[1].Descriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect303 requires its mandatory main task after native pool reset.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x9ED);
            NativeBattleEffect303VibrationStep vibration = inputs.StartVibration(frame);
            frame = vibration.Frame;
            frame = Write(frame, main, Read(frame, main).WriteDword(16, unchecked((uint)vibration.Handle)).WriteDword(28, main + 32));
            var workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength).Initialize7016B0(resources.MainProgramme, MainProgrammeWorkspaceLength);
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + MainProgrammeWorkspaceOffset, workspace);
            if (frame.CapturePresentation)
            {
                NativeBattleEffect32Presentation.Allocated(frame.Effect, main, main + MainProgrammeWorkspaceOffset, 0);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect303InitializeIo534110(IoOffset));
        }
        int mainSurvivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, mainSurvivors) = AdvancePool(frame, 1, resources, inputs);
            foreach (int index in new[] { 2, 3, 4 })
            {
                (frame, _) = AdvancePool(frame, index, resources, inputs);
            }
        }
        if (Read(frame, address).ReadByte(15) != 0 && mainSurvivors == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect303AdvanceIo534210());
            NativeBattleEffect303IoPoll poll = inputs.PollIo5342C0(frame, 0);
            frame = poll.Frame;
            if (poll.Busy)
            {
                return (frame, 0);
            }
            uint target = frame.State.ReadDword(TargetGlobal);
            frame = inputs.Execute(frame, new NativeBattleEffect303Activate487670(target));
            return (inputs.Execute(frame, new NativeBattleEffect303ModelFields(target, 4, 0)), 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleEffect303Frame Frame, int Result) AdvanceMain(NativeBattleEffect303Frame frame, uint address, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if (age >= 41)
        {
            return (inputs.Execute(frame, new NativeBattleEffect303QueueFile5341D0(360, frame.State.ReadDword(CursorGlobal), 1)), 2);
        }
        uint target = frame.State.ReadDword(TargetGlobal);
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect303ModelFields(target, 4, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect303ModelFields(caster, 0, 4));
        }
        frame = DrawFragments66B120(frame, address, resources, inputs);
        if ((uint)age < 22)
        {
            if (age == 0)
            {
                NativeBattleEffect303PointStep muzzle = ReadMuzzle66C000(frame, NativeBattleEffectArena.Clear().Write(0, resources.MuzzlePoints16.AsSpan().Slice(0, 8)), TaskView(Read(frame, address), 20, 8), inputs);
                frame = Write(muzzle.Frame, address, CopyTask(Read(muzzle.Frame, address), 20, muzzle.Point8, 8).WriteWord(26, 0));
            }
            NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
            caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(Read(frame, address), 20, 8), 0, caller, 0, 8).Write(20, new byte[4]).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(unchecked(frame.State.ReadDword(ArenaGlobal) + ScratchOffset)));
            (frame, _) = DrawProgramme303(frame, address, MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength, resources.MainProgramme, caller, resources, inputs);
            if (age < 2)
            {
                for (int index = 0; index < 32; index++)
                {
                    (frame, uint particle) = Append(frame, Pools[3].Descriptor, 0x66C100);
                    if (particle == 0)
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask task = CopyTask(Read(frame, particle), 12, TaskView(Read(frame, address), 20, 8), 8);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawCrt303(frame, out int random);
                        short velocity = unchecked((short)((random & 511) - 256));
                        task = task.WriteWord(20 + axis * 2, unchecked((ushort)velocity)).WriteWord(28 + axis * 2, unchecked((ushort)(-velocity / 12)));
                    }
                    frame = Write(frame, particle, task);
                }
            }
        }
        uint relative = unchecked((uint)(age - 24));
        if (relative < 16)
        {
            byte color = unchecked((byte)(NativeBattleCameraMath.Sine(unchecked((int)(relative << 7))) >> 6));
            NativeBattleEffectArena rectangle = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)704)).Write(2, BitConverter.GetBytes((short)256)).Write(4, BitConverter.GetBytes((short)128)).Write(6, BitConverter.GetBytes((short)128));
            NativeBattleEffectArena rgb = NativeBattleEffectArena.Clear().Write(0, new byte[] { color, color, color, 0 });
            frame = inputs.Execute(frame, new NativeBattleEffect303DrawBody188(target, rectangle, 0x3DD4, age * 2, age * 4, rgb));
            frame = inputs.Execute(frame, new NativeBattleEffect303ModelFields(target, 0, 4));
        }
        else if (relative < 41)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect303ModelFields(target, 4, 0));
        }
        frame = AdvanceFragments66B3A0(frame, address);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect303Sound501330(resources.Sound, 0, 128));
        }
        else if (age == 37)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect303StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        if (age == 6)
        {
            NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            NativeBattleEffect303PointStep muzzle = ReadMuzzle66C000(frame, NativeBattleEffectArena.Clear().Write(0, resources.MuzzlePoints16.AsSpan().Slice(8, 8)), output, inputs);
            frame = muzzle.Frame;
            short x = BinaryPrimitives.ReadInt16LittleEndian(muzzle.Point8.Read(0, 2));
            short z = BinaryPrimitives.ReadInt16LittleEndian(muzzle.Point8.Read(4, 2));
            for (int index = 0; index < 8; index++)
            {
                (frame, uint sprite) = Append(frame, Pools[4].Descriptor, 0x66C080);
                if (sprite == 0)
                {
                    continue;
                }
                frame = DrawCrt303(frame, out int rx);
                frame = DrawCrt303(frame, out int ry);
                frame = DrawCrt303(frame, out int rz);
                NativeBattleModulePoolTask task = Read(frame, sprite).WriteWord(12, unchecked((ushort)(x + (rx & 511) - 256))).WriteWord(14, unchecked((ushort)-(ry & 511))).WriteWord(16, unchecked((ushort)(z + (rz & 511) - 256))).WriteWord(18, 0);
                frame = Write(frame, sprite, task);
            }
        }
        if (age == 0)
        {
            int[] scales = [5000, 4096, 4096, 3000];
            for (int index = 0; index < 4; index++)
            {
                NativeBattleEffectArena velocity = NativeBattleEffectArena.Clear().Write(0, resources.ChildPoints32.AsSpan().Slice(index * 8, 8));
                frame = SpawnProgramme66C1C0(frame, velocity, MeshAddresses[index], scales[index], resources, inputs);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleEffect303Frame DrawCrt303(NativeBattleEffect303Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
        value = sample;
        return frame with { Random = random };
    }
}
