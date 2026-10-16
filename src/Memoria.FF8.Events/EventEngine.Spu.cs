using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<NativeSpuAudioAttempt>? PrepareSpuAudio { get; set; }
    public Action? StopSpuAudio { get; set; }
    public Func<bool>? HasLiveSpuAudio { get; set; }

    private string? UnsupportedNativeSpu(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.SPUREADY or Jsm.Opcode.SPUSYNC))
        {
            return null;
        }
        try
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || !services.CanStoreGlobalSoundProcess || services.GlobalSoundProcess is not { } sound || fiber.Stack.Count < 1 || !fiber.Stack.PeekValue().IsExact)
            {
                return "Native SPU commands require their authenticated process owner and one exact original DWORD operand";
            }
            sound.Validate();
            if (services.IsMoviePlaying || Scene.MovieActors?.Active == true)
            {
                return "SPU flags2 cannot reuse a competing ordinary AVI/CAM movie lifecycle";
            }
            if (operation.Opcode == Jsm.Opcode.SPUREADY)
            {
                NativeSpuProcess.ValidateSelector(fiber.Stack.Peek());
                if (PrepareSpuAudio is null || Scene.NativeMovieMask is not { Version: 1 })
                {
                    return "SPUREADY requires the EyesOnMe audio resource port and allocated native movie-mask byte owner";
                }
            }
            else if (sound.Spu is null)
            {
                return "SPUSYNC requires its executed SPUREADY reset and current global callback counter";
            }
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteNativeSpuReady(Fiber fiber)
    {
        int selector = fiber.Stack.Peek();
        NativeSpuReadyOrigin origin = new(FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, selector);
        // The selected resource port reports its authenticated asset and actual playback outcome.
        NativeSpuAudioAttempt audio = PrepareSpuAudio!();
        NativeSpuProcess state = NativeSpuProcess.Prepare(origin, audio);
        services.SetGlobalSoundProcess(services.GlobalSoundProcess! with { Spu = state });
        //530460 sets1CE4907=1; flags2 never bind or start an ordinary AVI/CAM resource.
        Scene.NativeMovieMask = Scene.NativeMovieMask! with { Value = 1 };
        int[] operands = Pop(fiber, 1);
        WaitFrames(fiber, 1);
        return operands;
    }

    private int[] ExecuteNativeSpuSync(Fiber fiber)
    {
        int raw = fiber.Stack.Peek();
        NativeGlobalSoundProcess sound = services.GlobalSoundProcess!;
        NativeSpuProcess state = sound.Spu!.ReadSync();
        services.SetGlobalSoundProcess(sound with { Spu = state });
        if (state.SyncWaits(raw))
        {
            fiber.Position--;
            WaitFrames(fiber, 1);
            return [];
        }
        return Pop(fiber, 1);
    }

    private void AdvanceNativeSpuCallback()
    {
        if (services.GlobalSoundProcess is { Spu: { } state } sound)
        {
            services.SetGlobalSoundProcess(sound with { Spu = state.AdvanceCallback() });
        }
    }

    private void StopNativeSpuAtFieldDeparture()
    {
        if (services.GlobalSoundProcess is not { Spu: { AudioActive: true } state } sound)
        {
            return;
        }
        if (StopSpuAudio is null || HasLiveSpuAudio?.Invoke() != true)
        {
            throw new NotSupportedException("Native field departure requires its live EyesOnMe audio resource before stopping the prepared sequence.");
        }
        StopSpuAudio();
        services.SetGlobalSoundProcess(sound with { Spu = state.Stop() });
    }

    private void ValidateNativeSpuRestore(NativeGlobalSoundProcess? sound)
    {
        sound?.Spu?.Validate();
        if (sound?.Spu?.AudioActive == true && HasLiveSpuAudio?.Invoke() != true)
        {
            throw new NotSupportedException("An active EyesOnMe mid-scene SPU checkpoint requires its actual live playback resource; cold playback reconstruction is unsupported. Resume before or after this scene.");
        }
    }
}
