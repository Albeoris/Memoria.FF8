using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoaPhysicalHit
{
    public static NativeBattleModuleCallback AdvanceProjectile(NativeBattleModuleState state, int slot, NativeBattleRinoaPhysicalHitInputs inputs, NativeBattleActionMatrix view, Func<NativeBattleModuleState, NativeBattleActionMatrix, int, int, NativeBattleModuleState> draw, Action<NativeBattleEffectArena>? publishCurveWork = null, Action<NativeBattleActionMatrix, NativeBattleActionMatrix>? publishMatrices = null)
    {
        var task = Read(state, slot);
        short age = Signed(task, 12);
        NativeBattleRinoaPhysicalHitState retained = RequireState(state);
        if (age == 15)
        {
            NativeBattleVector origin = (inputs.Modules.ReadModelBone ?? throw new NotSupportedException("Native56FE10 requires its actor502170 bone21."))(retained.Actor, 21, 0);
            retained = retained with { Origin = origin with { Y = unchecked((short)(origin.Y - 80)) } };
            state = state with { RinoaPhysicalHit = retained };
        }
        int x = retained.TargetAnchor.X - retained.Origin.X;
        int y = retained.TargetAnchor.Y - retained.Origin.Y;
        int z = retained.TargetAnchor.Z - retained.Origin.Z;
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        NativeBattleVector axis = new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z));
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), retained.Origin.X, retained.Origin.Y, retained.Origin.Z, 4096);
        int displacement = age < 4 ? -age * 128 : age < 15 ? -512 : Math.Min(age * 85 - 1787, 0);
        NativeBattleActionMatrix roll = new(NativeBattleModuleAlignment.Rotation5714F0(displacement, axis), 0, 0, 0, 4096);
        basis = basis with { Rotation = NativeBattleGfMatrices.Multiply56C090(roll, basis).Rotation };
        NativeBattleActionMatrix cameraCurve = NativeBattleGfMatrices.Compose56C2F0(view, basis);
        publishMatrices?.Invoke(basis, cameraCurve);
        if (age == 0)
        {
            //56BC50 returns the biased conversion of its stored float squared length, then56BEC0 truncates its square root.
            float squared = (float)((double)z * (float)z + (double)(float)y * (float)y + (double)x * x);
            int roundedSquared = NativePrecision53.RoundInteger(squared);
            if (roundedSquared < 0)
            {
                throw new NotSupportedException("Native56FE10 reaches an unsupported negative56BEC0 square root.");
            }
            int length = unchecked((int)(long)Math.Sqrt(roundedSquared));
            int third = length / 3;
            ImmutableArray<NativeBattleVector> controls =
            [
                new(0, 0, 0),
                new(unchecked((short)-third), 0, unchecked((short)-length)),
                new(0, 0, unchecked((short)-(length + third))),
                new(unchecked((short)third), 0, unchecked((short)-length)),
                new(0, 0, 0)
            ];
            NativeBattleEffectArena points = NativeBattleEffectArena.Clear().MarkUnknown(0, 40);
            byte[] coordinates = new byte[6];
            for (int index = 0; index < controls.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(coordinates, controls[index].X);
                BinaryPrimitives.WriteInt16LittleEndian(coordinates.AsSpan(2), controls[index].Y);
                BinaryPrimitives.WriteInt16LittleEndian(coordinates.AsSpan(4), controls[index].Z);
                points = points.Write(index * 8, coordinates);
            }
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(5, points, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
            NativeBattleEffectArena samples = NativeBattleEffectArena.Clear().MarkUnknown(0, 64 * 8);
            ImmutableArray<NativeBattleVector>.Builder curve = ImmutableArray.CreateBuilder<NativeBattleVector>(64);
            for (int index = 0; index < 64; index++)
            {
                samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(5, work, index * 4096 / 63, samples, index * 8);
                byte[] sample = samples.Read(index * 8, 6).ToArray();
                curve.Add(new(BinaryPrimitives.ReadInt16LittleEndian(sample), BinaryPrimitives.ReadInt16LittleEndian(sample.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(sample.AsSpan(4))));
            }
            publishCurveWork?.Invoke(work);
            retained = retained with { Controls = controls, Curve = curve.MoveToImmutable() };
            state = state with { RinoaPhysicalHit = retained };
        }
        if (retained.Controls.Length != 5 || retained.Curve.Length != 64)
        {
            throw new NotSupportedException("Native56FE10 requires its initialized retained571620/571690 curve.");
        }
        int start = Signed(task, 24);
        int end = start + Signed(task, 26);
        if (end >= 64)
        {
            end = start + 1;
        }
        state = draw(state, cameraCurve, start, end);
        if ((inputs.Modules.GraphicsFlags & 1) != 0)
        {
            return new(state, false, []);
        }
        short next = unchecked((short)(age + 1));
        state = Store(state, slot, current => current.WriteWord(12, unchecked((ushort)next)).WriteWord(24, unchecked((ushort)end)));
        return new(state, next > 30, []);
    }
}
