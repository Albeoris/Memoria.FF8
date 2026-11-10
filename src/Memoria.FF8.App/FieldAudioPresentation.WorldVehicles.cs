using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal readonly record struct WorldVehicleSoundFrame(int EntityType, int GraphicsSpeed, int Height, byte? GroundType)
{
    public static WorldVehicleSoundFrame FromWorld(WorldState world, int graphicsSpeed)
    {
        int entity = world.NativeFootRuntime?.EntityType ?? world.NativeCarRuntime?.EntityType ?? (world.NativeRagnarokRuntime is not null ? 50 : world.NativeGardenRuntime is not null ? 48 : -1);
        return new(entity, graphicsSpeed, world.NativeHeight, world.GroundType);
    }
}

internal sealed partial class FieldAudioPresentation
{
    internal void PresentWorldVehicleSounds(WorldVehicleSoundFrame? frame)
    {
        TryOutput(() =>
        {
            // Native542620 updates the dedicated world slots after movement and late transfers.
            UpdateWorldVehicleSound(6, 500002, frame is { EntityType: 50 } ragnarok ? Math.Clamp((ragnarok.GraphicsSpeed >> 2) + 80, 0, 127) : null);
            UpdateWorldVehicleSound(8, 500004, frame is { EntityType: 48 } garden ? Math.Clamp((garden.GraphicsSpeed >> 1) + 60, 0, 127) : null);
            UpdateWorldVehicleSound(9, 500005, frame is { EntityType: 48, GroundType: 33 or 34 } wake ? Math.Clamp((wake.Height >> 2) + 64, 0, 127) : null);
        });
    }

    private void UpdateWorldVehicleSound(int slot, uint effect, int? volume)
    {
        int voice = -100 - slot;
        if (volume is null)
        {
            StopVoice(voice);
            return;
        }
        if (!voices.TryGetValue(voice, out Voice? active))
        {
            PlayPresentationSound(effect, voice, 64, (uint)volume.Value);
            return;
        }
        active.Volume = volume.Value;
        active.VolumeRamp = null;
        int gain = OutputGain(Catalog.SoundVolume(volume.Value), soundPreference);
        active.Primary.Volume(gain);
        active.Loop?.Volume(gain);
    }
}
