using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private Dictionary<int, byte> CaptureGameplaySaveParticleControls()
    {
        Dictionary<int, byte> controls = new(Scene.NativeParticleSlots);
        ValidateGameplaySaveParticleControls(controls);
        if (Scene.NativeFieldParticles is { } particles)
        {
            particles.Validate();
            if (Scene.NativeFieldParticleRandomPhase != particles.RandomPhase || Enumerable.Range(0, 16).Any(slot => particles.Emitters[slot] != controls.GetValueOrDefault(slot)))
            {
                throw new InvalidDataException("Gameplay particle controls disagree with their current physical emitter bytes or local random phase.");
            }
        }
        return controls;
    }

    private void ValidateGameplaySaveParticleControls(IReadOnlyDictionary<int, byte> controls)
    {
        if (controls.Any(pair => pair.Key is < 0 or >= 16 || pair.Value is > 1 and < 0x80 || (pair.Value & 0x80) != 0 && (pair.Value & 0x7F) >= Scene.NativeActorObjectIds.Count))
        {
            throw new InvalidDataException("Gameplay particle control has an invalid slot, emitter byte or retained actor ordinal.");
        }
    }

    private void RestoreGameplaySaveParticles(GameplaySaveFieldScene saved)
    {
        IReadOnlyDictionary<int, byte> controls = saved.ParticleControls ?? [];
        ValidateGameplaySaveParticleControls(controls);
        byte[]? definition = ReadNativeFieldParticleDefinition?.Invoke(FieldId);
        NativeFieldParticlesState? particles = null;
        if (definition is not null)
        {
            byte phase = saved.ParticleRandomPhase ?? throw new InvalidDataException("Gameplay particle reconstruction requires its saved local random phase.");
            particles = NativeFieldParticlesState.Create(definition) with { RandomPhase = phase };
            foreach ((int slot, byte value) in controls)
            {
                particles = particles.WithEmitter(slot, value);
            }
        }
        // Restart presentation from current resources without a warmup callback or random draw.
        Scene.NativeFieldParticles = particles;
        Scene.NativeFieldParticleRandomPhase = saved.ParticleRandomPhase;
        Scene.NativeFieldParticleSprites = [];
        Scene.NativeParticleSlots.Clear();
        foreach ((int slot, byte value) in controls)
        {
            Scene.NativeParticleSlots.Add(slot, value);
        }
        Scene.NativeParticleSlotOrigin = NativeParticleControlOrigin.GameplayContinuation;
    }
}
