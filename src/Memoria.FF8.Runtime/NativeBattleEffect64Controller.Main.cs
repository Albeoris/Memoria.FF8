using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect64Controller
{
    public static NativeBattleEffect64Step AdvanceRoot6E5260(NativeBattleEffect64Frame frame, uint address, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool priorToggle = task.ReadDword(0x10) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(arena + (uint)(priorToggle ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, task.WriteDword(0x10, priorToggle ? 0u : 1u));
        task = Read(frame, address);
        if (task.ReadWord(0xC) == 1 && task.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, task.WriteByte(0xF, 1));
            frame = InitializeSecondaryBanks(frame);
            uint main;
            (frame, main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect64 exhausted its mandatory main task.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x11F);
            frame = inputs.Execute(frame, new NativeBattleEffect64Vibration4A29A0(VibrationAddress, 1, 0x80, main + 0x10));
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            (frame, survivors) = AdvancePool(frame, 1, resources, inputs);
            uint model = frame.State.ReadDword(CasterGlobal);
            frame = inputs.Execute(frame, new NativeBattleEffect64ApplyOffsetPose662C00(model, checked(arena + MainOffset + 0x18)));
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            (frame, _) = AdvancePool(frame, 3, resources, inputs);
            (frame, _) = AdvancePool(frame, 4, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect64RestorePose508C90(model));
            if (survivors == 0)
            {
                return new(frame, 2);
            }
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(Read(frame, address).ReadWord(0xC) + 1))));
        return new(frame, 0);
    }

    public static NativeBattleEffect64Step AdvanceMain6E53F0(NativeBattleEffect64Frame frame, uint address, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 0xC);
        uint model = frame.State.ReadDword(CasterGlobal);
        if (age >= 0xAA)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect64ModelFlags(model, 0xFFF7, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect64ModelMeshMask(model, 3));
            frame = inputs.Execute(frame, new NativeBattleEffect64ClearGeometryBit15_6E5C90(model));
            frame = inputs.Execute(frame, new NativeBattleEffect64ModelFlags(model, 0xFFFB, 0));
            for (int actor = 0; actor < 3; actor++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect64ModelFlags(ModelAddress(actor), 0xFFFB, 0));
            }
            return new(frame, 2);
        }
        if (age == 0)
        {
            foreach (uint source in ScriptAddresses)
            {
                (frame, _) = SpawnScript6E5D80(frame, model, address + 0x18, source, resources);
            }
        }
        foreach (int first in new[] { 19, 24, 43 })
        {
            int phase = age - first;
            if ((uint)phase < 4)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect64DeliverBatch(NativeBattleModuleRenderCommands.Emit700150(phase, 255, 255, 255, 0, 1, 3, NativeBattlePilotVisualLifetimes.Identity(64, Read(frame, address), first)), CursorGlobal));
            }
        }
        if (age >= 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect64DrawBody6E56A0(model, address + 0x18));
            frame = inputs.Execute(frame, new NativeBattleEffect64ModelFlags(model, ushort.MaxValue, 4));
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect64Sound501330(resources.Sound, 0, 128));
        }
        if (age == 166)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect64StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(0x10))));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1))));
        return new(frame, 0);
    }

    public static (NativeBattleEffect64Frame Frame, uint Address) SpawnScript6E5D80(NativeBattleEffect64Frame frame, uint model, uint offsetRows, uint source, NativeBattleEffect64Resources resources)
    {
        uint address;
        (frame, address) = Append(frame, ScriptDescriptor, 0x6E5DD0);
        if (address == 0)
        {
            throw new InvalidDataException("Effect64 exhausted its mandatory script bank.");
        }
        frame = ClearDwords701200(frame, address + 0x14, 8);
        frame = frame with { State = frame.State.BindScriptSource(resources, source) };
        short bone = BinaryPrimitives.ReadInt16LittleEndian(frame.State.Read(source, 2));
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(0xC, model).WriteDword(0x10, offsetRows).WriteDword(0x14, unchecked((uint)((long)offsetRows + bone * 16))).WriteDword(0x18, source + 2);
        return (Write(frame, address, task), address);
    }
}
