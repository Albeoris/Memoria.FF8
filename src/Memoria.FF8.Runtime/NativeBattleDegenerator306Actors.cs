using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDegenerator306Actors
{
    private const uint ActorRows = 0x2244168;

    public static NativeBattleDegenerator306Frame Schedule5A5010(NativeBattleDegenerator306Frame frame, short duration, NativeBattleDegenerator306Inputs inputs)
    {
        for (byte actor = 0; actor < 7; actor++)
        {
            if (frame.State.ReadDword(Row(actor)) != 0)
            {
                frame = AppendFade(frame, actor, duration, 0x5A5070, inputs);
            }
        }
        return frame;
    }

    public static NativeBattleDegenerator306Frame Schedule5A51B0(NativeBattleDegenerator306Frame frame, short duration, NativeBattleDegenerator306Inputs inputs)
    {
        int target = Actor(frame.State.ReadDword(TargetGlobal));
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        for (byte actor = 0; actor < 7; actor++)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            bool selected = (model.Flags0 & 6) == 2 && actor != target && actor != caster && !RingContainsTarget(model, target, inputs);
            frame = frame with { State = frame.State.WriteDword(Row(actor), selected ? 1u : 0u) };
            if (selected)
            {
                frame = AppendFade(frame, actor, duration, 0x5A5270, inputs);
            }
        }
        return frame;
    }

    public static (NativeBattleDegenerator306Frame Frame, int Result) Advance(NativeBattleDegenerator306Frame frame, uint taskAddress, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, taskAddress);
        return task.Callback switch
        {
            0x5A5070 => Fade(frame, taskAddress, inputs, false),
            0x5A5270 => Fade(frame, taskAddress, inputs, true),
            0x5A5540 => Move(frame, taskAddress, inputs),
            _ => throw new NotSupportedException("Degenerator actor callback is unsupported.")
        };
    }

    private static NativeBattleDegenerator306Frame AppendFade(NativeBattleDegenerator306Frame frame, byte actor, short duration, uint callback, NativeBattleDegenerator306Inputs inputs)
    {
        ushort flags = (ushort)(inputs.ReadModel(actor).Flags0 & 0x800);
        (frame, uint address) = NativeBattleDegenerator306Controller.Append(frame, callback);
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, 0).WriteWord(14, actor).WriteWord(0x18, unchecked((ushort)duration)).WriteWord(0x1A, flags);
        return NativeBattleDegenerator306Controller.Write(frame, address, task);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Fade(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Inputs inputs, bool disappear)
    {
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address);
        byte actor = checked((byte)Actor(unchecked((short)task.ReadWord(14))));
        int age = unchecked((short)task.ReadWord(12));
        int duration = unchecked((short)task.ReadWord(0x18));
        int cue = unchecked(age << 12) / duration;
        if (disappear)
        {
            cue = unchecked(4096 - cue);
            frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorAlpha(actor, 0));
        }
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorRecolor(actor, cue));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, disappear ? (ushort)0 : (ushort)4, 0x800));
        }
        ushort next = unchecked((ushort)(NativeBattleDegenerator306Controller.Read(frame, address).ReadWord(12) + 1));
        frame = NativeBattleDegenerator306Controller.Write(frame, address, NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) < duration)
        {
            return (frame, 0);
        }
        if (!disappear)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306RestoreShadow(actor));
        }
        ushort saved = NativeBattleDegenerator306Controller.Read(frame, address).ReadWord(0x1A);
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, 0x800, (ushort)(saved | (disappear ? 4 : 0))));
        return (frame, 2);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Move(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address);
        byte actor = checked((byte)Actor(unchecked((short)task.ReadWord(14))));
        int age = unchecked((short)task.ReadWord(12));
        uint row = Row(actor);
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        if (age == 0)
        {
            NativeBattleEffectArena packed = NativeBattleRaldo19ModelMemory.Capture(model);
            NativeBattleEffectArena position = NativeBattleEffectNodeContextMemory.Copy(packed, 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
            frame = frame with { State = frame.State.WriteWord(row + 4, (ushort)(model.Flags0 & 0x1020)).WriteView(row + 8, inputs.ReadRotation8(actor), 8).WriteView(row + 0x10, position, 8) };
        }
        NativeBattleEffectArena rotation = inputs.ReadRotation8(actor);
        short x = unchecked((short)(unchecked(unchecked((short)task.ReadWord(0x18)) * age) / 30));
        short y = unchecked((short)(unchecked(age << 12) / 30));
        short z = BinaryPrimitives.ReadInt16LittleEndian(rotation.Read(4, 2));
        rotation = rotation.Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, 0, 0x1020));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorRotation8(actor, rotation));
        NativeBattleVector target = new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14)));
        NativeBattleVector original = new(unchecked((short)frame.State.ReadWord(row + 0x10)), unchecked((short)frame.State.ReadWord(row + 0x12)), unchecked((short)frame.State.ReadWord(row + 0x14)));
        NativeBattleVector delta = new(unchecked((short)(target.X - original.X)), unchecked((short)(target.Y - original.Y)), unchecked((short)(target.Z - original.Z)));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(new(x, y, z)), delta);
        NativeBattleVector positionNow = new(unchecked((short)(target.X - rotated.X)), unchecked((short)(target.Y - rotated.Y)), unchecked((short)(target.Z - rotated.Z)));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorPosition(actor, positionNow));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        ushort next = unchecked((ushort)(NativeBattleDegenerator306Controller.Read(frame, address).ReadWord(12) + 1));
        frame = NativeBattleDegenerator306Controller.Write(frame, address, NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) < 30)
        {
            return (frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, 0x1020, frame.State.ReadWord(row + 4)));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorPackedPosition8(actor, frame.State.ReadView(row + 0x10, 8)));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorRotation8(actor, frame.State.ReadView(row + 8, 8)));
        return (frame, 2);
    }

    private static bool RingContainsTarget(NativeBattleGraphicsModel model, int target, NativeBattleDegenerator306Inputs inputs)
    {
        int? next = model.ReadLinkedModelIndex140();
        if (next is null || next == model.Index)
        {
            return false;
        }
        HashSet<int> visited = [];
        while (next != model.Index)
        {
            int index = next ?? throw new NotSupportedException("Degenerator linked-model cycle has an unknown next pointer.");
            if (!visited.Add(index))
            {
                throw new InvalidDataException("Degenerator linked-model cycle does not return to its selected actor.");
            }
            NativeBattleGraphicsModel linked = inputs.ReadModel(index);
            if ((linked.Flags0 & 2) != 0 && index == target)
            {
                return true;
            }
            next = linked.ReadLinkedModelIndex140();
        }
        return false;
    }

    private static int Actor(long value) => value is >= 0 and < 7 ? (int)value : throw new InvalidDataException("Degenerator actor leaves its seven physical model rows.");
    private static uint Row(int actor) => checked(ActorRows + (uint)(actor * 24));
}
