using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect285Controller
{
    public static NativeBattleEffect285Frame AdvanceCamera(NativeBattleEffect285Frame frame, int age, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((uint)age < 32)
        {
            if (age == 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect285CameraForce50A730());
                frame = inputs.Execute(frame, new NativeBattleEffect285CameraScalarFocal(288));
                for (int actor = 0; actor < 3; actor++)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect285ModelFlags(ModelAddress(actor), 2, ushort.MaxValue, 4));
                }
                frame = inputs.Execute(frame, new NativeBattleEffect285CameraRoll(0));
            }
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, frame.State.ReadDword(CasterGlobal));
            NativeBattleVector caster = Vector(model, 28);
            frame = BlendCameraPoint(frame, false, OffsetPoint(caster, -704, -1775, 2285), OffsetPoint(caster, 110, -2898, 2387), age * 128, inputs);
            frame = BlendCameraPoint(frame, true, OffsetPoint(caster, -1270, -17, 5028), OffsetPoint(caster, 636, 2386, 6749), age * 128, inputs);
        }
        if ((uint)(age - 16) < 32)
        {
            int elapsed = age - 16;
            frame = NativeBattleEffect285Lower.Camera67B5C0(frame, 0, elapsed, 4096, resources, inputs);
            NativeBattleEffect285CameraSnapshot camera = inputs.ReadCamera(frame);
            frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(false, OffsetY(camera.Target8, -(elapsed * 16))));
            camera = inputs.ReadCamera(frame);
            frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(true, OffsetY(camera.Eye8, -(elapsed * 4))));
        }
        if (age == 48)
        {
            frame = NativeBattleEffect285Lower.Visibility67B6C0(frame, 0, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(false, PointView(new(0, -10000, 0))));
            frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(true, PointView(new(312, -9481, -403))));
        }
        if ((uint)(age - 86) < 48)
        {
            int elapsed = age - 86;
            if (elapsed == 0)
            {
                frame = NativeBattleEffect285Lower.Visibility67B6C0(frame, 1, inputs);
                short centroid = unchecked((short)frame.State.ReadWord(CentroidGlobal));
                frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(false, PointView(new(0, -877, centroid))));
                frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(true, PointView(new(-1500, -1500, unchecked((short)(centroid + 5400))))));
                frame = DeliverWhite(frame, inputs, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = 0x7040 } : null);
            }
            if (elapsed is 1 or 8 or 10)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect285Flash5712C0(0, 1, elapsed == 1 ? 1 : 0, elapsed == 10 ? (short)96 : (short)128) { PilotIdentity = pilotIdentity });
            }
            int phase = ((elapsed << 12) / 48) >> 1;
            int angle = NativeBattleCameraMath.Sine(phase) >> 7;
            frame = NativeBattleEffect285Lower.Camera67B5C0(frame, 0, angle, 4040, resources, inputs);
        }
        return frame;
    }

    public static NativeBattleEffect285Frame RestoreSavedCamera(NativeBattleEffect285Frame frame, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleEffect285CameraSnapshot camera = inputs.ReadCamera(frame);
        short focal = camera.SavedScalarFocal ?? throw new NotSupportedException("Effect285 requires its actual captured scalar focal WORD.");
        frame = inputs.Execute(frame, new NativeBattleEffect285CameraPoint(false, camera.SavedTarget8, true));
        frame = inputs.Execute(frame, new NativeBattleEffect285CameraScalarFocal(focal));
        return inputs.Execute(frame, new NativeBattleEffect285CameraPoint(true, camera.SavedEye8, true));
    }

    private static NativeBattleVector OffsetPoint(NativeBattleVector point, int x, int y, int z) => new(unchecked((short)(point.X + x)), unchecked((short)(point.Y + y)), unchecked((short)(point.Z + z)));

    private static NativeBattleEffectArena OffsetY(NativeBattleEffectArena point, int amount)
    {
        short y = BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2));
        return point.Write(2, BitConverter.GetBytes(unchecked((short)(y + amount))));
    }

    private static NativeBattleEffect285Frame BlendCameraPoint(NativeBattleEffect285Frame frame, bool eye, NativeBattleVector first, NativeBattleVector second, int ratio, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleWeightedPointsStep blend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, first, second, (short)(4096 - ratio), (short)ratio, inputs.Colors);
        frame = frame with { Memory = blend.Memory };
        NativeBattleEffect285CameraSnapshot camera = inputs.ReadCamera(frame);
        NativeBattleEffectArena point = PutPoint(eye ? camera.Eye8 : camera.Target8, 0, blend.Position);
        return inputs.Execute(frame, new NativeBattleEffect285CameraPoint(eye, point));
    }
}
