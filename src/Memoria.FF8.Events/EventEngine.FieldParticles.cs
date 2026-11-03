using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<int, byte[]?>? ReadNativeFieldParticleDefinition { get; set; }
    public byte[]? NativeFieldParticleRandomTable { get; set; }
    public IReadOnlyList<NativeFieldParticleSprite> FieldParticleSprites => Scene.NativeFieldParticleSprites ?? [];

    private void InitializeNativeFieldParticles(SceneEventState previous, bool coldProcess)
    {
        Scene.NativeFieldParticleRandomPhase = coldProcess ? (byte)0 : previous.NativeFieldParticles?.RandomPhase ?? previous.NativeFieldParticleRandomPhase;
        byte[]? definition = ReadNativeFieldParticleDefinition?.Invoke(FieldId);
        if (definition is not null && Scene.NativeFieldParticleRandomPhase is byte phase)
        {
            Scene.NativeFieldParticles = NativeFieldParticlesState.Create(definition) with { RandomPhase = phase };
            Scene.NativeFieldParticleSprites = [];
        }
    }

    private void ValidateNativeFieldParticleCallback()
    {
        if (ReadNativeFieldParticleDefinition?.Invoke(FieldId) is null)
        {
            return;
        }
        if (Scene.NativeFieldParticles is not { } particles || NativeFieldParticleRandomTable is not { Length: 256 })
        {
            throw new NotSupportedException("Field particle presentation requires its current PMD pool and random phase; explicitly migrate this standalone save before continuing.");
        }
        particles.Validate();
    }

    private NativeParticlePosition NativeParticleActorPosition(int instance)
    {
        if ((uint)instance >= Scene.NativeActorObjectIds.Count)
        {
            throw new NotSupportedException("The field particle attachment names an absent physical actor instance.");
        }
        FieldFixedPosition position = ReadNativeScriptPosition(actors[Scene.NativeActorObjectIds[instance]]) ?? throw new NotSupportedException("The field particle attachment requires current native actor coordinates.");
        return new(unchecked((short)(position.X / 4096)), unchecked((short)(position.Y / 4096)), unchecked((short)(position.Z / 4096)));
    }

    private void WarmUpNativeFieldParticles()
    {
        if (Scene.NativeFieldParticles is { } particles)
        {
            Scene.NativeFieldParticles = particles.WarmUp(NativeFieldParticleRandomTable ?? throw new NotSupportedException("Field particles require the original random table."), NativeParticleActorPosition);
            Scene.NativeFieldParticleRandomPhase = Scene.NativeFieldParticles.RandomPhase;
        }
    }

    private void StepNativeFieldParticles()
    {
        if (services.IsMoviePlaying || Scene.NativeFieldParticles is not { } particles)
        {
            return;
        }
        NativeFieldParticlesFrame frame = particles.Step(NativeFieldParticleRandomTable ?? throw new NotSupportedException("Field particles require the original random table."), NativeParticleActorPosition);
        Scene.NativeFieldParticles = frame.State;
        Scene.NativeFieldParticleSprites = frame.Sprites.ToArray();
        Scene.NativeFieldParticleRandomPhase = frame.State.RandomPhase;
    }

    /// <summary>Explicit transient presentation restart; preserves story and command history, and supplies a declared random phase.</summary>
    public void RestartNativeFieldParticlesForMigration(byte randomPhase)
    {
        byte[] definition = ReadNativeFieldParticleDefinition?.Invoke(FieldId) ?? throw new NotSupportedException("Particle migration requires the actual current field PMD resource.");
        NativeFieldParticlesState restarted = NativeFieldParticlesState.Create(definition) with { RandomPhase = randomPhase };
        foreach ((int slot, byte value) in Scene.NativeParticleSlots)
        {
            restarted = restarted.WithEmitter(slot, value);
        }
        restarted = restarted.WarmUp(NativeFieldParticleRandomTable ?? throw new NotSupportedException("Particle migration requires the original random table."), NativeParticleActorPosition);
        Scene.NativeFieldParticles = restarted;
        Scene.NativeFieldParticleRandomPhase = restarted.RandomPhase;
        Scene.NativeFieldParticleSprites = [];
    }
}
