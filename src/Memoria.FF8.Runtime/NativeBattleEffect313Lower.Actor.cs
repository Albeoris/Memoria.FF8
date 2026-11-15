using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Lower
{
    public static NativeBattleEffect313Step AdvanceActor(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        BattleModelOwner model = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
        if (model is not BattlePartyModelOwner)
        {
            throw new InvalidDataException("Effect313 actor callback requires its selected party model.");
        }
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleEffect313PointStep anchor = inputs.Anchor571400(frame, model, TaskView(task, 16, 8));
            frame = Write(anchor.Frame, address, CopyTask(Read(anchor.Frame, address), 16, anchor.Output8, 8));
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            NativeBattleEffectArena savedFields = NativeBattleEffectArena.Clear().MarkUnknown(0, 20);
            savedFields = PutWord313(savedFields, 0, unchecked((ushort)Word313(current, 0)) & 0x1020);
            savedFields = CopyBytes(current, 12, savedFields, 4, 8);
            savedFields = CopyBytes(current, 28, savedFields, 12, 8);
            NativeBattleModelAliases aliases = inputs.ReadPackets(frame).ModelAliases;
            if (aliases.TryEncodeExact(model, out byte originalSlot))
            {
                uint original = checked(0x223CC38u + (uint)originalSlot * 20);
                frame = frame with { State = frame.State.WriteView(original, CopyBytes(savedFields, 0, NativeBattleEffectArena.Clear(), 0, 2), 2).WriteView(original + 4, CopyBytes(savedFields, 4, NativeBattleEffectArena.Clear(), 0, 8), 8).WriteView(original + 12, CopyBytes(savedFields, 12, NativeBattleEffectArena.Clear(), 0, 8), 8) };
            }
            frame = frame with { State = frame.State with { SavedActors = frame.State.SavedActors.SetItem(model, savedFields) } };
            task = Read(frame, address);
        }
        NativeBattleEffectArena saved = frame.State.SavedActors.TryGetValue(model, out NativeBattleEffectArena? fields) ? fields : throw new InvalidDataException("Effect313 requires its own saved actor fields.");
        if (age <= 24)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel313(frame, model, 0, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked((ushort)Word313(current, 0)) | 0x1020), 2, inputs);
            frame = WriteModel313(frame, model, 12, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked(Signed(task, 24) * age) / 24), 2, inputs);
            frame = WriteModel313(frame, model, 14, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked(age << 13) / 24), 2, inputs);
            NativeBattleVector anchor = new(Signed(task, 16), Signed(task, 18), Signed(task, 20));
            NativeBattleVector original = Vector(CopyBytes(saved, 12, NativeBattleEffectArena.Clear(), 0, 8));
            NativeBattleVector delta = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(Vector(inputs.ReadModelArena9C(frame, model), 12));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, delta);
            int bounce = unchecked(NativeBattleCameraMath.Sine(unchecked(age << 11) / 24) * Signed(task, 32)) >> 12;
            NativeBattleEffectArena position = PutWord313(PutWord313(PutWord313(NativeBattleEffectArena.Clear(), 0, anchor.X - rotated.X), 2, anchor.Y - rotated.Y + bounce), 4, anchor.Z - rotated.Z);
            frame = WriteModel313(frame, model, 28, position, 4, inputs);
            frame = WriteModel313(frame, model, 32, CopyBytes(position, 4, NativeBattleEffectArena.Clear(), 0, 2), 2, inputs);
        }
        else if (age <= 32)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel313(frame, model, 0, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked((ushort)Word313(current, 0)) | 0x1020), 2, inputs);
            int bounce = unchecked(NativeBattleCameraMath.Sine(unchecked((age - 24) << 11) / 8) * (Signed(task, 32) >> 3)) >> 12;
            frame = WriteModel313(frame, model, 30, PutWord313(NativeBattleEffectArena.Clear(), 0, bounce), 2, inputs);
        }
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (Signed(task, 12) is 24 or 31)
        {
            frame = BirthActorRecords313(frame, task, Signed(task, 12) == 24, inputs.CapturePresentation);
            if (Signed(task, 12) == 24 && task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset is not null)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect313SelectSequence505C00(model, 6));
            }
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next < 32)
        {
            return new(frame, 0);
        }
        int? contact = task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset;
        if (contact is not null && inputs.ReadSequenceHeaderByte0(frame, model) == 6)
        {
            return new(frame, 0);
        }
        NativeBattleEffectArena final = inputs.ReadModelArena9C(frame, model);
        int flags = (unchecked((ushort)Word313(final, 0)) & 0xEFDF) | unchecked((ushort)Word313(CopyBytes(saved, 0, NativeBattleEffectArena.Clear(), 0, 2), 0));
        frame = WriteModel313(frame, model, 0, PutWord313(NativeBattleEffectArena.Clear(), 0, flags), 2, inputs);
        frame = WriteModel313(frame, model, 28, CopyBytes(saved, 12, NativeBattleEffectArena.Clear(), 0, 4), 4, inputs);
        frame = WriteModel313(frame, model, 32, CopyBytes(saved, 16, NativeBattleEffectArena.Clear(), 0, 4), 4, inputs);
        frame = WriteModel313(frame, model, 12, CopyBytes(saved, 4, NativeBattleEffectArena.Clear(), 0, 4), 4, inputs);
        frame = WriteModel313(frame, model, 16, CopyBytes(saved, 8, NativeBattleEffectArena.Clear(), 0, 4), 4, inputs);
        if (contact is int relativeIndex)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect313 requires its current action header.");
            BattleContactOccurrence occurrence = packets.ResolveContact(parameters.Header, relativeIndex);
            frame = inputs.Execute(frame, new NativeBattleEffect313Contact506690(occurrence));
        }
        return new(frame, 2);
    }

    public static NativeBattleEffect313Frame BirthActorRecords313(NativeBattleEffect313Frame frame, NativeBattleModulePoolTask task, bool firstWave, bool capturePresentation = false)
    {
        int count = firstWave ? 16 : 12;
        for (int attempt = 0; attempt < count; attempt++)
        {
            uint row = 0;
            for (int index = 0; index < 80; index++)
            {
                uint candidate = checked(0x223B698u + (uint)index * 24);
                if (frame.State.ReadDword(candidate) == 0)
                {
                    row = candidate;
                    break;
                }
            }
            if (row == 0)
            {
                break;
            }
            if (capturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, NativeBattleEffect313Profile.RecordsAddress, checked((int)((row - NativeBattleEffect313Profile.RecordsAddress) / NativeBattleEffect313Profile.RecordStride)));
            }
            NativeBattleEffectArena record = frame.State.ReadView(row, 24);
            record = PutWord313(Put313(record, 0, 1), 4, 0);
            int scale = firstWave ? Random313(ref frame) % 1024 + 2560 : Random313(ref frame) % 768 + 2048;
            record = PutWord313(PutWord313(PutWord313(PutWord313(record, 6, scale), 8, Signed(task, 16)), 10, 0), 12, Signed(task, 20));
            int angle = Random313(ref frame) % 4096;
            int speed = firstWave ? Random313(ref frame) % 90 + 120 : Random313(ref frame) % 60 + 100;
            record = PutWord313(PutWord313(PutWord313(record, 16, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12), 18, 0), 20, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
            frame = frame with { State = frame.State.WriteView(row, record, 24) };
        }
        return frame;
    }

    private static NativeBattleEffect313Frame WriteModel313(NativeBattleEffect313Frame frame, BattleModelOwner model, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect313Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect313WriteModelBytes(model, offset, data, length));
}
