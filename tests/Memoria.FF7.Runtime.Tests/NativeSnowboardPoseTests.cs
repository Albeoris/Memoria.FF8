using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardPoseTests
{
    internal static void Run()
    {
        Ff7SnowboardJointBinding first = new(Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity, -1, -1, 16, false);
        Ff7SnowboardJointBinding second = first with { PositionParent = 0, RotationParent = 0 };
        Ff7SnowboardPresentationData data = new([first, second], [new(2, 3, 0, 0), new(7, 2, 1, 1), new(-1, -1, -1, -1)], [new(new Vector3[2], new(0, 1031, 0)), new(new[] { new Vector3(0, 0, MathF.PI / 2), Vector3.Zero }, new(16, 1031, 0))]);
        string source = JsonSerializer.Serialize(data);
        Ff7SnowboardPose nativeInitial = new(data with { Poses = [data.Poses[1]] });
        var nativeInitialPart = nativeInitial.Sample(Matrix4x4.Identity)[0];
        Near(nativeInitialPart.Transform.Translation, new(0, -1031f / 16, -0.125f));
        Near(Vector3.TransformNormal(Vector3.UnitX, nativeInitialPart.Transform), Vector3.UnitX);
        Matrix4x4 nativePlacement = Ff7SnowboardPose.BuildActorPlacement(new(4096, 8192, 12288), 0, Vector3.Zero);
        Near(nativePlacement.Translation, new(1, 2, 3));
        Near(Vector3.TransformNormal(Vector3.UnitX, nativePlacement), -Vector3.UnitZ);
        Near(Vector3.TransformNormal(Vector3.UnitX, Ff7SnowboardPose.BuildActorPlacement(Vector3.Zero, -MathF.PI * 0.5f, Vector3.Zero)), Vector3.UnitX);
        Ff7SnowboardPose pose = new(data, 0);
        var initial = pose.Sample(Matrix4x4.Identity);
        Check(initial.Count == 2 && initial[0].ModelIndex == 2 && initial[1].NativePrimitiveKind == 2, "Native draw bindings were confused with joint or pose indexes.");
        Near(initial[0].Transform.Translation, new(0, 0, -0.125f));
        Near(initial[1].Transform.Translation, new(0, 0, -0.25f));
        pose.Begin(new(1, 2, 0));
        Check(pose.Remaining == 2 && pose.Sample(Matrix4x4.Identity)[0].Transform == initial[0].Transform, "Selecting a target pose advanced interpolation immediately.");
        pose.Advance();
        Check(pose.Remaining == 1, "A pose callback did not consume exactly one native frame.");
        Near(pose.Sample(Matrix4x4.Identity)[0].Transform.Translation.X, 0.5f);
        pose.Advance();
        var final = pose.Sample(Matrix4x4.Identity);
        Near(final[0].Transform.Translation, new(1, 0, -0.125f));
        Near(Vector3.TransformNormal(Vector3.UnitX, final[0].Transform), -Vector3.UnitY);
        Near(Vector3.TransformNormal(Vector3.UnitX, final[1].Transform), -Vector3.UnitY);
        pose.Advance();
        Check(final[0].Transform == pose.Sample(Matrix4x4.Identity)[0].Transform, "A completed native pose continued changing.");
        Matrix4x4 placement = Matrix4x4.CreateTranslation(100, 200, 300);
        Near(pose.Sample(placement)[0].Transform.Translation, final[0].Transform.Translation + new Vector3(100, 200, 300));
        foreach (byte easing in new byte[] { 0, 1, 2, 3 })
        {
            Ff7SnowboardPose eased = new(data, 0);
            eased.Begin(new(1, 2, easing));
            eased.Advance();
            Near(eased.Sample(Matrix4x4.Identity)[0].Transform.Translation.X, Ff7SnowboardPose.Ease(0.5f, easing));
            eased.Advance();
            Near(eased.Sample(Matrix4x4.Identity)[0].Transform.Translation.X, 1);
        }
        Near(Ff7SnowboardPose.Ease(0.5f, 2), 0.2928932f);
        Near(Ff7SnowboardPose.Ease(0.5f, 3), 0.7071068f);
        Ff7SnowboardPose zero = new(data, 0);
        zero.Begin(new(1, 0, 0));
        zero.Advance();
        Check(zero.Sample(Matrix4x4.Identity)[0].Transform == initial[0].Transform, "A zero-duration target manufactured a native interpolation callback.");
        pose.SetReference(0);
        Check(pose.Sample(Matrix4x4.Identity)[0].Transform == final[0].Transform, "Setting a native reference changed the current displayed pose.");
        pose.Blend(-1, 1, 0);
        Near(pose.Sample(Matrix4x4.Identity)[0].Transform.Translation.X, 1);
        Near(Vector3.TransformNormal(Vector3.UnitX, pose.Sample(Matrix4x4.Identity)[0].Transform), Vector3.UnitX);
        pose.Blend(0, 1, 0.5f);
        Near(pose.Sample(Matrix4x4.Identity)[0].Transform.Translation.X, 0.5f);
        Check(pose.Remaining == 0, "Direct native pose blending advanced or restarted the frame timer.");
        pose.Blend(0, 1, 1.5f);
        Near(pose.Sample(Matrix4x4.Identity)[0].Transform.Translation.X, 1.5f);
        Check(source == JsonSerializer.Serialize(data), "Sampling or animation changed the selected native presentation data.");
        Console.WriteLine("Native snowboard pose interpolation, separate joint parents and draw transforms passed.");
    }

    private static void Near(Vector3 actual, Vector3 expected)
    {
        Check(Vector3.Distance(actual, expected) < 0.0001f, $"Unexpected native pose vector {actual}, expected {expected}.");
    }

    private static void Near(float actual, float expected)
    {
        Check(Math.Abs(actual - expected) < 0.0001f, $"Unexpected native pose scalar {actual}, expected {expected}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
