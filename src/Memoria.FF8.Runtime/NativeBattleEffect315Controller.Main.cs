using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect315Controller
{
    public static (NativeBattleEffect315Frame Frame, int Result) AdvanceMain(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 0xC);
        uint caster = ModelAddress(Caster(frame));
        if (age == 0)
        {
            NativeBattleEffect315PointStep point = inputs.ReadAnchor502170(frame, caster, 0xF1, 0x800, TaskView(Read(frame, address), 0x10, 8));
            frame = Write(point.Frame, address, CopyTask(Read(point.Frame, address), 0x10, point.Point8, 8));
            frame = inputs.Execute(frame, new NativeBattleEffect315ModelFlags8(caster, 0, 0x8000));
            frame = inputs.Execute(frame, new NativeBattleEffect315ModelFlags(caster, 4, 0));
            uint next = ModelAddress(Caster(frame) + 1);
            frame = inputs.Execute(frame, new NativeBattleEffect315ModelFlags(next, 0, 4));
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, caster);
            byte saved = (model.SequenceState ?? throw new NotSupportedException("Effect315 requires its current mutable sequence header.")).Default2;
            frame = Write(frame, address, Read(frame, address).WriteWord(0xE, saved));
            frame = inputs.Execute(frame, new NativeBattleEffect315ModelDefaultSequence2(caster, model.Sequence ?? throw new NotSupportedException("Effect315 requires its current sequence header BYTE0.")));
            frame = inputs.Execute(frame, new NativeBattleEffect315ModelInitial20XZ(next, Signed(Read(frame, address), 0x10), Signed(Read(frame, address), 0x14)));
            NativeBattleEffect315VibrationStep vibration = inputs.StartVibration(frame);
            frame = Global(vibration.Frame, VibrationGlobal, unchecked((uint)vibration.Handle));
        }
        NativeBattleModulePoolTask main = Read(frame, address);
        if (age == 1)
        {
            (frame, uint child) = Spawn(frame, 0x59ADB0);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0xE, unchecked((ushort)Caster(frame))));
        }
        if (age == 82)
        {
            (frame, uint child) = Spawn(frame, 0x59AEF0);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0xE, unchecked((ushort)(Caster(frame) + 1))));
        }
        if (age == 13)
        {
            for (int index = 0; index < 3; index++)
            {
                (frame, uint child) = Spawn(frame, 0x599470);
                frame = Random315(frame, out int random);
                NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x10, TaskView(main, 0x10, 8), 8).WriteWord(0xC, 0).WriteWord(0xE, (ushort)(random % 10 + index)).WriteWord(0x18, (ushort)index).WriteWord(0x1A, (ushort)(1 << index));
                frame = Write(frame, child, task);
            }
        }
        if (age is 28 or 95)
        {
            (frame, uint child) = Spawn(frame, age == 28 ? 0x599860u : 0x599A90u);
            frame = Write(frame, child, CopyTask(Read(frame, child), 0x10, TaskView(main, 0x10, 8), 8).WriteWord(0xC, 0));
        }
        if (age == 32)
        {
            (frame, uint child) = Spawn(frame, 0x599CC0);
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, caster);
            short value = unchecked((short)((model.GeometryBounds52 ?? throw new NotSupportedException("Effect315 requires its current caster minimum Y WORD54.")).Minimum.Y - 300));
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0xE, unchecked((ushort)Caster(frame))).WriteWord(0x10, main.ReadWord(0x10)).WriteWord(0x12, 0).WriteWord(0x14, main.ReadWord(0x14)).WriteWord(0x20, unchecked((ushort)value)).WriteWord(0x22, unchecked((ushort)(value / 70))));
        }
        if (age == 82)
        {
            (frame, uint child) = Spawn(frame, 0x59A3E0);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0xE, unchecked((ushort)(Caster(frame) + 1))).WriteWord(0x10, main.ReadWord(0x10)).WriteWord(0x12, unchecked((ushort)-2000)).WriteWord(0x14, main.ReadWord(0x14)).WriteWord(0x20, unchecked((ushort)-2000)).WriteWord(0x22, 25));
        }
        if (age >= 30 && age <= 82 && age % 4 == 1)
        {
            (frame, uint child) = Spawn(frame, 0x59A500);
            frame = Random315(frame, out int angle);
            frame = Random315(frame, out int speed);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0x10, main.ReadWord(0x10)).WriteWord(0x12, unchecked((ushort)-100)).WriteWord(0x14, main.ReadWord(0x14)).WriteWord(0x18, (ushort)(angle % 4096)).WriteWord(0x1A, (ushort)(speed % 48 + 16)).WriteWord(0x1C, 384).WriteWord(0x1E, 384).WriteWord(0x20, 2304));
        }
        if (age == 30)
        {
            (frame, uint child) = Spawn(frame, 0x59A670);
            frame = Random315(frame, out int angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0x10, main.ReadWord(0x10)).WriteWord(0x12, 0).WriteWord(0x14, main.ReadWord(0x14)).WriteWord(0x18, (ushort)(angle % 4096)).WriteWord(0x1C, 4096).WriteWord(0x1E, 79).WriteWord(0x20, 8192).WriteWord(0x22, 148));
            (frame, child) = Spawn(frame, 0x59A7F0);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0x10, main.ReadWord(0x10)).WriteWord(0x12, 0).WriteWord(0x14, main.ReadWord(0x14)));
        }
        if (age >= 112 && age <= 182 && age % 3 == 1)
        {
            (frame, uint child) = Spawn(frame, 0x59AAF0);
            frame = Random315(frame, out int kind);
            frame = Random315(frame, out int x);
            frame = Random315(frame, out int z);
            frame = Random315(frame, out int angle);
            frame = Random315(frame, out int size);
            frame = Write(frame, child, Read(frame, child).WriteWord(0xC, 0).WriteWord(0xE, (ushort)(kind % 4)).WriteWord(0x10, unchecked((ushort)(Signed(main, 0x10) + x % 1700 - 850))).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)(Signed(main, 0x14) + z % 1700 - 850))).WriteWord(0x18, (ushort)(angle % 4096)).WriteWord(0x1C, (ushort)(size % 2560 + 2048)).WriteWord(0x20, 2560).WriteWord(0x22, 2560));
        }
        if (age == 126)
        {
            (frame, uint first) = Spawn(frame, 0x59AC70);
            frame = Random315(frame, out int angle);
            frame = Random315(frame, out int speed);
            frame = Write(frame, first, Read(frame, first).WriteWord(0xC, 0).WriteWord(0x10, main.ReadWord(0x10)).WriteWord(0x12, 0).WriteWord(0x14, main.ReadWord(0x14)).WriteWord(0x18, (ushort)(angle % 4096)).WriteWord(0x1A, unchecked((ushort)(-20 - speed % 45))).WriteWord(0x1C, 6656));
            (frame, uint second) = Spawn(frame, 0x59AC70);
            frame = Random315(frame, out speed);
            frame = Write(frame, second, CopyTask(Read(frame, second), 0x10, TaskView(Read(frame, first), 0x10, 8), 8).WriteWord(0xC, 0).WriteWord(0x18, unchecked((ushort)(Read(frame, first).ReadWord(0x18) + 512))).WriteWord(0x1A, unchecked((ushort)(-20 - speed % 45))).WriteWord(0x1C, 6656));
        }
        if (age == 112)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect315Contact506690());
        }
        if (age is 1 or 112)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect315Sound5013A0(age == 1 ? resources.SoundA : resources.SoundB, 0, TaskView(Read(frame, address), 0x10, 8)));
        }
        if (age == 157)
        {
            int handle = unchecked((int)frame.State.ReadDword(VibrationGlobal));
            if (inputs.IsPlayingVibration(frame, handle))
            {
                frame = inputs.Execute(frame, new NativeBattleEffect315StopVibration4A2940(handle));
            }
        }
        short nextAge = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)nextAge)));
        if (nextAge <= 160)
        {
            return (frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect315ModelFlags8(caster, 0x8000, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect315ModelDefaultSequence2(caster, unchecked((byte)Read(frame, address).ReadWord(0xE))));
        frame = inputs.Execute(frame, new NativeBattleEffect315Activate487670(Caster(frame) + 1));
        return (frame, 2);
    }

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceModelColor(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs, bool recipient)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0xC);
        int actor = Signed(task, 0xE);
        uint color;
        if (age < (recipient ? 8 : 4) || age >= (recipient ? 110 : 105))
        {
            bool final = age >= (recipient ? 110 : 105);
            int selector = final ? Signed(task, 0x1C) : 0;
            uint palette = recipient ? BinaryPrimitives.ReadUInt32LittleEndian(resources.RecipientColors96.AsSpan().Slice(selector * 4, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(resources.CasterColors48.AsSpan().Slice(selector * 4, 4));
            uint far = recipient ? final ? inputs.ReadTransitionColor40(frame) : 0u : final ? 0u : inputs.ReadTransitionColor40(frame);
            int factor = recipient ? final ? (age - 110) << 9 : (8 - age) << 9 : final ? (age - 105) * 341 : (4 - age) << 10;
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect315 requires its current primitive register process.");
            NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect315 requires its current far-color owner.");
            registers = registers with { FarRed = (far & 255) << 4, FarGreen = ((far >> 8) & 255) << 4, FarBlue = ((far >> 16) & 255) << 4 };
            NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 requires its current DPCS registers."), palette, unchecked((uint)factor), registers, inputs.Colors);
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers }, ScriptedColorRegisters = result.Registers } };
            color = result.PackedRgb;
        }
        else
        {
            int selector = recipient ? age % 24 : (age - 4) % 12;
            frame = Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)selector)));
            color = recipient ? BinaryPrimitives.ReadUInt32LittleEndian(resources.RecipientColors96.AsSpan().Slice(selector * 4, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(resources.CasterColors48.AsSpan().Slice(selector * 4, 4));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect315ModelColor40(ModelAddress(actor), color));
        frame = inputs.Execute(frame, new NativeBattleEffect315ModelFlags(ModelAddress(actor), 0, 0x800));
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)next)));
        if (next < (recipient ? 118 : 117))
        {
            return (frame, 0);
        }
        return (inputs.Execute(frame, new NativeBattleEffect315ModelFlags(ModelAddress(actor), 0x800, 0)), 2);
    }
}
