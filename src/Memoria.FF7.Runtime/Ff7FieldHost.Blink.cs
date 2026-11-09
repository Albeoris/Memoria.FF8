using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public Func<PcFieldCamera?>? NativeBlinkCamera { get; set; }
    public Func<int, Ff7NativeModelPresentation>? NativeModelConfiguration { get; set; }
    public IReadOnlyList<byte>? NativeBlinkRandomTable { get; set; }

    private void InitializeBlinkState(bool restoring)
    {
        if (Process.BlinkStateInitialized)
        {
            return;
        }
        Process = Process with
        {
            BlinkStateInitialized = true,
            Actors = Process.Actors.Select(actor => actor with { NativeEyeLeft = 0, NativeEyeRight = 0, BlinkCountdown = 0 }).ToArray()
        };
        if (restoring && Process.Actors.Length != 0)
        {
            Console.Error.WriteLine("FFVII compatibility: legacy blink presentation was reset from native initial eye modes/countdowns; the separate blink index defaults to zero, and story RNG, progress and script PCs are preserved.");
        }
    }

    private void AdvanceBlink()
    {
        if (NativeBlinkRandomTable is null)
        {
            return;
        }
        Func<int, Ff7NativeModelPresentation> configuration = NativeModelConfiguration ?? throw new NotSupportedException("Native blinking requires the original field/model presentation configuration.");
        PcFieldCamera? camera = Process.Movie is { Playing: true }
            ? (NativeBlinkCamera ?? throw new NotSupportedException("Native blinking during a movie requires its current camera snapshot."))()
            : field.Cameras[0];
        Process = Ff7NativeBlink.Advance(Process, NativeBlinkRandomTable, actor => Ff7NativeBlink.Eligible(actor, camera, configuration(actor.Slot), Ff7NativeCamera.PresentationOffset(Process, camera), Process.CameraFollowPlayer == false));
    }
}
