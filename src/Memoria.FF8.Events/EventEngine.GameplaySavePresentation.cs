using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateGameplayPresentationWait(Fiber fiber)
    {
        if (fiber.WaitReason is WaitReason.Color or WaitReason.Fade)
        {
            Jsm.Opcode expected = fiber.WaitReason == WaitReason.Color ? Jsm.Opcode.COLSYNC : Jsm.Opcode.FADESYNC;
            if (fiber.Complete || !fiber.Script.ContainsOperation(fiber.Position) || program!.Operations[fiber.Position].Opcode != expected || services.FieldFade is null)
            {
                throw new InvalidDataException("Gameplay color/fade wait requires its held synchronization instruction and logical fade owner.");
            }
        }
        if (fiber.WaitReason == WaitReason.SoundResource && (fiber.Complete || !fiber.Script.ContainsOperation(fiber.Position - 1) || program!.Operations[fiber.Position - 1].Opcode != Jsm.Opcode.EFFECTLOAD || fiber.SoundResourceId is < 0 or > byte.MaxValue || !Scene.LoadedSoundEffects.ContainsKey(fiber.SoundResourceId)))
        {
            throw new InvalidDataException("Gameplay sound-resource wait requires its consumed load instruction and resource identity.");
        }
    }

    private static int GameplayPresentationResumePosition(Fiber fiber)
    {
        // Color/fade sync holds the instruction without consuming operands. Gameplay loading
        // settles that visual transition and continues after the sync, without running scripts.
        return fiber.WaitReason is WaitReason.Color or WaitReason.Fade ? checked(fiber.Position + 1) : fiber.Position;
    }

    private GameplaySaveFieldFadeEndpoint? CaptureGameplayFadeEndpoint()
    {
        if (services.FieldFade is not { } fade)
        {
            return null;
        }
        NativeFadeWords words = fade.Physical;
        GameplaySaveFieldFadeEndpoint endpoint = words.Mode switch
        {
            2 => new(0, 0, 0, 0, fade.SyncMode),
            5 or 6 => new(words.Mode == 5 ? (short)7 : (short)8, words.TargetRed, words.TargetGreen, words.TargetBlue, fade.SyncMode),
            _ => new(words.Mode, words.Red, words.Green, words.Blue, fade.SyncMode)
        };
        endpoint.Validate();
        return endpoint;
    }

    private void RestoreGameplayPresentationFade(GameplaySaveFieldFadeEndpoint? endpoint)
    {
        if (endpoint is null || services.FieldFade is not { } fade)
        {
            // Older gameplay documents did not own physical presentation. Keep their
            // existing fresh-allocation interpretation rather than inventing a phase.
            return;
        }
        endpoint.Validate();
        short counter = endpoint.Mode == 3 ? (short)255 : (short)0;
        NativeFadeWords words = new(endpoint.Mode, counter, counter, endpoint.Red, endpoint.Green, endpoint.Blue, endpoint.Red, endpoint.Green, endpoint.Blue, endpoint.Red, endpoint.Green, endpoint.Blue);
        NativeFadeDraw draw = endpoint.Mode switch
        {
            3 => new(NativeFadeDrawMode.Subtract, unchecked((byte)(words.Red * 255 / 256)), unchecked((byte)(words.Green * 255 / 256)), unchecked((byte)(words.Blue * 255 / 256))),
            4 => new(NativeFadeDrawMode.Black, 0, 0, 0),
            7 or 8 => new(endpoint.Mode == 7 ? NativeFadeDrawMode.Add : NativeFadeDrawMode.Subtract, unchecked((byte)words.Red), unchecked((byte)words.Green), unchecked((byte)words.Blue)),
            _ => new(NativeFadeDrawMode.None, 0, 0, 0)
        };
        // Saved words remain script-visible facts; only disposable physical presentation
        // and completion are reconstructed. Black mode remains observable to movement.
        services.SetFieldFade(fade with { Physical = words, SyncMode = endpoint.SyncMode, Draw = draw });
    }
}
