namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltdownController
{
    private sealed partial class Execution
    {
        private bool Director(ref NativeBattleModulePoolTask task)
        {
            int action = S(task, 14);
            NativeBattleModuleAnchor target = Anchor(Target(action));
            NativeBattleModuleAnchor caster = Anchor(0x2466630);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(target.X - caster.X, target.Y - caster.Y, target.Z - caster.Z);
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
            NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), unchecked((direction.X * 900 >> 12) + caster.X), unchecked((direction.Y * 900 >> 12) + caster.Y), unchecked((direction.Z * 900 >> 12) + caster.Z), 4096);
            basis = NativeBattlePoseMatrices.Scale56BEF0(basis, 0x600, 0x600, 0x600);
            Matrix(ActionFrame(action), NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native Meltdown requires its current camera matrix."), basis));
            if (inputs.DrawOnly)
            {
                return false;
            }
            int age = S(task, 12);
            int targetActor = S(task, 32);
            int casterActor = S(task, 34);
            if (age == 0)
            {
                target = NativeBattlePoseMatrices.ReadAnchor(inputs.ReadModel(targetActor), inputs.ReadPoseSource(targetActor));
                caster = NativeBattlePoseMatrices.ReadAnchor(inputs.ReadModel(casterActor), inputs.ReadPoseSource(casterActor));
                if (target.Y > caster.Y && casterActor < 3)
                {
                    target = target with { Y = caster.Y };
                }
                Anchor(Target(action), target);
                Anchor(0x2466630, caster);
            }
            if (age == 40)
            {
                ushort previous = (ushort)(inputs.ReadModel(targetActor).Flags0 & 0xC);
                Add(0x612540, (node, slot) => Set(slot, node.WriteWord(12, 0).WriteWord(14, unchecked((ushort)action)).WriteWord(24, unchecked((ushort)targetActor)).WriteWord(26, previous)));
            }
            if (age == 1)
            {
                Add(0x612D90, (node, slot) => Set(slot, node.WriteWord(12, 0).WriteWord(14, unchecked((ushort)action)).WriteWord(28, 0xC00).WriteWord(30, 0x80)));
                Add(0x613C40, (node, slot) => Set(slot, node.WriteWord(12, 0).WriteWord(14, unchecked((ushort)action))));
                Add(0x613EF0, (node, slot) => Set(slot, node.WriteWord(12, 0).WriteWord(14, unchecked((ushort)action))));
                for (uint address = 0x2464BB0; address < 0x2465510; address += 20)
                {
                    W(address, -1);
                }
            }
            if (age == 35)
            {
                Add(0x613A50, (node, slot) => Set(slot, node.WriteWord(12, 0).WriteWord(14, unchecked((ushort)action)).WriteWord(28, 0x300).WriteWord(30, 0x600)));
                Emit(new NativeBattleMeltdownFade());
            }
            NativeBattleEffectParameters parameters = current.Effect.Parameters!;
            if (age == 80)
            {
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C + action];
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Meltdown requires its original result target list.");
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    Emit(new NativeBattleMeltdownContact(first + index));
                }
            }
            if (age == 85 && action + 1 <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17))
            {
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C + action + 1];
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Meltdown requires its next action's first target.");
                Add(0x612190, (node, slot) => Set(slot, node.WriteWord(12, 0).WriteWord(14, unchecked((ushort)(action + 1))).WriteWord(32, inputs.Packets.ReadContact(first).Primary.ActorIndex).WriteWord(34, header.ReadByte(0))));
            }
            if (age == 1)
            {
                Emit(new NativeBattleMeltdownSound(resources.Sound));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 > 90;
        }

        private bool Melt(ref NativeBattleModulePoolTask task)
        {
            int age = S(task, 12);
            int actor = S(task, 24);
            Emit(new NativeBattleMeltdownActor(actor, Anchor(Target(S(task, 14))), Anchor(0x2466630), age < 28 ? age * 81 : 0x305B - age * 364, age < 34));
            if (inputs.DrawOnly)
            {
                return false;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            if (age + 1 <= 34)
            {
                return false;
            }
            Emit(new NativeBattleMeltdownFlags(actor, (ushort)((inputs.ReadModel(actor).Flags0 & 0xFFF3) | task.ReadWord(26))));
            return true;
        }

        private bool Disc(ref NativeBattleModulePoolTask task)
        {
            int age = S(task, 12);
            int fade = age < 8 ? (8 - age) << 9 : age >= 65 ? (age - 65) << 9 : 0;
            NativeBattleActionMatrix matrix = ScaleFrame(S(task, 14), task.ReadWord(28), task.ReadWord(28));
            Emit(new NativeBattleMeltdownPackets(NativeBattleMeltdownDrawing.Prim612EE0(resources.Disc, matrix, inputs.Projection, inputs.Colors, age < 8 || age >= 65 ? 0xF0u : 0x30u, fade, 0, -12 * age, 0, 0x80, 0x80, 0x80)));
            if (inputs.DrawOnly)
            {
                return false;
            }
            task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + task.ReadWord(30)))).WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 73;
        }

        private bool Glow(ref NativeBattleModulePoolTask task)
        {
            int age = S(task, 12);
            int fade = age < 6 ? 4096 - age * 682 : age >= 32 ? (age - 32) << 9 : 0;
            uint flags = age < 6 || age >= 32 ? 0xF0u : 0x30u;
            int size = task.ReadWord(28);
            NativeBattleActionMatrix matrix = ScaleFrame(S(task, 14), size, 0x4000);
            Emit(new NativeBattleMeltdownPackets(NativeBattleMeltdownDrawing.Prim612EE0(resources.FlatGlow, matrix, inputs.Projection, inputs.Colors, flags, fade, age * 24, 0, 0, 0, 0x80, 0x80)));
            matrix = ScaleFrame(S(task, 14), size, size);
            Emit(new NativeBattleMeltdownPackets(NativeBattleMeltdownDrawing.Prim612EE0(resources.Glow, matrix, inputs.Projection, inputs.Colors, flags, fade, 0, age * 8, 0x40, 0, 0x40, 0x80)));
            if (inputs.DrawOnly)
            {
                return false;
            }
            int step = S(task, 30);
            if (age >= 0)
            {
                task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + step))).WriteWord(30, unchecked((ushort)(step - (step >> 3))));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 40;
        }

        private NativeBattleActionMatrix ScaleFrame(int action, int size, int depthSize)
        {
            W(0x2476640, size);
            W(0x2476648, size);
            W(0x2476650, depthSize);
            D(0x2476654, 0);
            D(0x2476658, 0);
            D(0x247665C, 0);
            return NativeBattleGfMatrices.Compose56C2F0(Matrix(ActionFrame(action)), Matrix(0x2476640));
        }
    }
}
