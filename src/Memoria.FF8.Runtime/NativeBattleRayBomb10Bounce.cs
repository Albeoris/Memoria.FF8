namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb10Controller
{
    private sealed partial class Execution
    {
        private int Bounce(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            int counter = Word(task, 12);
            BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(14, 2).Owner;
            if (actor is not BattlePartyModelOwner)
            {
                throw new InvalidDataException("Native629200 requires a party model owner.");
            }
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native629200 requires its actor transform.");
            if (counter == 0)
            {
                task = Point(task, inputs.ReadDefaultAnchor(actor));
                Put(index, task);
                short pitch = model.Rotation12 ?? throw new NotSupportedException("Native629200 requires actor pitch.");
                short yaw = model.Rotation14 ?? throw new NotSupportedException("Native629200 requires actor yaw.");
                short roll = model.Rotation16 ?? throw new NotSupportedException("Native629200 requires actor roll.");
                NativeBattleVector position = new(unchecked((short)transform.Position28.X), unchecked((short)transform.Position28.Y), unchecked((short)transform.Position28.Z));
                NativeBattleSavedActorTransform saved = new((ushort)(model.Flags0 & 0x1020), pitch, yaw, roll, model.RotationPadding18, position, unchecked((short)transform.Vertical34));
                Frame = Frame with { Effect = Frame.Effect with { SavedActorTransforms = Frame.Effect.SavedActorTransforms.SetItem(actor, saved) } };
            }
            if (counter <= 24)
            {
                int lift = unchecked(NativeBattleCameraMath.Sine(unchecked(counter << 11) / 24) * Word(task, 32)) >> 12;
                short pitch = unchecked((short)(unchecked(Word(task, 24) * counter) / 24));
                short yaw = unchecked((short)(unchecked(counter << 13) / 24));
                short roll = model.Rotation16 ?? throw new NotSupportedException("Native629200 requires actor roll.");
                NativeBattleVector savedPosition = ReadSavedActor(actor).Position;
                NativeBattleVector delta = new(unchecked((short)(Word(task, 16) - savedPosition.X)), unchecked((short)(Word(task, 18) - savedPosition.Y)), unchecked((short)(Word(task, 20) - savedPosition.Z)));
                NativeBattleVector rotated = NativeBattlePoseMatrices.RotateVector(new(pitch, yaw, roll), delta);
                NativeBattleVector position = new(unchecked((short)(Word(task, 16) - rotated.X)), unchecked((short)(Word(task, 18) - rotated.Y + lift)), unchecked((short)(Word(task, 20) - rotated.Z)));
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x1020), Rotation12 = pitch, Rotation14 = yaw, Transform = transform with { Position28 = position } };
                Execute(new NativeBattleRayBomb10Model(actor, model));
            }
            else if (counter <= 32)
            {
                int height = unchecked(NativeBattleCameraMath.Sine(unchecked((counter - 24) << 11) / 8) * (Word(task, 32) >> 3)) >> 12;
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x1020), Transform = transform with { Position28 = transform.Position28 with { Y = unchecked((short)height) } } };
                Execute(new NativeBattleRayBomb10Model(actor, model));
            }
            if (DrawOnly)
            {
                return 0;
            }
            if (counter == 24)
            {
                BounceDust(task, 16, 0x400, 0xA00, 90, 120, true);
                if (task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset.HasValue)
                {
                    Execute(new NativeBattleRayBomb10Sequence(actor, 6));
                }
            }
            if (counter == 31)
            {
                BounceDust(task, 12, 0x300, 0x800, 60, 100, false);
            }
            task = Add(task, 12, 1);
            Put(index, task);
            if (Word(task, 12) < 32)
            {
                return 0;
            }
            int? target = task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(26, 2).Offset;
            model = inputs.ReadModel(actor);
            if (target.HasValue && (model.Sequence ?? throw new NotSupportedException("Native629200 requires its actor sequence BYTE0.")) == 6)
            {
                return 0;
            }
            transform = model.Transform ?? throw new NotSupportedException("Native629200 lost its actor transform.");
            NativeBattleSavedActorTransform savedActor = ReadSavedActor(actor);
            model = model with { Flags0 = (ushort)((model.Flags0 & 0xEFDF) | savedActor.Flags0), Rotation12 = savedActor.Pitch, Rotation14 = savedActor.Yaw, Rotation16 = savedActor.Roll, RotationPadding18 = savedActor.RotationPadding18, Transform = transform with { Position28 = savedActor.Position, Vertical34 = savedActor.Vertical34 } };
            Execute(new NativeBattleRayBomb10Model(actor, model));
            if (target is { } offset)
            {
                BattleContactCursor cursor = inputs.Packets.ReadHeader(new BattleActionHeaderReference(0)).FirstContact.Advance(offset);
                BattleContactOccurrence occurrence = inputs.Packets.ReadContact(cursor).Occurrence ?? throw new InvalidDataException("Ray-Bomb contact has no occurrence reference.");
                Execute(new NativeBattleRayBomb10Contact(occurrence));
            }
            return 2;
        }

        private NativeBattleSavedActorTransform ReadSavedActor(BattleModelOwner actor)
        {
            return Frame.Effect.SavedActorTransforms.TryGetValue(actor, out NativeBattleSavedActorTransform? saved) ? saved : throw new NotSupportedException("Native629200 requires its saved actor transform.");
        }

        private void BounceDust(NativeBattleModulePoolTask task, int count, int scaleModulus, int scaleAdd, int speedModulus, int speedAdd, bool maskScale)
        {
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int particle = FreeParticle();
                if (particle < 0)
                {
                    return;
                }
                P32(particle, 0, 1);
                P16(particle, 4, 0);
                int random = Rand();
                P16(particle, 6, (maskScale ? random & 0x3FF : random % scaleModulus) + scaleAdd);
                P16(particle, 8, Word(task, 16));
                P16(particle, 10, 0);
                P16(particle, 12, Word(task, 20));
                int angle = Rand() & 0xFFF;
                int speed = Rand() % speedModulus + speedAdd;
                P16(particle, 16, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12);
                P16(particle, 18, 0);
                P16(particle, 20, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
            }
        }
    }
}
