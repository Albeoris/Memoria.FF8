namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private void AdvanceCameraShake()
    {
        if (Process.CameraShakeX is null && Process.CameraShakeY is null)
        {
            return;
        }
        IReadOnlyList<byte> random = CameraShakeRandomTable ?? throw new NotSupportedException("FFVII camera shake requires its original random table.");
        Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII camera shake requires native eased interpolation.");
        Process = Process with
        {
            CameraShakeX = Ff7NativeCameraShake.Advance(Process.CameraShakeX ?? new(), random, movement),
            CameraShakeY = Ff7NativeCameraShake.Advance(Process.CameraShakeY ?? new(), random, movement)
        };
    }
}
