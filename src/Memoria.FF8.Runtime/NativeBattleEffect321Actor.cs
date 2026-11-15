using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Actor
{
    public static NativeBattleEffect321Step Advance(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        BattleModelOwner model = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            NativeBattleEffectArena savedFields = PutWord(NativeBattleEffectArena.CreateUnknown(20), 0, Word(current, 0) & 0x1020);
            savedFields = CopyBytes(current, 28, savedFields, 12, 8);
            savedFields = CopyBytes(current, 12, savedFields, 4, 8);
            NativeBattleEffect321State state = frame.State;
            if (inputs.ReadPackets(frame).ModelAliases.TryEncodeExact(model, out byte originalSlot))
            {
                uint addressOfSavedFields = 0x221D420u + (uint)originalSlot * 36;
                state = state.WriteView(addressOfSavedFields, CopyBytes(savedFields, 0, NativeBattleEffectArena.CreateUnknown(2), 0, 2), 2)
                    .WriteView(addressOfSavedFields + 12, CopyBytes(savedFields, 12, NativeBattleEffectArena.CreateUnknown(8), 0, 8), 8)
                    .WriteView(addressOfSavedFields + 4, CopyBytes(savedFields, 4, NativeBattleEffectArena.CreateUnknown(8), 0, 8), 8);
            }
            frame = frame with { State = state with { SavedActors = state.SavedActors.SetItem(model, NativeBattleStorageSnapshots.Capture(savedFields)) } };
        }
        NativeBattleEffectArena saved = frame.State.SavedActors.TryGetValue(model, out NativeBattleEffectArena? previous) ? previous : throw new InvalidDataException("Effect321 requires its saved actor fields.");
        if (age < 66)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel(frame, model, 0, PutWord(NativeBattleEffectArena.Clear(), 0, Word(current, 0) | 0x1020), 2, inputs);
            frame = WriteModel(frame, model, 12, PutWord(NativeBattleEffectArena.Clear(), 0, unchecked(age * Signed(task, 24)) / 12), 2, inputs);
            frame = WriteModel(frame, model, 14, PutWord(NativeBattleEffectArena.Clear(), 0, unchecked(age << 13) / 12), 2, inputs);
            NativeBattleVector anchor = new(Signed(task, 16), Signed(task, 18), Signed(task, 20));
            NativeBattleVector original = Vector(saved, 12);
            NativeBattleVector delta = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(Vector(inputs.ReadModelArena9C(frame, model), 12));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, delta);
            int vertical;
            if (age < 12)
            {
                vertical = unchecked(NativeBattleCameraMath.Sine(unchecked(age << 10) / 12) * Signed(task, 32)) >> 12;
            }
            else if (age < 54)
            {
                vertical = Signed(task, 32);
            }
            else
            {
                vertical = unchecked(NativeBattleCameraMath.Sine(1024 + (unchecked((age - 54) << 10) / 12)) * Signed(task, 32)) >> 12;
            }
            NativeBattleEffectArena position = PutWord(PutWord(PutWord(NativeBattleEffectArena.Clear(), 0, anchor.X - rotated.X), 2, anchor.Y - rotated.Y + vertical), 4, anchor.Z - rotated.Z);
            frame = WriteModel(frame, model, 28, position, 4, inputs);
            frame = WriteModel(frame, model, 32, CopyBytes(position, 4, NativeBattleEffectArena.Clear(), 0, 2), 2, inputs);
        }
        else
        {
            frame = Restore(frame, model, saved, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (Signed(task, 12) == 66)
        {
            (frame, uint child) = Append(frame, 0x221C600, 0x5988B0);
            if (child == 0)
            {
                throw new InvalidDataException("The selected actor completion requires its available child slot.");
            }
            uint mask = 4u << (ContactOffset(task) & 31);
            NativeBattleModulePoolTask output = Read(frame, child).WriteWord(12, 0).WriteWord(18, 0).WriteWord(16, task.ReadWord(16)).WriteWord(20, task.ReadWord(20)).WriteWord(14, unchecked((ushort)mask));
            frame = Write(frame, child, output);
        }
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next < 68)
        {
            return new(frame, 0);
        }
        NativeBattleEffectArena final = inputs.ReadModelArena9C(frame, model);
        int flags = (unchecked((ushort)Word(final, 0)) & 0xEFDF) | unchecked((ushort)Word(saved, 0));
        frame = WriteModel(frame, model, 0, PutWord(NativeBattleEffectArena.Clear(), 0, flags), 2, inputs);
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        BattleActionHeaderReference header = frame.Effect.Parameters?.Header ?? throw new InvalidDataException("Effect321 requires its current action header.");
        BattleContactCursor cursor = packets.ReadHeader(header).FirstContact.Advance(ContactOffset(Read(frame, address)));
        BattleContactOccurrence contact = packets.ReadContact(cursor).Occurrence ?? throw new InvalidDataException("Effect321 requires its reached contact occurrence.");
        frame = inputs.Execute(frame, new NativeBattleEffect321Contact506690(contact));
        return new(frame, 2);
    }

    private static int ContactOffset(NativeBattleModulePoolTask task) => task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset ?? throw new InvalidDataException("Effect321 actor task requires its contact offset.");

    private static NativeBattleEffect321Frame Restore(NativeBattleEffect321Frame frame, BattleModelOwner model, NativeBattleEffectArena saved, NativeBattleEffect321Inputs inputs)
    {
        frame = WriteModel(frame, model, 28, CopyBytes(saved, 12, NativeBattleEffectArena.CreateUnknown(4), 0, 4), 4, inputs);
        frame = WriteModel(frame, model, 32, CopyBytes(saved, 16, NativeBattleEffectArena.CreateUnknown(4), 0, 4), 4, inputs);
        frame = WriteModel(frame, model, 12, CopyBytes(saved, 4, NativeBattleEffectArena.CreateUnknown(4), 0, 4), 4, inputs);
        return WriteModel(frame, model, 16, CopyBytes(saved, 8, NativeBattleEffectArena.CreateUnknown(4), 0, 4), 4, inputs);
    }

    private static NativeBattleEffect321Frame WriteModel(NativeBattleEffect321Frame frame, BattleModelOwner model, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect321Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect321WriteModelBytes(model, offset, data, length));
}
