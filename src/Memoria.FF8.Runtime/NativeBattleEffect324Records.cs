using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect324Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect324Records
{
    private const uint First = 0x2201AC8;
    private const int Count = 100;
    private const int Stride = 24;

    public static NativeBattleEffect324Step Advance(NativeBattleEffect324Frame frame, uint address, int kind, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        if (kind is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        (frame, uint workAddress) = Allocate(frame, 72);
        int age = Signed(Read(frame, address), 12);
        if (!Paused324(frame, inputs) && age <= 46)
        {
            frame = Birth(frame, age, kind, inputs);
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattleEffectArena work = NativeBattleEffectNodeContextMemory.Copy(PackedArena(inputs.ReadView(frame)), 0, Context(frame, workAddress, 72), 8, 32);
        frame = PublishContext(frame, workAddress, work, 72);
        NativeBattleEffectArena sprite = PutWord324(Put324(Context(frame, spriteAddress, 180), 0, kind == 1 ? 0xCD0B88u : 0xCD0D00u), 0x24, 0);
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, inputs.ReadView(frame)), inputs.ReadView(frame)) };
        int survivors = 0;
        for (int index = 0; index < Count; index++)
        {
            uint rowAddress = First + checked((uint)(index * Stride));
            NativeBattleEffectArena row = frame.State.ReadView(rowAddress, Stride);
            if ((row.Read(0, 1)[0] & kind) == 0)
            {
                continue;
            }
            (byte[] wBytes, bool[] wKnown) = row.ReadSnapshot(14, 2);
            short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
            NativeBattleVector point = new(Word324(row, 8), Word324(row, 10), Word324(row, 12));
            frame = Publish(frame, NativeBattleGeometry.LoadVector(Registers(frame), 0, point, w));
            frame = frame with { Memory = NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory) };
            short scale = Word324(row, 6);
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(frame.Memory, unchecked((ushort)scale)) };
            NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters!;
            int x = colors.Mac[0]!.Value;
            int y = colors.Mac[1]!.Value;
            int z = colors.Mac[2]!.Value;
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            work = Context(frame, workAddress, 72);
            int[] coordinates = [x, y, z];
            int[] direction = [normal.X, normal.Y, normal.Z];
            int distance = -(scale >> 3);
            for (int axis = 0; axis < 3; axis++)
            {
                work = Put324(work, 0x28 + axis * 4, unchecked((uint)direction[axis]));
                coordinates[axis] = unchecked(coordinates[axis] + (unchecked(direction[axis] * distance) >> 12));
                work = Put324(work, 0x38 + axis * 4, unchecked((uint)coordinates[axis]));
            }
            frame = PublishContext(frame, workAddress, work, 72);
            frame = frame with { Memory = NativeBattleEffect315Gte.LoadTranslation64DD70(frame.Memory, NativeBattleEffectNodeContextMemory.Copy(work, 0x38, NativeBattleEffectArena.Clear(), 0, 12)) };
            sprite = PutWord324(Context(frame, spriteAddress, 180), 4, Word324(row, 4));
            frame = PublishContext(frame, spriteAddress, sprite, 180);
            frame = DrawSprite324(frame, spriteAddress, resources, inputs);
            if (Paused324(frame, inputs))
            {
                continue;
            }
            sprite = Context(frame, spriteAddress, 180);
            row = frame.State.ReadView(rowAddress, Stride);
            if (Word324(sprite, 0x28) < 0)
            {
                row = Put324(row, 0, 0);
            }
            else
            {
                row = PutWord324(row, 4, Word324(row, 4) + 1);
                if (kind == 1)
                {
                    row = PutWord324(row, 10, Word324(row, 10) + Word324(row, 18));
                }
                survivors++;
            }
            frame = frame with { State = frame.State.WriteView(rowAddress, row, Stride) };
        }
        frame = Release(Release(frame, 180), 72);
        if (Paused324(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect324Frame Birth(NativeBattleEffect324Frame frame, int age, int kind, NativeBattleEffect324Inputs inputs)
    {
        int boneCount = inputs.ReadCurrentBoneMatrices32(frame, Caster324(frame)).Length;
        for (int birth = 0; birth < 2; birth++)
        {
            uint address = First;
            int index = 0;
            while (index < Count && frame.State.ReadDword(address) != 0)
            {
                index++;
                address += Stride;
            }
            if (index == Count)
            {
                return frame;
            }
            NativeBattleEffectArena row = PutWord324(Put324(frame.State.ReadView(address, Stride), 0, (uint)kind), 4, 0);
            frame = frame with { State = frame.State.WriteView(address, row, Stride) };
            int scale = Draw(ref frame) % 1536 + (kind == 1 ? 2048 - age * 40 : (24 - age) * 32);
            frame = frame with { State = frame.State.WriteWord(address + 6, unchecked((ushort)scale)) };
            int bone = boneCount == 0 ? 0 : Draw(ref frame) % boneCount;
            int fraction = Draw(ref frame) % 4096;
            NativeBattleEffect324PointStep point = inputs.Point502170(frame, Caster324(frame), unchecked((short)bone), unchecked((short)fraction), frame.State.ReadView(address + 8, 8));
            frame = point.Frame with { State = point.Frame.State.WriteView(address + 8, point.Output8, 8) };
            int radius = kind == 1 ? 250 - 3 * age : (100 - age) * 3;
            for (int axis = 0; axis < 3; axis++)
            {
                int delta = Draw(ref frame) % (radius * 2) - radius;
                short value = unchecked((short)(frame.State.ReadWord(address + 8 + (uint)(axis * 2)) + delta));
                frame = frame with { State = frame.State.WriteWord(address + 8 + (uint)(axis * 2), unchecked((ushort)value)) };
            }
            if (kind == 1)
            {
                short velocity = unchecked((short)(-5 - Draw(ref frame) % 25));
                frame = frame with { State = frame.State.WriteWord(address + 18, unchecked((ushort)velocity)) };
            }
        }
        return frame;
    }

    private static int Draw(ref NativeBattleEffect324Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
}
