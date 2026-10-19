namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb10Controller
{
    private sealed partial class Execution
    {
        private void SaveWord(int actor, int offset, ushort? value)
        {
            saved[actor * 20 + offset] = value.HasValue ? unchecked((byte)value.Value) : null;
            saved[actor * 20 + offset + 1] = value.HasValue ? unchecked((byte)(value.Value >> 8)) : null;
        }

        private ushort? SavedOptionalWord(int actor, int offset)
        {
            byte? low = saved[actor * 20 + offset];
            byte? high = saved[actor * 20 + offset + 1];
            if (low.HasValue != high.HasValue)
            {
                throw new NotSupportedException("Native629200 requires both physical bytes of the retained actor WORD.");
            }
            return low.HasValue ? (ushort)(low.Value | high!.Value << 8) : null;
        }

        private short SavedWord(int actor, int offset) => unchecked((short)(SavedOptionalWord(actor, offset) ?? throw new NotSupportedException("Native629200 requires its saved actor WORD producer.")));

        private int Bounce(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            int counter = Word(task, 12);
            int actor = Word(task, 14);
            if (actor is < 0 or > 2)
            {
                throw new NotSupportedException("Native629200 requires one of its three physical party actors.");
            }
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native629200 requires its actor transform.");
            if (counter == 0)
            {
                task = Point(task, inputs.ReadDefaultAnchor(actor));
                Put(index, task);
                SaveWord(actor, 0, (ushort)(model.Flags0 & 0x1020));
                SaveWord(actor, 4, unchecked((ushort)(model.Rotation12 ?? throw new NotSupportedException("Native629200 requires actor pitch."))));
                SaveWord(actor, 6, unchecked((ushort)(model.Rotation14 ?? throw new NotSupportedException("Native629200 requires actor yaw."))));
                SaveWord(actor, 8, unchecked((ushort)(model.Rotation16 ?? throw new NotSupportedException("Native629200 requires actor roll."))));
                SaveWord(actor, 10, model.RotationPadding18);
                SaveWord(actor, 12, unchecked((ushort)transform.Position28.X));
                SaveWord(actor, 14, unchecked((ushort)transform.Position28.Y));
                SaveWord(actor, 16, unchecked((ushort)transform.Position28.Z));
                SaveWord(actor, 18, unchecked((ushort)transform.Vertical34));
            }
            if (counter <= 24)
            {
                int lift = unchecked(NativeBattleCameraMath.Sine(unchecked(counter << 11) / 24) * Word(task, 32)) >> 12;
                short pitch = unchecked((short)(unchecked(Word(task, 24) * counter) / 24));
                short yaw = unchecked((short)(unchecked(counter << 13) / 24));
                short roll = model.Rotation16 ?? throw new NotSupportedException("Native629200 requires actor roll.");
                NativeBattleVector delta = new(unchecked((short)(Word(task, 16) - SavedWord(actor, 12))), unchecked((short)(Word(task, 18) - SavedWord(actor, 14))), unchecked((short)(Word(task, 20) - SavedWord(actor, 16))));
                NativeBattleVector rotated = NativeBattlePoseMatrices.RotateVector(new(pitch, yaw, roll), delta);
                NativeBattleVector position = new(unchecked((short)(Word(task, 16) - rotated.X)), unchecked((short)(Word(task, 18) - rotated.Y + lift)), unchecked((short)(Word(task, 20) - rotated.Z)));
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x1020), Rotation12 = pitch, Rotation14 = yaw, Transform = transform with { Position28 = position } };
                Execute(new NativeBattleRayBomb10Model(model));
            }
            else if (counter <= 32)
            {
                int height = unchecked(NativeBattleCameraMath.Sine(unchecked((counter - 24) << 11) / 8) * (Word(task, 32) >> 3)) >> 12;
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x1020), Transform = transform with { Position28 = transform.Position28 with { Y = unchecked((short)height) } } };
                Execute(new NativeBattleRayBomb10Model(model));
            }
            if (DrawOnly)
            {
                return 0;
            }
            if (counter == 24)
            {
                BounceDust(task, 16, 0x400, 0xA00, 90, 120, true);
                if (Word(task, 26) >= 0)
                {
                    Execute(new NativeBattleRayBomb10Sequence(checked((byte)actor), 6));
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
            int target = Word(task, 26);
            model = inputs.ReadModel(actor);
            if (target >= 0 && (model.Sequence ?? throw new NotSupportedException("Native629200 requires its actor sequence BYTE0.")) == 6)
            {
                return 0;
            }
            transform = model.Transform ?? throw new NotSupportedException("Native629200 lost its actor transform.");
            model = model with { Flags0 = (ushort)((model.Flags0 & 0xEFDF) | unchecked((ushort)SavedWord(actor, 0))), Rotation12 = SavedWord(actor, 4), Rotation14 = SavedWord(actor, 6), Rotation16 = SavedWord(actor, 8), RotationPadding18 = SavedOptionalWord(actor, 10), Transform = transform with { Position28 = new(SavedWord(actor, 12), SavedWord(actor, 14), SavedWord(actor, 16)), Vertical34 = SavedWord(actor, 18) } };
            Execute(new NativeBattleRayBomb10Model(model));
            if (target >= 0)
            {
                int first = inputs.Packets.Headers[0].FirstContactIndex ?? throw new NotSupportedException("Native629200 requires its first header contact pointer.");
                Execute(new NativeBattleRayBomb10Contact(checked(first + target)));
            }
            return 2;
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
