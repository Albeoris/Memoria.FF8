using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static NativeBattleRayBomb307TaskStep AdvanceTarget(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleRayBomb307AnchorStep anchor = inputs.ReadAnchor571400(frame, actor);
            frame = anchor.Frame;
            task = task.WriteWord(16, unchecked((ushort)anchor.Anchor.X)).WriteWord(18, unchecked((ushort)anchor.Anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Anchor.Z)).WriteWord(22, unchecked((ushort)anchor.Anchor.W));
            ushort flags = (ushort)(inputs.ReadModel(actor).Flags0 & 0x1020);
            NativeBattleEffectArena position = inputs.ReadPosition8(actor);
            NativeBattleEffectArena rotation = inputs.ReadRotation8(actor);
            NativeBattleSavedActorTransform captured = new(flags, OptionalTargetWord(rotation, 0), OptionalTargetWord(rotation, 2), OptionalTargetWord(rotation, 4), unchecked((ushort?)OptionalTargetWord(rotation, 6)), new(TargetWord(position, 0), TargetWord(position, 2), TargetWord(position, 4)), TargetWord(position, 6));
            frame = frame with { Effect = frame.Effect with { SavedActorTransforms = frame.Effect.SavedActorTransforms.SetItem(actor, captured) } };
        }
        if (age <= 24)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags(actor, 0, 0x1020));
            int lift = unchecked(NativeBattleCameraMath.Sine(unchecked(age << 11) / 24) * Signed(task, 32)) >> 12;
            NativeBattleEffectArena rotation = inputs.ReadRotation8(actor).Write(0, BitConverter.GetBytes(unchecked((short)(unchecked(Signed(task, 24) * age) / 24)))).Write(2, BitConverter.GetBytes(unchecked((short)(unchecked(age << 13) / 24))));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8(actor, rotation, null));
            NativeBattleVector original = ReadSavedTarget(frame, actor).Position;
            NativeBattleVector delta = new(unchecked((short)(Signed(task, 16) - original.X)), unchecked((short)(Signed(task, 18) - original.Y)), unchecked((short)(Signed(task, 20) - original.Z)));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(TargetWord(rotation, 0), TargetWord(rotation, 2), TargetWord(rotation, 4)));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, delta);
            NativeBattleEffectArena position = inputs.ReadPosition8(actor).Write(0, BitConverter.GetBytes(unchecked((short)(Signed(task, 16) - unchecked((short)x))))).Write(2, BitConverter.GetBytes(unchecked((short)(Signed(task, 18) - unchecked((short)y) + lift)))).Write(4, BitConverter.GetBytes(unchecked((short)(Signed(task, 20) - unchecked((short)z)))));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8(actor, null, position));
        }
        else if (age <= 32)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags(actor, 0, 0x1020));
            int lift = unchecked(NativeBattleCameraMath.Sine(unchecked((age - 24) << 11) / 8) * (Signed(task, 32) >> 3)) >> 12;
            NativeBattleEffectArena position = inputs.ReadPosition8(actor).Write(2, BitConverter.GetBytes(unchecked((short)lift)));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8(actor, null, position));
        }
        frame = Write(frame, address, task);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (age is 24 or 31)
        {
            int count = age == 24 ? 16 : 12;
            for (int particle = 0; particle < count; particle++)
            {
                int slot = FindParticle(frame, 100);
                if (slot is < 0 or >= 80)
                {
                    break;
                }
                int at = slot * 24;
                frame = SlotDword(frame, at, 1);
                frame = SlotWord(frame, at + 4, 0);
                frame = DrawRandom(frame, out ushort size);
                frame = SlotWord(frame, at + 6, unchecked((short)(age == 24 ? size % 1024 + 2560 : size % 768 + 2048)));
                frame = SlotWord(frame, at + 8, Signed(task, 16));
                frame = SlotWord(frame, at + 10, 0);
                frame = SlotWord(frame, at + 12, Signed(task, 20));
                frame = DrawRandom(frame, out ushort angleDraw);
                int angle = angleDraw % 4096;
                frame = DrawRandom(frame, out ushort speedDraw);
                int speed = age == 24 ? speedDraw % 90 + 120 : speedDraw % 60 + 100;
                frame = SlotWord(frame, at + 16, unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12)));
                frame = SlotWord(frame, at + 18, 0);
                frame = SlotWord(frame, at + 20, unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12)));
                if (inputs.CapturePresentation)
                {
                    NativeBattleRayBomb307Visuals.Born(frame, slot);
                }
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(age + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) < 32)
        {
            return new(frame, 0);
        }
        NativeBattleSavedActorTransform saved = ReadSavedTarget(frame, actor);
        frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags(actor, 0x1020, saved.Flags0));
        saved = ReadSavedTarget(frame, actor);
        frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8(actor, PackTargetWords(saved.Pitch, saved.Yaw, saved.Roll, unchecked((short?)saved.RotationPadding18)), PackTargetWords(saved.Position.X, saved.Position.Y, saved.Position.Z, saved.Vertical34)));
        int offset = task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset ?? throw new InvalidDataException("Ray-Bomb target task requires its contact offset.");
        BattleContactOccurrence occurrence = inputs.Packets.ResolveContact(Parameters(frame).Header, offset);
        frame = inputs.Execute(frame, new NativeBattleRayBomb307Contact(occurrence));
        return new(frame, 2);
    }

    private static NativeBattleSavedActorTransform ReadSavedTarget(NativeBattleRayBomb307Frame frame, BattleModelOwner actor)
    {
        return frame.Effect.SavedActorTransforms.TryGetValue(actor, out NativeBattleSavedActorTransform? saved) ? saved : throw new NotSupportedException("Ray-Bomb requires its saved actor transform.");
    }

    private static short? OptionalTargetWord(NativeBattleEffectArena view, int offset)
    {
        (byte[] bytes, bool[] known) = view.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    private static NativeBattleEffectArena PackTargetWords(params short?[] words)
    {
        NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int index = 0; index < words.Length; index++)
        {
            if (words[index] is { } word)
            {
                packed = packed.Write(index * 2, BitConverter.GetBytes(word));
            }
        }
        return packed;
    }

    private static short TargetWord(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
}
