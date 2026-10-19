using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163ComposeActor : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163BindActor : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163Sequence(byte Actor, int Sequence) : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163SetPosition(byte Actor, NativeBattleModuleAnchor Position) : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163Sound(uint Descriptor) : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163Stage(int Intensity) : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163Contacts : NativeBattleRoughDivide163Operation;
public sealed record NativeBattleRoughDivide163Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random)
{
    public NativeBattleEffectParameters Parameters => Memory.RoughDivide163?.Parameters ?? throw new InvalidDataException("Native Rough Divide requires its actual entered parameter owner.");
    public byte FirstRecipient => Memory.RoughDivide163?.FirstRecipient ?? throw new InvalidDataException("Native Rough Divide requires its actual first recipient.");
}
public sealed record NativeBattleRoughDivide163ChildStep(NativeBattleRoughDivide163Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
public sealed record NativeBattleRoughDivide163Inputs(uint GraphicsFlags, Func<byte, NativeBattleModuleAnchor> ReadActorPosition, Func<byte, byte, short, NativeBattleModuleAnchor> ReadBone, Func<NativeBattleRoughDivide163Frame, NativeBattleRoughDivide163Operation, NativeBattleRoughDivide163Frame> Execute)
{
    public NativeBattleModuleAnchor ReadBone241(byte actor) => ReadBone(actor, 241, 0);
}

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163ChildStep Advance5F5C80(NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRoughDivide163Inputs inputs)
    {
        Execute(new NativeBattleRoughDivide163ComposeActor());
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        byte actor = frame.Parameters.Actor78;
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        switch (counter)
        {
            case 0:
                Execute(new NativeBattleRoughDivide163BindActor());
                Sequence(actor, 1);
                break;
            case 1:
                Execute(new NativeBattleRoughDivide163Sound(ReadDword(frame.Staging, 0xDA00A8)));
                Append(0x5F5FE0);
                for (uint address = 0x23CE288; address < 0x23CE314; address += 20)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                }
                AppendSimple(0x5F67A0);
                break;
            case 4:
                int beam = AppendAtBone(0x5F65E0);
                Set(beam, 28, 10240);
                Set(beam, 32, 10240);
                Set(beam, 34, 853);
                break;
            case 7:
                Sequence(actor, 2);
                break;
            case 13:
                Sequence(actor, 3);
                break;
            case 20:
                AppendSimple(0x5F6A30);
                AppendSimple(0x5F6CA0);
                AppendSimple(0x5F7000);
                int echo = Append(0x5F7390);
                Set(echo, 28, 20);
                break;
            case 30:
                AppendAtBone(0x5F7A80);
                Sequence(frame.FirstRecipient, 6);
                break;
            case 31:
                int spiral = AppendAtBone(0x5F78D0);
                Set(spiral, 24, Rand() % 4096);
                Set(spiral, 26, -80 - Rand() % 40);
                Set(spiral, 28, 3072);
                Set(spiral, 34, 426);
                Set(spiral, 32, 426);
                int ground = Append(0x5F7E00);
                Set(ground, 18, 0);
                Set(ground, 20, unchecked((short)ReadDword(frame.Staging, 0x23CD31C)));
                Set(ground, 14, 1);
                Set(ground, 16, inputs.ReadActorPosition(frame.FirstRecipient).X);
                frame = frame with { Staging = WriteDword(WriteDword(frame.Staging, 0x23CE280, TaskDword(pool.Slots[ground], 16)), 0x23CE284, TaskDword(pool.Slots[ground], 20)) };
                break;
            case 38:
                task = Position(task, inputs.ReadActorPosition(actor));
                Move(inputs.ReadActorPosition(actor) with { Y = -12000 });
                break;
            case 40:
                Sequence(actor, 4);
                AppendSimple(0x5F81D0);
                break;
            case 51:
                int ring = AppendAtRetained(0x5F8540);
                Set(ring, 24, Rand() % 4096);
                Set(ring, 28, Rand() % 512 + 3584);
                int scale = (Rand() % 1024 + 5120) / 3;
                Set(ring, 34, scale);
                Set(ring, 32, scale);
                for (int index = 0; index < 12; index++)
                {
                    int spray = Append(0x5F8B80);
                    Set(spray, 14, index + Rand() % 4);
                    SetRetained(spray);
                    Set(spray, 16, Word(pool.Slots[spray], 16) + Rand() % 4000 - 2000);
                    Set(spray, 20, Word(pool.Slots[spray], 20) + Rand() % 4000 - 2000);
                    Set(spray, 24, Rand() % 4096);
                    Set(spray, 28, Rand() % 3072 + 2304);
                    Set(spray, 32, Rand() % 1536 + 3584);
                    Set(spray, 34, Rand() % 10 + 6);
                }
                AppendSimple(0x5F8D10);
                break;
            case 52:
                int heightIndex = 1;
                for (int baseScale = 4096; baseScale >= 3072; baseScale -= 512)
                {
                    int shock = Append(0x5F8770);
                    Set(shock, 14, heightIndex);
                    Set(shock, 16, unchecked((short)ReadDword(frame.Staging, 0x23CE280)));
                    int heightRandom = Rand();
                    Set(shock, 20, unchecked((short)ReadDword(frame.Staging, 0x23CE284)));
                    Set(shock, 18, -1000 * heightIndex - heightRandom % 400 + 200);
                    Set(shock, 24, Rand() % 4096);
                    Set(shock, 26, Rand() % 100 + 30);
                    int shockScale = (baseScale + Rand() % 768) / 4;
                    Set(shock, 30, shockScale);
                    Set(shock, 28, shockScale);
                    Set(shock, 32, Rand() % 1280 + 3584);
                    heightIndex++;
                }
                int finalRing = AppendAtRetained(0x5F8960);
                Set(finalRing, 24, Rand() % 4096);
                int finalScale = (Rand() % 768 + 5120) / 3;
                Set(finalRing, 30, finalScale);
                Set(finalRing, 28, finalScale);
                break;
            case 65:
                Execute(new NativeBattleRoughDivide163Contacts());
                break;
        }
        if (counter is >= 74 and < 80)
        {
            NativeBattleModuleAnchor current = inputs.ReadActorPosition(actor);
            if (counter == 74)
            {
                Sequence(actor, 5);
                current = current with { Y = Word(task, 18) };
            }
            short displacement = unchecked((short)(1000 * (counter - 74)));
            Move(current with { Z = unchecked((short)(Word(task, 20) - displacement / 6 + 1000)) });
        }
        if (counter >= 77)
        {
            Execute(new NativeBattleRoughDivide163Stage((85 - counter) << 8));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))) with { Next = pool.Slots[slot].Next };
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        if (Word(task, 12) <= 85)
        {
            return new(frame, pool, 0);
        }
        Execute(new NativeBattleRoughDivide163Stage(0));
        Move(Position(task));
        return new(frame, pool, 2);

        int Append(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            pool = pool with { Slots = pool.Slots.SetItem(created, pool.Slots[created].WriteWord(12, 0)) };
            return created;
        }
        int AppendSimple(uint callback)
        {
            int created = Append(callback);
            Set(created, 14, 0);
            return created;
        }
        int AppendAtBone(uint callback)
        {
            int created = Append(callback);
            NativeBattleModuleAnchor point = inputs.ReadBone241(actor);
            Set(created, 16, point.X);
            Set(created, 18, 0);
            Set(created, 20, point.Z);
            return created;
        }
        int AppendAtRetained(uint callback)
        {
            int created = Append(callback);
            SetRetained(created);
            return created;
        }
        void SetRetained(int created)
        {
            pool = pool with { Slots = pool.Slots.SetItem(created, TaskDword(TaskDword(pool.Slots[created], 16, ReadDword(frame.Staging, 0x23CE280)), 20, ReadDword(frame.Staging, 0x23CE284))) };
        }
        void Set(int targetSlot, int offset, int value)
        {
            pool = pool with { Slots = pool.Slots.SetItem(targetSlot, pool.Slots[targetSlot].WriteWord(offset, unchecked((ushort)value))) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        void Execute(NativeBattleRoughDivide163Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Sequence(byte target, int sequence)
        {
            Execute(new NativeBattleRoughDivide163Sequence(target, sequence));
        }
        void Move(NativeBattleModuleAnchor point)
        {
            Execute(new NativeBattleRoughDivide163SetPosition(actor, point));
        }
    }

    private static uint TaskDword(NativeBattleModulePoolTask task, int offset) => (uint)task.ReadWord(offset) | ((uint)task.ReadWord(offset + 2) << 16);
    private static NativeBattleModulePoolTask TaskDword(NativeBattleModulePoolTask task, int offset, uint value) => task.WriteWord(offset, unchecked((ushort)value)).WriteWord(offset + 2, unchecked((ushort)(value >> 16)));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModuleAnchor Position(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModuleAnchor point) => task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z)).WriteWord(22, unchecked((ushort)point.W));
    private static uint ReadDword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
    private static short ReadWord(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
    private static NativeBattleFileStaging WriteWord(NativeBattleFileStaging staging, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging WriteDword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
