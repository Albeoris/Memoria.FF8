using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Late
{
    public static NativeBattleEffect312Step Advance(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x59F7E0 => History(frame, address, resources, inputs),
            0x59FBD0 => Wide(frame, address, resources, inputs),
            0x5A0030 or 0x5A0150 or 0x5A0290 or 0x5A03D0 or 0x5A0F50 => Mesh(frame, address, resources, inputs),
            0x5A0570 => Sprites(frame, address, resources, inputs),
            0x5A0780 => Debris(frame, address, resources, inputs),
            0x5A0AA0 => Raise(frame, address, inputs),
            0x5A0BF0 => Throw(frame, address, inputs),
            _ => throw new NotSupportedException("Effect312 late callback is unsupported.")
        };
    }

    private static bool Paused(NativeBattleEffect312Frame frame, NativeBattleEffect312Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static short Word(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena bytes, int offset, uint value) => bytes.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena bytes, int offset, int value) => bytes.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static short TaskWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask TaskPut(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
    private static NativeBattleEffect312Frame DrawRandom(NativeBattleEffect312Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort drawn);
        value = drawn;
        return frame with { Random = random };
    }

    private static NativeBattleEffect312Frame ModelWrite(NativeBattleEffect312Frame frame, BattleModelOwner model, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect312Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect312WriteModelBytes(model, offset, data, length));

    private static NativeBattleEffect312Frame Capture(NativeBattleEffect312Frame frame, BattleModelOwner model, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
        NativeBattleModelAliases aliases = inputs.ReadPackets(frame).ModelAliases;
        uint? original = aliases.TryEncodeExact(model, out byte slot) && slot < SavedActorCount
            ? SavedActorsAddress + (uint)(slot * SavedActorStride)
            : null;
        NativeBattleEffectArena saved = NativeBattleEffectArena.Clear().MarkUnknown(0, SavedActorStride);
        if (original is uint originalRow)
        {
            saved = CopyBytes(ReadBytes(frame, originalRow, SavedActorLength), 0, saved, 0, SavedActorLength);
        }
        saved = PutWord(saved, 0, Word(current, 0) & 0x1000);
        saved = CopyBytes(current, 0x0C, saved, 4, 8);
        saved = CopyBytes(current, 0x1C, saved, 12, 8);
        if (original is uint raw)
        {
            frame = WriteBytes(frame, raw, saved, 2);
            frame = WriteBytes(frame, raw + 4, CopyBytes(saved, 4, NativeBattleEffectArena.Clear(), 0, 8), 8);
            frame = WriteBytes(frame, raw + 12, CopyBytes(saved, 12, NativeBattleEffectArena.Clear(), 0, 8), 8);
        }
        return frame with { Effect = frame.Effect with { Effect312SavedTransforms = frame.Effect.Effect312SavedTransforms.SetItem(model, saved) } };
    }

    private static NativeBattleEffectArena Saved(NativeBattleEffect312Frame frame, BattleModelOwner model)
    {
        return frame.Effect.Effect312SavedTransforms.TryGetValue(model, out NativeBattleEffectArena? saved)
            ? saved
            : throw new InvalidDataException("Effect312 requires its previously captured model transform.");
    }

    private static NativeBattleEffect312Frame Restore(NativeBattleEffect312Frame frame, BattleModelOwner model, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleEffectArena saved = Saved(frame, model);
        NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
        int flags = (Word(current, 0) & 0xEFFF) | unchecked((ushort)Word(saved, 0));
        frame = ModelWrite(frame, model, 0, PutWord(current, 0, flags), 2, inputs);
        frame = ModelWrite(frame, model, 0x1C, CopyBytes(saved, 12, NativeBattleEffectArena.Clear(), 0, 8), 8, inputs);
        return ModelWrite(frame, model, 0x0C, CopyBytes(saved, 4, NativeBattleEffectArena.Clear(), 0, 8), 8, inputs);
    }

    private static NativeBattleEffect312Frame ForceTransform(NativeBattleEffect312Frame frame, BattleModelOwner model, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
        return ModelWrite(frame, model, 0, PutWord(current, 0, Word(current, 0) | 0x1000), 2, inputs);
    }

    private static NativeBattleEffect312Step Raise(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        BattleModelOwner model = task.ReadExecutionReference<BattleExecutionReference.Model>(0x0E, 2).Owner;
        int age = TaskWord(task, 0x0C);
        if (age == 0)
        {
            frame = Capture(frame, model, inputs);
        }
        if (age < 8)
        {
            frame = ForceTransform(frame, model, inputs);
            short y = unchecked((short)(Word(Saved(frame, model), 14) - TaskWord(task, 0x20) * age / 8));
            frame = ModelWrite(frame, model, 0x1E, PutWord(NativeBattleEffectArena.Clear(), 0, y), 2, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        age = unchecked((short)(age + 1));
        frame = Write(frame, address, TaskPut(task, 0x0C, age));
        return age >= 9 ? new(Restore(frame, model, inputs), 2) : new(frame, 0);
    }

    private static NativeBattleEffect312Step Throw(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        BattleModelOwner model = task.ReadExecutionReference<BattleExecutionReference.Model>(0x0E, 2).Owner;
        int age = TaskWord(task, 0x0C);
        if (age == 0)
        {
            NativeBattleEffect312PointStep anchored = inputs.Point571400(frame, model, ReadBytes(frame, address + 0x10, 8));
            frame = WriteBytes(anchored.Frame, address + 0x10, anchored.Output8, 8);
            task = Read(frame, address);
            frame = Capture(frame, model, inputs);
        }
        if (age <= 20)
        {
            frame = ForceTransform(frame, model, inputs);
            NativeBattleEffectArena rotation = inputs.ReadModelArena9C(frame, model);
            rotation = PutWord(PutWord(rotation, 0x0C, TaskWord(task, 0x18) * age / 20), 0x0E, age * 8192 / 20);
            frame = ModelWrite(frame, model, 0x0C, CopyBytes(rotation, 0x0C, NativeBattleEffectArena.Clear(), 0, 4), 4, inputs);
            NativeBattleEffectArena anchor = ReadBytes(frame, address + 0x10, 8);
            NativeBattleEffectArena old = Saved(frame, model);
            NativeBattleVector delta = new(unchecked((short)(Word(anchor, 0) - Word(old, 12))), unchecked((short)(Word(anchor, 2) - Word(old, 14))), unchecked((short)(Word(anchor, 4) - Word(old, 16))));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(Word(rotation, 0x0C), Word(rotation, 0x0E), Word(rotation, 0x10)));
            NativeBattleVector transformed = NativeBattleGfMatrices.RotateShort56C4F0(matrix, delta);
            int lift = unchecked(NativeBattleCameraMath.Sine(age * 2048 / 20) * TaskWord(task, 0x20)) >> 12;
            NativeBattleEffectArena position = PutWord(PutWord(PutWord(NativeBattleEffectArena.Clear(), 0, Word(anchor, 0) - transformed.X), 2, Word(anchor, 2) - transformed.Y + lift), 4, Word(anchor, 4) - transformed.Z);
            frame = ModelWrite(frame, model, 0x1C, position, 6, inputs);
        }
        else if (age <= 28)
        {
            frame = ForceTransform(frame, model, inputs);
            int lift = unchecked(NativeBattleCameraMath.Sine((age - 20) * 2048 / 8) * (TaskWord(task, 0x20) >> 3)) >> 12;
            frame = ModelWrite(frame, model, 0x1E, PutWord(NativeBattleEffectArena.Clear(), 0, lift), 2, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int? hit = task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(0x1A, 2).Offset;
        if (age == 20)
        {
            (frame, uint childAddress) = TrySpawn(frame, 0x223E1A8, 0x5A0F50);
            NativeBattleEffectArena actor = inputs.ReadModelArena9C(frame, model);
            NativeBattleModulePoolTask child = TaskPut(TaskPut(TaskPut(TaskPut(Read(frame, childAddress), 0x0C, 0), 0x10, Word(actor, 0x1C)), 0x12, 0), 0x14, Word(actor, 0x20));
            frame = DrawRandom(frame, out int random);
            int scale = (random % 1024 + 3072) / 4;
            frame = Write(frame, childAddress, TaskPut(TaskPut(child, 0x1C, scale), 0x1E, scale));
            if (hit.HasValue)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect312SetModelAnimation505C00(model, 6));
            }
        }
        age = unchecked((short)(age + 1));
        frame = Write(frame, address, TaskPut(task, 0x0C, age));
        if (age < 28 || hit.HasValue && inputs.ReadCurrentSequenceByte(frame, model) == 6)
        {
            return new(frame, 0);
        }
        frame = Restore(frame, model, inputs);
        if (hit.HasValue)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Effect312 requires its current action parameters.");
            BattleContactOccurrence occurrence = inputs.ReadPackets(frame).ResolveContact(parameters.Header, hit.Value);
            frame = inputs.Execute(frame, new NativeBattleEffect312Contact506690(occurrence));
        }
        return new(frame, 2);
    }
}
