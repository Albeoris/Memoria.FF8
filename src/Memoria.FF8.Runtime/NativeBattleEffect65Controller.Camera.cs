using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    public static NativeBattleEffect65Frame AdvanceCamera(NativeBattleEffect65Frame frame, int age, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleVector saved = Vector(frame.State.ReadView(CasterPointAddress, 8));
        NativeBattleVector center = Vector(frame.State.ReadView(CenterAddress, 8));
        int x = saved.X;
        int z = saved.Z;
        if ((uint)age < 31)
        {
            if (age == 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect65CameraForce50A730());
                frame = inputs.Execute(frame, new NativeBattleEffect65CameraRoll(0));
            }
            frame = NativeBattleEffect65Camera.Interpolate6E2C10(frame, V(x - 17, -4259, z - 3499), V(x + 798, -559, z + 7315), V(x + 36, -1007, z + 6783), V(x - 83, -1416, z + 13919), 0, 0, NativeBattleCameraMath.Sine((age << 12) / 120), inputs);
        }
        if (age == 54)
        {
            frame = Pose(frame, V(x, -5086, z - 25274), V(x, -996, z + 6436), null, inputs);
        }
        if (age is >= 66 and < 96)
        {
            frame = NativeBattleEffect65Camera.Interpolate6FC250(frame, V(x + 782, 618, z - 153), V(x + 841, -685, z + 4907), V(x + 44, 15, z - 226), V(x + 253, -125, z + 2211), 0, 0, age - 66, 30, inputs);
        }
        if (age is >= 96 and < 122)
        {
            frame = NativeBattleEffect65Camera.Interpolate6FC250(frame, V(x - 1530, 157, z + 1092), V(x + 1576, -172, z + 2995), V(x - 736, 31, z + 261), V(x + 594, 0, z + 2272), -64, 288, age - 96, 26, inputs);
        }
        if (age is >= 122 and < 144)
        {
            frame = NativeBattleEffect65Camera.Interpolate6FC250(frame, V(x - 736, 31, z + 261), V(x + 594, 0, z + 2272), V(x - 517, 230, z + 150), V(x + 586, -425, z + 1816), -1184, -1376, age - 122, 22, inputs);
        }
        if (age is >= 144 and < 166)
        {
            frame = NativeBattleEffect65Camera.Interpolate6FC250(frame, V(x - 152, -90, z + 326), V(x + 220, 53, z + 1184), V(x + 70, -350, z + 327), V(x + 973, 170, z + 2983), -2496, -2496, age - 144, 22, inputs);
        }
        if (age is >= 197 and < 237)
        {
            frame = NativeBattleEffect65Camera.Interpolate6FC250(frame, V(x + 377, -2235, z + 3760), V(x + 462, -2923, z + 13202), V(x + 329, 266, z - 542), V(x + 403, -7836, z + 11602), 0, 0, age - 197, 40, inputs);
        }
        switch (age)
        {
            case 31:
                frame = Pose(frame, V(x - 265, -5128, z + 6947), V(x - 40, -3655, z + 1310), null, inputs);
                break;
            case 166:
                frame = Pose(frame, V(x - 5220, -10546, z + 10747), V(x + 380, -2871, z + 4055), 0, inputs);
                break;
            case 237:
                frame = Pose(frame, V(x - 283, 319, z + 1447), V(x + 268, -155, z - 50), 1728, inputs);
                break;
            case 263:
                frame = Pose(frame, V(x + 521, 131, z + 1438), V(x - 192, -103, z + 70), 1248, inputs);
                break;
            case 289:
                frame = Pose(frame, V(x + 516, 125, z + 1433), V(x - 192, -103, z + 70), 4096, inputs);
                break;
            case 315:
                frame = Pose(frame, V(x + 38, 141, z + 1696), V(x + 321, 75, z - 101), 3744, inputs);
                break;
        }
        x = center.X;
        z = center.Z;
        if (age is >= 341 and < 401)
        {
            frame = NativeBattleEffect65Camera.Interpolate6FC250(frame, V(x - 32, 2602, z - 743), V(x + 416, -10285, z - 17181), V(x - 100, -1132, z + 1646), V(x + 237, -5798, z - 12426), 0, 0, age - 341, 60, inputs);
        }
        if (age is >= 401 and < 469)
        {
            int ratio = 4096 - NativeBattleCameraMath.Cosine(((age - 401) << 10) / 68);
            frame = NativeBattleEffect65Camera.Interpolate6E2C10(frame, V(x - 75, -212, z + 3066), V(x + 42, -1223, z + 874), V(x - 905, 203, z + 2704), V(x + 143, -8524, z - 5113), 0, 0, ratio, inputs);
        }
        if (age == 471)
        {
            frame = Pose(frame, V(x, -6081, z + 56), V(x, 0, z + 2713), null, inputs);
        }
        return frame;
    }

    private static NativeBattleEffect65Frame Pose(NativeBattleEffect65Frame frame, NativeBattleVector eye, NativeBattleVector target, short? roll, NativeBattleEffect65Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect65CameraPoseXYZ(new(eye.X, eye.Y, eye.Z), new(target.X, target.Y, target.Z)));
        return roll is short value ? inputs.Execute(frame, new NativeBattleEffect65CameraRoll(value)) : frame;
    }

    private static NativeBattleVector V(int x, int y, int z) => new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
}
