namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWildCannonBlow156Controller
{
    private static NativeBattleWildCannonBlow156Frame PrepareTrajectory(NativeBattleWildCannonBlow156Frame frame, short index, NativeBattleWildCannonBlow156Inputs inputs)
    {
        uint start = 0x23F03C8u + (uint)(index * 8);
        uint target = 0x23F0318u + (uint)(index * 8);
        uint points = 0x23F0298u + (uint)(index * 32);
        int caster = checked((int)frame.State.ReadDword(0x23F03BC));
        NativeBattleWildCannonBlow156PointStep bone = inputs.ReadBone502170(frame, caster, (index & 1) == 0 ? 9 : 15, 4096);
        frame = bone.Frame with { State = bone.Frame.State.WriteView(start, bone.Point8, 6) };
        NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, caster);
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(world, new((short)((index & 1) == 0 ? -200 : 200), -250, -250));
        frame = Word(Word(Word(frame, start, Word(frame, start) + offset.X), start + 2, Word(frame, start + 2) + offset.Y), start + 4, Word(frame, start + 4) + offset.Z);
        NativeBattleWildCannonBlow156PointStep center = inputs.ReadTargetCenter(frame, Parameters(frame).TargetMask7A);
        frame = center.Frame with { State = center.Frame.State.WriteView(target, center.Point8, 6) };
        frame = DrawRandom(frame, out ushort randomX);
        frame = Word(frame, target, Word(frame, target) + randomX % 1600 - 800);
        frame = DrawRandom(frame, out ushort randomZ);
        frame = Word(frame, target + 4, Word(frame, target + 4) + randomZ % 800 - 400);
        var direction = NativeBattleGfMatrices.RotateLong56C600(world, 0, 0, -4096);
        frame = Copy(Copy(frame, start, points, 8), start, points + 8, 8);
        frame = Word(frame, points + 8, Word(frame, points + 8) + (unchecked(direction.X * 600) >> 12));
        frame = Word(frame, points + 10, Word(frame, points + 10) - 500);
        frame = Word(frame, points + 12, Word(frame, points + 12) + (unchecked(direction.Z * 600) >> 12));
        frame = Word(frame, points + 16, (Word(frame, start) + Word(frame, target)) / 2);
        frame = Word(frame, points + 18, Word(frame, points + 10) - 1400);
        frame = Word(frame, points + 20, (Word(frame, start + 4) + Word(frame, target + 4)) / 2);
        frame = Copy(frame, target, points + 24, 8);
        (frame, uint temporary) = Allocate(frame, 400);
        NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(4, frame.State.ReadView(points, 32), 0, Context(frame, temporary, 400));
        frame = PublishContext(frame, temporary, work, 400);
        uint samples = 0x23F03E8u + (uint)(index * 256);
        NativeBattleEffectArena rows = frame.State.ReadView(samples, 256);
        for (int sample = 0; sample < 32; sample++)
        {
            rows = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(4, work, sample * 4096 / 31, rows, sample * 8);
        }
        frame = frame with { State = frame.State.WriteView(samples, rows, 256) };
        frame = Release(frame, 400);
        var tangent = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(frame, points + 8) - Word(frame, points), Word(frame, points + 10) - Word(frame, points + 2), Word(frame, points + 12) - Word(frame, points + 4));
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, tangent.X, tangent.Y, tangent.Z);
        var rotation = NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis);
        uint matrix = 0x23F0338u + (uint)(index * 32);
        for (int element = 0; element < 9; element++)
        {
            frame = Word(frame, matrix + (uint)(element * 2), rotation[element]);
        }
        frame = Dword(Dword(Dword(frame, matrix + 20, unchecked((uint)(int)Word(frame, points))), matrix + 24, unchecked((uint)(int)Word(frame, points + 2))), matrix + 28, unchecked((uint)(int)Word(frame, points + 4)));
        return frame;
    }
}
