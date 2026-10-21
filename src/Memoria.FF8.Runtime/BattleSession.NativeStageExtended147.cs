namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState InitializeNativeStage147(NativeBattleGraphicsState graphics)
    {
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidDataException("Native511DB0 requires its initialized UI services.");
        NativeBattleUiWindows windows = ui.Windows ?? throw new InvalidDataException("Native511DB0 requires its retained vitals window bank.");
        return graphics with { UiServices = ui with { Windows = windows.RequestVitalsVisibility4B1830(0, false).RequestVitalsVisibility4B1830(2, false) } };
    }

    private NativeBattleGraphicsState AdvanceNativeStage147(NativeBattleGraphicsState graphics, NativeBattleStageState state, NativeBattleStageTask task)
    {
        NativeBattleStageExtendedState extended = state.Extended ?? throw new InvalidDataException("Native511EF0 requires its retained stage process state.");
        NativeBattleStage147State owner = extended.Stage147;
        if (task.Phase == 0)
        {
            NativeStage = state with { Task = task with { Phase = 1 }, Extended = extended with { Stage147 = owner with { Subphase = 0 } } };
            return graphics;
        }
        if (task.Phase != 1)
        {
            return graphics;
        }
        if ((state.Flags & 2) == 0)
        {
            for (int texture = 0; texture < 2; texture++)
            {
                NativeBattleStageTextureStep motion = NativeBattleStageTextureMotion.Advance50CB20(state.TextureAnimations, checked((ushort)texture));
                state = state with { TextureAnimations = motion.Source };
                if (!motion.Copies.IsEmpty)
                {
                    NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native511EF0 requires its retained texture queue.");
                    NativeBattleEffectTextureState textures = memory.Textures ?? throw new InvalidDataException("Native511EF0 requires its retained texture process.");
                    foreach (NativeBattleTextureCopy copy in motion.Copies)
                    {
                        textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
                    }
                    NativeEffectMemory = memory with { Textures = textures };
                }
            }
        }
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new InvalidDataException("Native511EF0 requires its prior view matrix.");
        state = state.Step500FD0(view);
        graphics = SubmitNativeStagePackets(graphics, state);
        if (task.Callback.Address == 0x511EB0)
        {
            NativeStage = state;
            return graphics;
        }
        switch (owner.Subphase)
        {
            case 0:
                foreach (int index in new[] { 0, 2 })
                {
                    NativeBattleGraphicsModel model = graphics.Models[index];
                    graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Flags0 = (ushort)(model.Flags0 | 4) }) };
                }
                if ((graphics.Models[0].Flags0 & 2) != 0)
                {
                    owner = owner with { Subphase = 1 };
                }
                break;
            case 1:
                graphics = graphics with { Effects = graphics.Effects with { PublishedCount = 1 } };
                if (!graphics.Models.Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40, 0)))
                {
                    owner = owner with { Subphase = 2 };
                }
                break;
            case 2:
                foreach (byte index in new byte[] { 0, 2 })
                {
                    NativeBattleGraphicsModel model = graphics.Models[index];
                    NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("Native511EF0 requires the party actor transform.");
                    model = model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + 6000)) } }, Rotation14 = 2048 };
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(index, model) }, index, 26);
                }
                owner = owner with { Subphase = 3, Counter = 0 };
                break;
            case 3:
                foreach (int index in new[] { 0, 2 })
                {
                    NativeBattleGraphicsModel model = graphics.Models[index];
                    graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Flags0 = (ushort)(model.Flags0 & ~4) }) };
                }
                owner = owner with { Counter = 0, Subphase = 4 };
                break;
            case 4:
                owner = owner with { Counter = unchecked((short)(owner.Counter + 1)) };
                foreach (int index in new[] { 0, 2 })
                {
                    NativeBattleGraphicsModel model = graphics.Models[index];
                    NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("Native511EF0 requires the party actor transform.");
                    graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Transform = transform with { Position28 = transform.Position28 with { Z = NativeBattleStage147.Descent511EF0(transform.Initial20.Z, owner.Counter) } } }) };
                }
                int sound = (owner.Counter & 7) == 3 ? 2 : (owner.Counter & 7) == 7 ? 3 : -1;
                if (sound >= 0)
                {
                    PublishNativeModuleSound(new((uint)sound, 0, null) { RawPan = 0x80 });
                }
                if (owner.Counter >= 20)
                {
                    NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidDataException("Native511EF0 requires its initialized UI services.");
                    NativeBattleUiWindows windows = ui.Windows ?? throw new InvalidDataException("Native511EF0 requires its retained vitals window bank.");
                    graphics = graphics with { UiServices = ui with { Windows = windows.RequestVitalsVisibility4B1830(0, true).RequestVitalsVisibility4B1830(2, true) } };
                    owner = owner with { Subphase = 5, Counter = 0 };
                    foreach (byte index in new byte[] { 0, 2 })
                    {
                        NativeBattleGraphicsModel model = graphics.Models[index];
                        byte sequence = NativeStandingSequence(model.Flags8 ?? throw new InvalidDataException("Native511EF0 requires the party status DWORD."));
                        graphics = SelectNativeHeaderSequence(graphics, index, sequence);
                        model = graphics.Models[index];
                        graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Rotation14 = 0 }) };
                    }
                }
                break;
            case 5:
                if (owner.Counter >= 5)
                {
                    NativeBattleEffectHeapAllocation animation = definition.NativeStageSource?.TextureAnimation147 ?? throw new InvalidDataException("Native511EF0 requires its original stage animation resource.");
                    NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native511EF0 requires its retained effect memory.");
                    NativeEffectMemory = memory with { Textures = (memory.Textures ?? throw new InvalidDataException("Native511EF0 requires its retained texture process.")).Register5099A0(animation) };
                    graphics = SelectNativeHeaderSequence(graphics, 3, 16);
                    owner = owner with { Subphase = 6 };
                }
                else
                {
                    owner = owner with { Counter = unchecked((short)(owner.Counter + 1)) };
                    graphics = graphics with { Effects = graphics.Effects with { PublishedCount = 1 } };
                }
                break;
            case 6:
                NativeBattleCameraState cameraState = NativeCamera ?? throw new InvalidDataException("Native511EF0 requires its retained camera process.");
                if ((cameraState.Mask & 0xFF) == 0)
                {
                    NativeCamera = cameraState with { BlendProgress = 4096 };
                    owner = owner with { Subphase = -1 };
                }
                break;
        }
        NativeStage = state with { Extended = extended with { Stage147 = owner } };
        return graphics;
    }
}
