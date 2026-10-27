using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191CreatureServices(Func<uint> ReadFlags, Func<NativeBattleDoomtrain191Frame, uint, NativeBattleDoomtrain191Frame> Invoke, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191Frame> Draw, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191Frame> ScaleCameraHalf, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191Frame> Flash);

public static class NativeBattleDoomtrain191Creature
{
    public static NativeBattleDoomtrain191LeafStep Advance641EA0(NativeBattleDoomtrain191Frame frame, int slot, int sourceLength, NativeBattleDoomtrain191CreatureServices services)
    {
        NativeBattleModulePoolTask task = Pool(frame).Slots[slot];
        if (task.Callback != 0x641EA0)
        {
            throw new InvalidDataException("Native Doomtrain creature requires its actual task.");
        }
        int age = unchecked((short)task.ReadWord(0xC));
        if (age == 0)
        {
            NativeBattleDoomtrain191ModelMemory memory = State().Model ?? NativeBattleDoomtrain191ModelMemory.Unknown();
            frame = NativeBattleDoomtrain191Model.Put(frame, memory);
            frame = NativeBattleDoomtrain191Model.InitializeWorld642E10(frame);
            memory = Memory();
            frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = memory.Model.Write(0x58, new byte[4]).Write(0x54, new byte[4]) });
            frame = NativeBattleDoomtrain191Model.Initialize642400(frame, 15);
            memory = Memory();
            uint pointer = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(memory.Descriptor.Read(12, 4)) + 2);
            frame = NativeBattleDoomtrain191Model.Put(frame, memory with { ScalePointer = pointer });
            short scale = BinaryPrimitives.ReadInt16LittleEndian(Arena().Read(Offset(pointer), 2));
            task = task.WriteWord(0x1C, unchecked((ushort)scale));
            NativeBattleEffectTaskPool initialized = Pool(frame);
            frame = PutPool(frame, initialized.WriteSlot(slot, initialized.Slots[slot].WriteWord(0x1C, unchecked((ushort)scale))));
        }
        bool paused = (services.ReadFlags() & 1) != 0;
        bool draw = true;
        bool halfCamera = false;
        int relative;
        if (age < 35)
        {
            SetScale(4, 8192);
            SetZ(25000);
            if (!paused)
            {
                if (age == 0)
                {
                    Select(0);
                }
                else if (age == 1)
                {
                    Append(0x643D10);
                }
                else if (age == 14)
                {
                    Invoke(0x6437F0);
                }
            }
        }
        else if ((relative = age - 35) < 9)
        {
            if (!paused)
            {
                if (relative == 0)
                {
                    Invoke(0x643FD0);
                    Invoke(0x644E10);
                }
                else if (relative == 1)
                {
                    Invoke(0x6439C0);
                }
                AddZ(-400);
            }
            draw = relative != 0;
        }
        else if ((relative = age - 44) < 25)
        {
            if (!paused)
            {
                AddZ(-600);
                Advance();
            }
        }
        else if ((relative = age - 69) < 100)
        {
            SetScale(10, 4096);
            SetZ(0);
            if (!paused)
            {
                if (relative == 0)
                {
                    Select(1);
                    Append(0x642EF0);
                }
                else
                {
                    Advance();
                    if (relative == 1)
                    {
                        Invoke(0x643C10);
                        Invoke(0x6444E0);
                        Invoke(0x6454F0);
                    }
                }
            }
            draw = relative != 0;
        }
        else if ((relative = age - 169) < 20)
        {
            SetScale(4, 8192);
            if (!paused)
            {
                if (relative == 0)
                {
                    SetZ(29000);
                    Select(0);
                    Invoke(0x642EA0);
                    Invoke(0x644490);
                    Invoke(0x6454A0);
                }
                else if (relative == 1)
                {
                    Invoke(0x63F8F0);
                    Invoke(0x6432F0);
                    Invoke(0x643C50);
                }
                else if (relative == 19)
                {
                    Invoke(0x63F930);
                }
                AddZ(-1000);
                Advance();
            }
            draw = relative != 0;
            halfCamera = draw;
        }
        else if ((relative = age - 189) < 22)
        {
            SetScale(4, 16384);
            if (!paused)
            {
                if (relative == 0)
                {
                    SetZ(28000);
                    Advance();
                    Invoke(0x643560);
                    Invoke(0x6448F0);
                    Invoke(0x645A90);
                }
                else if (relative == 1)
                {
                    Invoke(0x643C90);
                }
                else if (relative == 4)
                {
                    Append(0x646180);
                }
                AddZ(-800);
                Advance();
            }
            draw = relative != 0;
        }
        else if ((relative = age - 211) < 31)
        {
            SetScale(4, 16384);
            if (!paused)
            {
                if (relative == 0)
                {
                    SetZ(32000);
                    Invoke(0x63F8F0);
                    Invoke(0x642E40);
                    frame = frame with { Memory = frame.Memory with { Doomtrain191 = State() with { LookupCounter = State().LookupCounter | 2 } } };
                }
                else if (relative == 20)
                {
                    Append(0x6435B0);
                }
                else if (relative > 20)
                {
                    AddZ(-1800);
                }
                Advance();
            }
            draw = relative != 0;
        }
        else if ((relative = age - 242) < 40)
        {
            SetScale(4, 16384);
            if (!paused)
            {
                if (relative == 0)
                {
                    SetZ(28000);
                    Invoke(0x643CD0);
                }
                else if (relative == 2)
                {
                    Invoke(0x646570);
                }
                else if (relative == 3)
                {
                    Invoke(0x646AA0);
                    frame = services.Flash(frame);
                }
                AddZ(-4800);
                Advance();
            }
            draw = relative != 0;
        }
        else
        {
            draw = age - 282 < 4;
        }
        if (draw)
        {
            frame = services.Draw(frame);
            if (halfCamera)
            {
                frame = services.ScaleCameraHalf(frame);
            }
        }
        frame = NativeBattleDoomtrain191Model.PublishAnchor(frame);
        if (paused)
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(age + 1));
        NativeBattleEffectTaskPool pool = Pool(frame);
        frame = PutPool(frame, pool.WriteSlot(slot, pool.Slots[slot].WriteWord(0xC, next)));
        if (unchecked((short)next) < 279)
        {
            return new(frame, 0);
        }
        frame = frame with { Memory = frame.Memory with { Doomtrain191 = State() with { LookupCounter = State().LookupCounter | 1 } } };
        return new(frame, 2);

        NativeBattleDoomtrain191State State() => frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain creature requires its retained module state.");
        NativeBattleDoomtrain191ModelMemory Memory() => NativeBattleDoomtrain191Model.Require(frame);
        NativeBattleEffectArena Arena() => frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain creature requires its current source arena.");
        int Offset(uint address) => checked((int)(address - State().ArenaAddress));
        void SetZ(int value)
        {
            NativeBattleDoomtrain191ModelMemory memory = Memory();
            frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = memory.Model.Write(0x5C, BitConverter.GetBytes(value)) });
        }
        void AddZ(int value)
        {
            SetZ(unchecked(BinaryPrimitives.ReadInt32LittleEndian(Memory().Model.Read(0x5C, 4)) + value));
        }
        void SetScale(int divisor, ushort rotationScale)
        {
            uint pointer = Memory().ScalePointer ?? throw new NotSupportedException("Native Doomtrain creature requires its actual mutable scale pointer.");
            short scale = unchecked((short)task.ReadWord(0x1C));
            frame = frame with { Memory = frame.Memory with { Arena = Arena().Write(Offset(pointer), BitConverter.GetBytes(unchecked((short)(scale / divisor)))) } };
            NativeBattleDoomtrain191ModelMemory memory = Memory();
            frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = memory.Model.Write(0x50, BitConverter.GetBytes(rotationScale)).Write(0x48, BitConverter.GetBytes(rotationScale)).Write(0x40, BitConverter.GetBytes(rotationScale)) });
        }
        void Select(short animation) => frame = NativeBattleDoomtrain191Model.Select642470(frame, animation, sourceLength);
        void Advance() => frame = NativeBattleDoomtrain191Model.Advance642CA0(frame, sourceLength);
        void Invoke(uint callback) => frame = services.Invoke(frame, callback);
        void Append(uint callback)
        {
            NativeBattleEffectTaskPool pool = Pool(frame);
            (pool, int index) = pool.Append(callback);
            pool = pool.WriteSlot(index, pool.Slots[index].WriteWord(0xC, 0));
            frame = PutPool(frame, pool);
        }
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
