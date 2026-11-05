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

    private void RestoreGameplayPresentationFade()
    {
        if (services.FieldFade is not { } fade)
        {
            return;
        }
        NativeFadeWords words = fade.Saved;
        byte sync = words.Mode == 3 ? (byte)3 : (byte)2;
        NativeFadeDraw draw = new(NativeFadeDrawMode.None, 0, 0, 0);
        switch (words.Mode)
        {
            case 2:
                words = words with { Mode = 0, Counter = 0 };
                break;
            case 3:
                words = words with { Counter = 255 };
                draw = new(NativeFadeDrawMode.Subtract, unchecked((byte)(words.Red * 255 / 256)), unchecked((byte)(words.Green * 255 / 256)), unchecked((byte)(words.Blue * 255 / 256)));
                break;
            case 4:
                draw = new(NativeFadeDrawMode.Black, 0, 0, 0);
                break;
            case 5:
            case 6:
                words = words with { Counter = words.Rate, Red = words.TargetRed, Green = words.TargetGreen, Blue = words.TargetBlue, StartRed = words.TargetRed, StartGreen = words.TargetGreen, StartBlue = words.TargetBlue };
                draw = new(words.Mode == 5 ? NativeFadeDrawMode.Add : NativeFadeDrawMode.Subtract, unchecked((byte)words.Red), unchecked((byte)words.Green), unchecked((byte)words.Blue));
                break;
            case 7:
            case 8:
                draw = new(words.Mode == 7 ? NativeFadeDrawMode.Add : NativeFadeDrawMode.Subtract, unchecked((byte)words.Red), unchecked((byte)words.Green), unchecked((byte)words.Blue));
                break;
        }
        // Saved words remain script-visible facts. Only the disposable physical presentation
        // and completion state are reconstructed; no callback, timer or reward is advanced.
        services.SetFieldFade(fade with { Physical = words, SyncMode = sync, Draw = draw });
    }
}
