using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66BodyDrawing
{
    private static (NativeBattleEffect66Frame Frame, NativeBattleEffectArena Temporary, bool Allocated) BirthShard(NativeBattleEffect66Frame frame, NativeBattleEffectArena temporary, uint flagAddress, int corners)
    {
        uint start = frame.State.ReadDword(0x24C917C);
        int slot = unchecked((int)frame.State.ReadDword(0x24C9180));
        int attempts = 0;
        while (attempts < 1000)
        {
            if (slot >= 1000)
            {
                slot = 0;
            }
            if (Uword(ReadBytes(frame, checked(start + (uint)(slot * 68)), 2), 0) == 0)
            {
                break;
            }
            slot++;
            attempts++;
        }
        if (attempts == 1000)
        {
            return (frame, temporary, false);
        }
        frame = WriteBytes(frame, flagAddress, Word(NativeBattleEffectArena.Clear(), 0, slot), 2);
        frame = frame with { State = frame.State.WriteDword(0x24C9180, (uint)slot) };
        uint address = checked(start + (uint)(slot * 68));
        NativeBattleEffectArena shard = Word(ReadBytes(frame, address, 68), 0, 1);
        (frame, int random) = Random(frame);
        shard = Word(shard, 2, random % 24 + 16);
        for (int corner = 0; corner < corners; corner++)
        {
            shard = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x58 + corner * 8, shard, 4 + corner * 8, 8);
        }
        temporary = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x60, temporary, 0x78, 8);
        temporary = Word(temporary, 0x7A, S16(temporary, 0x7A) - 4000);
        NativeBattleVector center = Vector(temporary, 0x78);
        for (int corner = 0; corner < corners; corner++)
        {
            NativeBattleVector point = Vector(temporary, 0x58 + corner * 8);
            NativeBattleVector delta = new(unchecked((short)(center.X - point.X)), unchecked((short)(center.Y - point.Y)), unchecked((short)(center.Z - point.Z)));
            temporary = Word(Word(Word(temporary, 0x80, delta.X), 0x82, delta.Y), 0x84, delta.Z);
            var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(delta.X, delta.Y, delta.Z);
            shard = Word(Word(Word(shard, 0x24 + corner * 8, unchecked((short)normalized.X)), 0x26 + corner * 8, unchecked((short)normalized.Y)), 0x28 + corner * 8, unchecked((short)normalized.Z));
        }
        frame = WriteBytes(frame, address, shard, 68);
        if (corners == 3)
        {
            frame = BirthSpark(frame, temporary);
        }
        return (frame, temporary, true);
    }

    private static NativeBattleEffect66Frame BirthSpark(NativeBattleEffect66Frame frame, NativeBattleEffectArena temporary)
    {
        uint start = frame.State.ReadDword(0xDEA3D8);
        int slot = unchecked((int)frame.State.ReadDword(0x24C66B0));
        int attempts = 0;
        while (attempts < 800)
        {
            if (slot >= 800)
            {
                slot = 0;
            }
            if (Dword(ReadBytes(frame, checked(start + (uint)(slot * 24)), 4), 0) == 0)
            {
                break;
            }
            slot++;
            attempts++;
        }
        if (attempts == 800)
        {
            return frame;
        }
        frame = frame with { State = frame.State.WriteDword(0x24C66B0, (uint)slot) };
        uint address = checked(start + (uint)(slot * 24));
        NativeBattleEffectArena spark = Put(Word(ReadBytes(frame, address, 24), 4, 0), 0, 1);
        (frame, int random) = Random(frame);
        spark = Word(spark, 6, random % 0x880 + 0x880);
        spark = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x58, spark, 8, 8);
        (frame, random) = Random(frame);
        spark = Word(spark, 0x12, -25 - random % 65);
        return WriteBytes(frame, address, spark, 24);
    }

    private static (NativeBattleEffect66Frame Frame, NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers) ReadAndAdvanceShard(NativeBattleEffect66Frame frame, NativeBattleEffectArena temporary, uint flagAddress, int slot, int corners, NativeBattleGeometryRegisterStep registers, NativeBattleEffect66Inputs inputs)
    {
        uint address = checked(frame.State.ReadDword(0x24C917C) + (uint)(slot * 68));
        NativeBattleEffectArena shard = ReadBytes(frame, address, 68);
        for (int corner = 0; corner < corners; corner++)
        {
            temporary = NativeBattleEffectNodeContextMemory.Copy(shard, 4 + corner * 8, temporary, 0x58 + corner * 8, 8);
        }
        int age = S16(shard, 0);
        if (age < 48)
        {
            int factor = unchecked(age << 12) / 48;
            temporary = Put(temporary, 0x38, 0);
            NativeBattleModelRendererRegisters far = frame.Memory.PrimitiveProcess!.ModelRendererRegisters!;
            NativeBattleScriptedDpcsResult faded = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, 0, unchecked((uint)factor), far, inputs.Colors);
            registers = registers with { Colors = faded.Registers };
            temporary = Put(temporary, 0x38, faded.PackedRgb);
        }
        else
        {
            temporary = Put(temporary, 0x38, 0xFFFFFF);
        }
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            age = unchecked((short)(age + 1));
            shard = Word(shard, 0, age);
            if (age >= 64)
            {
                shard = Word(shard, 0, 0);
                frame = WriteBytes(frame, flagAddress, Word(NativeBattleEffectArena.Clear(), 0, 0x8000), 2);
            }
            int speed = S16(shard, 2);
            for (int corner = 0; corner < corners; corner++)
            {
                int factor = corners == 3 ? corner switch { 0 => unchecked(speed * 3) >> 2, 1 => speed, _ => unchecked(speed * 7) >> 4 } : corner switch { 0 => unchecked(speed * 5) >> 3, 1 => speed, 2 => unchecked(speed * 3) >> 2, _ => unchecked(speed * 7) >> 4 };
                for (int axis = 0; axis < 3; axis++)
                {
                    int delta = unchecked(S16(shard, 0x24 + corner * 8 + axis * 2) * factor) >> 12;
                    int at = 4 + corner * 8 + axis * 2;
                    shard = Word(shard, at, S16(shard, at) + delta);
                }
            }
            shard = Word(shard, 2, speed + (speed >> 4));
            frame = WriteBytes(frame, address, shard, 68);
        }
        return (frame, temporary, registers);
    }

    private static (NativeBattleEffect66Frame Frame, int Value) Random(NativeBattleEffect66Frame frame)
    {
        NativeCrtRandomState state = frame.Random.Draw(out ushort value);
        return (frame with { Random = state }, value);
    }
}
