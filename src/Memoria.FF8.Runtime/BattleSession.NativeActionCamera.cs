namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsResource NativeActionResource(NativeBattleGraphicsState graphics, int index) => graphics.Models[index].ActiveStagedResourceRow100 is not null ? NativeGraphicsPoseResource(graphics, index) : FindNativeGraphicsBinding(graphics.ActorIds[index] ?? throw new InvalidDataException("Native action model has no physical actor binding."))?.Resource ?? throw new NotSupportedException("Native action camera requires its original model resource.");

    private (NativeBattleGraphicsState Graphics, bool Selected) ChooseNativeActionCamera(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[context.HeaderIndex];
        byte command = header.ReadByte(1);
        if (command is 5 or 11 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 0xEF or 0xF4 or 0xFE)
        {
            return (graphics, true);
        }
        if (command != 8 && command is not (2 or 6 or 24 or 25 or 26 or 27 or 29 or 30 or 31 or 32 or 33 or 34))
        {
            return (graphics, false);
        }
        if (command == 6 && NativeBattleActionGraphics.Word(header, 6) == 15)
        {
            graphics = graphics with { Flags = graphics.Flags | 0x20000000 };
        }
        sbyte choice = unchecked((sbyte)header.ReadByte(3));
        if (command == 8 && choice == -1)
        {
            return (graphics, false);
        }
        byte mode = NativeCamera?.Mode ?? throw new NotSupportedException("Native505F00 requires camera mode BYTE2(1D97718).");
        if (mode == 0)
        {
            return (graphics, false);
        }
        bool hasCamera = NativeActionResource(graphics, context.ActorIndex).HasActionCamera ?? throw new NotSupportedException("Native505F00 requires its original model camera pointer.");
        if (!hasCamera)
        {
            return (graphics, false);
        }
        if (command == 8 && choice < 0)
        {
            return (graphics, true);
        }
        NativeBattleActionCameraRules rules = definition.NativeActionCameraRules ?? throw new NotSupportedException("Native action camera frequency requires its authenticated executable rules.");
        rules.Validate();
        int frequency = NativeBattleActionCameraRules.Frequency(definition.NativeSavedOptions ?? throw new NotSupportedException("Native action camera requires the full saved options DWORD1CFE73C."));
        if (command == 8)
        {
            return (graphics, rules.EnemyThreshold[frequency] + NextNativeGlobalRandom() % 193 >= 256);
        }
        byte stage = graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native5060E0 requires its506CF0-produced stage resource index.");
        if (definition.EncounterId is 1003 or 63 && (context.TargetMask & ~7) != 0 || stage == 11 || stage is >= 0x98 and <= 0x9C)
        {
            return (graphics, false);
        }
        if (mode == 2)
        {
            return (graphics, true);
        }
        NativeBattleGraphicsModel actor = graphics.Models[context.ActorIndex];
        int value = (actor.CameraAccumulator131 ?? throw new NotSupportedException("Native5060E0 requires model BYTE131.")) + rules.Increment[frequency];
        if (rules.RandomRange[frequency] != 0)
        {
            value += NextNativeGlobalRandom() % rules.RandomRange[frequency];
        }
        return (graphics with { Models = graphics.Models.SetItem(context.ActorIndex, actor with { CameraAccumulator131 = unchecked((byte)value) }) }, value >= 256);
    }

    private int NativeCameraMenuSelection(NativeBattleGraphicsState graphics)
    {
        NativeBattleUiServices menu = graphics.UiServices ?? throw new NotSupportedException("Native4A7120 requires its actual menu WORD957/955 owners.");
        if (menu.Menu957 != 2)
        {
            return 0;
        }
        sbyte choice = menu.Menu955 ?? throw new NotSupportedException("Native4A7120 requires the selected camera menu WORD955.");
        return choice >= 0 ? choice + 1 : 0;
    }

    private NativeBattleGraphicsState StartNativeHeaderCamera(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        NativeBattleCameraState camera = (NativeCamera ?? throw new NotSupportedException("Native506190 requires its original camera owner.")).SetActionSelector(65535);
        if (context.UseActionCamera != true)
        {
            NativeCamera = camera;
            return graphics;
        }
        camera = camera with { Mask = (ushort)(camera.Mask | 0x8000) };
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[context.HeaderIndex];
        byte command = header.ReadByte(1);
        NativeBattleGraphicsModel actor = graphics.Models[context.ActorIndex];
        NativeBattleGraphicsResource resource = NativeActionResource(graphics, context.ActorIndex);
        bool present = resource.HasActionCamera ?? throw new NotSupportedException("Native506190 requires its observed model camera pointer.");
        NativeBattleCameraBank? bank = present ? resource.ActionCameraBank ?? throw new InvalidDataException("Native model camera pointer lacks its immutable bank.") : null;
        byte selector;
        switch (command)
        {
            case 2 or 6 or 24 or 25 or 26 or 27 or 29 or 30 or 32 or 33 or 34:
                if (NativeCameraMenuSelection(graphics) == 1)
                {
                    camera = camera with { Mask = (ushort)(camera.Mask & 0x7FFF) };
                    break;
                }
                if ((actor.ModelId4 ?? throw new InvalidDataException("Native506190 actor lacks its loaded model ID.")) >= 16)
                {
                    byte standing = actor.SequenceState?.Default2 ?? throw new InvalidDataException("Native506190 actor lacks its sequence header BYTE2.");
                    selector = standing != 1 || header.ReadByte(2) != 11 ? (byte)1 : (byte)0;
                    camera = camera.SetActionSelector(8);
                }
                else
                {
                    selector = checked((byte)(((NextNativeGlobalRandom() & 3) + (actor.CameraSequence130 ?? throw new NotSupportedException("Native506190 requires its model BYTE130 camera index.")) + 2) % 6));
                    int targetIndex = graphics.ActionContext?.TargetIndex ?? throw new NotSupportedException("Native506190 requires its50D300 target.");
                    NativeBattleGraphicsModel target = graphics.Models[targetIndex];
                    if (NativeBattleCameraGraphics.ReadVariable(graphics, 26) > 1 || (target.ModelId4 ?? throw new InvalidDataException("Native506190 target lacks its model ID.")) < 16 && ((target.Flags8 ?? throw new InvalidDataException("Native506190 target lacks its status DWORD8.")) & 0x41021) != 0)
                    {
                        selector &= 0xFE;
                    }
                    actor = actor with { CameraSequence130 = selector };
                    graphics = graphics with { Models = graphics.Models.SetItem(context.ActorIndex, actor) };
                    camera = camera.SetActionSelector(selector);
                }
                camera = command == 6 && NativeBattleActionGraphics.Word(header, 6) == 15 ? camera with { Mask = (ushort)(camera.Mask & 0x7FFF) } : camera.Start(bank, selector);
                break;
            case 5 or 11 or 14 or 15 or 17 or 18 or 19 or 20 or 21 or 22 or 31 or 0xEF:
                selector = (byte)(NextNativeGlobalRandom() & 1);
                camera = camera.SetActionSelector(selector).Start(bank, (byte)(selector + 6)).SetSecondarySelector(NativeBattleActionGraphics.Word(header, 4) == 65534 ? (ushort)1 : (ushort)0) with { ForceCamera = true };
                break;
            case 8:
                byte choice = header.ReadByte(3);
                if ((choice & 128) != 0)
                {
                    camera = camera with { ForceCamera = true };
                }
                else if (NativeCameraMenuSelection(graphics) == 1)
                {
                    camera = camera with { Mask = (ushort)(camera.Mask & 0x7FFF) };
                    break;
                }
                selector = (byte)(choice & 127);
                camera = camera.SetActionSelector(selector).Start(bank, selector);
                break;
            case 16:
                selector = (byte)(NextNativeGlobalRandom() & 1);
                camera = camera.Start(bank, (byte)(selector + 6)).SetActionSelector(8) with { ForceCamera = true };
                break;
            case 0xF4 or 0xFE:
                selector = checked((byte)(((NextNativeGlobalRandom() & 3) + (actor.CameraSequence130 ?? throw new NotSupportedException("Native GF camera requires its model BYTE130.")) + 2) % 6));
                graphics = graphics with { Models = graphics.Models.SetItem(context.ActorIndex, actor with { CameraSequence130 = selector }) };
                camera = camera.SetActionSelector(selector).Start(bank, selector) with { ForceCamera = true };
                break;
        }
        NativeCamera = camera;
        return graphics;
    }
}
