using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Records
{
    public const uint First = 0x221BCA0;
    public const int Count = 100;
    public const int Stride = 24;

    public static NativeBattleEffect321Step Advance(NativeBattleEffect321Frame frame, uint address, bool masked, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        (frame, uint workAddress) = Allocate(frame, 72);
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (!Paused(frame, inputs) && age <= (masked ? 3 : 10))
        {
            NativeBattleEffectArena sourcePoint = TaskView(task, 16, 8);
            frame = PublishContext(frame, workAddress, CopyBytes(sourcePoint, 0, Context(frame, workAddress, 72), 0, 8), 72);
            frame = Birth(frame, sourcePoint, masked ? unchecked((uint)Signed(task, 14)) : 1u, masked ? 8 : 6, inputs);
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattleEffectArena work = CopyBytes(PackedArena(inputs.ReadView(frame)), 0, Context(frame, workAddress, 72), 8, 32);
        frame = PublishContext(frame, workAddress, work, 72);
        NativeBattleEffectArena sprite = PutWord(Put(Context(frame, spriteAddress, 180), 0, 0xCD1390), 0x24, 0);
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, inputs.ReadView(frame)), inputs.ReadView(frame)) };
        int survivors = 0;
        uint mask = masked ? unchecked((uint)Signed(Read(frame, address), 14)) : 1u;
        for (int index = 0; index < Count; index++)
        {
            uint current = First + (uint)(index * Stride);
            NativeBattleEffectArena row = frame.State.ReadView(current, Stride);
            bool selected = masked ? (Dword(row, 0) & mask) != 0 : (row.Read(0, 1)[0] & 1) != 0;
            if (!selected)
            {
                continue;
            }
            frame = DrawRow(frame, row, workAddress, spriteAddress, resources, inputs);
            if (Paused(frame, inputs))
            {
                continue;
            }
            row = frame.State.ReadView(current, Stride);
            if (Word(Context(frame, spriteAddress, 180), 0x28) < 0)
            {
                row = Put(row, 0, 0);
            }
            else
            {
                row = PutWord(row, 4, Word(row, 4) + 1);
                if (masked)
                {
                    row = PutWord(row, 8, Word(row, 8) + Word(row, 16));
                    row = PutWord(row, 12, Word(row, 12) + Word(row, 20));
                    row = PutWord(row, 16, Word(row, 16) - (Word(row, 16) >> 3));
                    row = PutWord(row, 20, Word(row, 20) - (Word(row, 20) >> 3));
                }
                else
                {
                    row = PutWord(row, 10, Word(row, 10) + Word(row, 18));
                    row = PutWord(row, 16, Word(row, 16) - (Word(row, 16) >> 4));
                }
                survivors++;
            }
            frame = frame with { State = frame.State.WriteView(current, row, Stride) };
        }
        frame = Release(Release(frame, 180), 72);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)))), survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect321Frame Birth(NativeBattleEffect321Frame frame, NativeBattleEffectArena point, uint marker, int attempts, NativeBattleEffect321Inputs inputs)
    {
        for (int birth = 0; birth < attempts; birth++)
        {
            uint current = FindFree(frame);
            if (current == 0)
            {
                return frame;
            }
            NativeBattleEffectArena row = PutWord(Put(frame.State.ReadView(current, Stride), 0, marker), 4, 0);
            bool masked = marker != 1;
            int scale = masked ? (Random(ref frame) & 1023) + 1536 : Random(ref frame) % 2560 + 2304;
            row = CopyBytes(point, 0, PutWord(row, 6, scale), 8, 8);
            if (masked)
            {
                int angle = Random(ref frame) & 4095;
                int radius = Random(ref frame) % 90 + 70;
                row = PutWord(row, 16, unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12);
                row = PutWord(row, 18, -(Random(ref frame) % 120));
                row = PutWord(row, 20, unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12);
            }
            else
            {
                row = PutWord(row, 8, Word(row, 8) + Random(ref frame) % 4400 - 2200);
                row = PutWord(row, 10, Word(row, 10) - Random(ref frame) % 300);
                row = PutWord(row, 12, Word(row, 12) + Random(ref frame) % 4400 - 2200);
                row = PutWord(row, 18, -5 - Random(ref frame) % 45);
            }
            frame = frame with { State = frame.State.WriteView(current, row, Stride) };
        }
        return frame;
    }

    public static uint FindFree(NativeBattleEffect321Frame frame)
    {
        for (int index = 0; index < Count; index++)
        {
            uint address = First + (uint)(index * Stride);
            if (frame.State.ReadDword(address) == 0)
            {
                return address;
            }
        }
        return 0;
    }

    public static NativeBattleEffect321Frame DrawRow(NativeBattleEffect321Frame frame, NativeBattleEffectArena row, uint workAddress, uint spriteAddress, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        (byte[] bytes, bool[] known) = row.ReadSnapshot(14, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        NativeBattleGeometryRegisterStep loaded = NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(row, 8), w);
        (byte[] packed, bool[] packedKnown) = row.ReadSnapshot(8, 8);
        loaded = loaded with { Colors = loaded.Colors with { NormalInput = packed.ToImmutableArray(), NormalInputKnown = packedKnown.ToImmutableArray() } };
        frame = Publish(frame, loaded);
        frame = frame with { Memory = NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory) };
        short scale = Word(row, 6);
        frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(frame.Memory, unchecked((ushort)scale)) };
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters!;
        int[] point = [colors.Mac[0]!.Value, colors.Mac[1]!.Value, colors.Mac[2]!.Value];
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(point[0], point[1], point[2]);
        int[] normal = [normalized.X, normalized.Y, normalized.Z];
        NativeBattleEffectArena work = Context(frame, workAddress, 72);
        for (int axis = 0; axis < 3; axis++)
        {
            work = Put(work, 0x28 + axis * 4, unchecked((uint)normal[axis]));
            point[axis] = unchecked(point[axis] + (unchecked(normal[axis] * -(scale >> 3)) >> 12));
            work = Put(work, 0x38 + axis * 4, unchecked((uint)point[axis]));
        }
        frame = PublishContext(frame, workAddress, work, 72);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadTranslation64DD70(frame.Memory, CopyBytes(work, 0x38, NativeBattleEffectArena.Clear(), 0, 12)) };
        frame = PublishContext(frame, spriteAddress, PutWord(Context(frame, spriteAddress, 180), 4, Word(row, 4)), 180);
        return DrawSprite(frame, spriteAddress, resources, inputs);
    }
}
