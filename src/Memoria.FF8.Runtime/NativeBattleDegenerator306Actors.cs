using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDegenerator306Actors
{
    private const uint ActorRows = 0x2244168;

    public static NativeBattleDegenerator306Frame Schedule5A5010(NativeBattleDegenerator306Frame frame, short duration, NativeBattleDegenerator306Inputs inputs)
    {
        foreach (BattleModelOwner actor in inputs.Packets.Roster.Owners)
        {
            if (frame.State.FadedActors.Contains(actor))
            {
                frame = AppendFade(frame, actor, duration, 0x5A5070, inputs);
            }
        }
        return frame;
    }

    public static NativeBattleDegenerator306Frame Schedule5A51B0(NativeBattleDegenerator306Frame frame, short duration, NativeBattleDegenerator306Inputs inputs)
    {
        BattleModelOwner target = frame.State.ReadModelOperand(TargetGlobal);
        BattleModelOwner caster = frame.State.ReadModelOperand(CasterGlobal);
        foreach (BattleModelOwner actor in inputs.Packets.Roster.Owners)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            bool selected = (model.Flags0 & 6) == 2 && actor != target && actor != caster && !RingContainsTarget(actor, model, target, inputs);
            if (inputs.Packets.ModelAliases.TryEncodeExact(actor, out byte originalSlot))
            {
                frame = frame with { State = frame.State.WriteDword(OriginalSavedRow(originalSlot), selected ? 1u : 0u) };
            }
            frame = frame with { State = frame.State with { FadedActors = selected ? frame.State.FadedActors.Add(actor) : frame.State.FadedActors.Remove(actor) } };
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

    private static NativeBattleDegenerator306Frame AppendFade(NativeBattleDegenerator306Frame frame, BattleModelOwner actor, short duration, uint callback, NativeBattleDegenerator306Inputs inputs)
    {
        ushort flags = (ushort)(inputs.ReadModel(actor).Flags0 & 0x800);
        (frame, uint address) = NativeBattleDegenerator306Controller.Append(frame, callback);
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, 0);
        task.WriteExecutionReference(14, 2, new BattleExecutionReference.Model(actor), inputs.Packets.ModelAliases.EncodeOperand(actor, 2));
        task = task.WriteWord(0x18, unchecked((ushort)duration)).WriteWord(0x1A, flags);
        return NativeBattleDegenerator306Controller.Write(frame, address, task);
    }

    private static (NativeBattleDegenerator306Frame Frame, int Result) Fade(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Inputs inputs, bool disappear)
    {
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address);
        BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
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
        BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
        int age = unchecked((short)task.ReadWord(12));
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        if (age == 0)
        {
            NativeBattleEffectArena packed = NativeBattleRaldo19ModelMemory.Capture(model);
            NativeBattleEffectArena position = NativeBattleEffectNodeContextMemory.Copy(packed, 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
            ushort flags = (ushort)(model.Flags0 & 0x1020);
            NativeBattleEffectArena savedRotation = inputs.ReadRotation8(actor);
            NativeBattleEffectArena savedFields = NativeBattleEffectArena.Clear().MarkUnknown(0, 24).Write(4, BitConverter.GetBytes(flags));
            savedFields = savedFields.CopyResourceSnapshot(savedRotation, 0, 8, 8);
            savedFields = savedFields.CopyResourceSnapshot(position, 0, 0x10, 8);
            if (inputs.Packets.ModelAliases.TryEncodeExact(actor, out byte originalSlot))
            {
                uint row = OriginalSavedRow(originalSlot);
                frame = frame with { State = frame.State.WriteWord(row + 4, flags).WriteView(row + 8, savedRotation, 8).WriteView(row + 0x10, position, 8) };
            }
            frame = frame with { State = frame.State with { SavedActors = frame.State.SavedActors.SetItem(actor, savedFields) } };
        }
        NativeBattleEffectArena rotation = inputs.ReadRotation8(actor);
        short x = unchecked((short)(unchecked(unchecked((short)task.ReadWord(0x18)) * age) / 30));
        short y = unchecked((short)(unchecked(age << 12) / 30));
        short z = BinaryPrimitives.ReadInt16LittleEndian(rotation.Read(4, 2));
        rotation = rotation.Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, 0, 0x1020));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorRotation8(actor, rotation));
        NativeBattleVector target = new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14)));
        NativeBattleEffectArena saved = frame.State.SavedActors.TryGetValue(actor, out NativeBattleEffectArena? savedFieldsForActor) ? savedFieldsForActor : throw new InvalidDataException("Degenerator actor motion requires its own saved model fields.");
        NativeBattleVector original = new(BinaryPrimitives.ReadInt16LittleEndian(saved.Read(0x10, 2)), BinaryPrimitives.ReadInt16LittleEndian(saved.Read(0x12, 2)), BinaryPrimitives.ReadInt16LittleEndian(saved.Read(0x14, 2)));
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
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags(actor, 0x1020, BinaryPrimitives.ReadUInt16LittleEndian(saved.Read(4, 2))));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorPackedPosition8(actor, NativeBattleEffectArena.Clear().CopyResourceSnapshot(saved, 0x10, 0, 8)));
        frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorRotation8(actor, NativeBattleEffectArena.Clear().CopyResourceSnapshot(saved, 8, 0, 8)));
        return (frame, 2);
    }

    private static bool RingContainsTarget(BattleModelOwner actor, NativeBattleGraphicsModel model, BattleModelOwner target, NativeBattleDegenerator306Inputs inputs)
    {
        BattleModelOwner? next = model.ReadLinkedModel();
        if (next is null || next == actor)
        {
            return false;
        }
        HashSet<BattleModelOwner> visited = [];
        while (next != actor)
        {
            BattleModelOwner linkedOwner = next ?? throw new NotSupportedException("Degenerator linked-model cycle has an unknown next pointer.");
            if (!visited.Add(linkedOwner))
            {
                throw new InvalidDataException("Degenerator linked-model cycle does not return to its selected actor.");
            }
            NativeBattleGraphicsModel linked = inputs.ReadModel(linkedOwner);
            if ((linked.Flags0 & 2) != 0 && linkedOwner == target)
            {
                return true;
            }
            next = linked.ReadLinkedModel();
        }
        return false;
    }

    private static uint OriginalSavedRow(byte originalSlot) => checked(ActorRows + (uint)(originalSlot * 24));
}
