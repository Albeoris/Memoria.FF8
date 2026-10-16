using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeDrawPointParticleTables? DrawPointParticleTables { get; set; }
    public Func<int, int, int, int, byte?>? ResolveNativeDrawPointDirection { get; set; }
    public bool NativeDrawPointAcquisitionActive => Scene.NativeDrawPointParticles?.AcquisitionActive ?? throw new NotSupportedException("Native draw-point acquisition requires its current particle owner.");
    public NativeDrawPointParticles? DrawPointParticleFrame => Scene.NativeDrawPointParticles is NativeDrawPointParticles current && NativeModelDrawEnabled(Scene, services.MovieSource, Frame, true) ? current : null;

    private byte DrawPointParticlePhase() => NativeDrawPointParticles.Phase(services.ExamTimer ?? throw new NotSupportedException("Native draw-point phase requires the process clock owner."));

    private void ValidateNativeDrawPointCallback()
    {
        if (DrawPointParticleTables is null)
        {
            return;
        }
        if (Scene.NativeDrawPointParticles is NativeDrawPointParticles particles)
        {
            particles.Validate();
            _ = DrawPointParticlePhase();
        }
        else if (Scene.DrawPointEnabled != 0)
        {
            throw new NotSupportedException("Active draw-point display requires explicit migration of its absent current particle owner before the field callback.");
        }
    }

    private void InitializeNativeDrawPointParticles(SceneEventState previousScene, bool coldProcess)
    {
        if (DrawPointParticleTables is not null && (previousScene.NativeDrawPointParticles is not null || coldProcess))
        {
            Scene.NativeDrawPointParticles = NativeDrawPointParticles.Initialize(previousScene.NativeDrawPointParticles, DrawPointParticlePhase(), DrawPointParticleTables);
        }
    }

    /// <summary>Explicit migration only: restart transient particle presentation at its current setup anchor; never infer missing historical lifetimes.</summary>
    public void RestartNativeDrawPointPresentationForMigration()
    {
        if (Scene.DrawPointInteraction is not null)
        {
            throw new NotSupportedException("Particle presentation cannot restart during an active DRAWPOINT interaction.");
        }
        NativeDrawPointParticles restarted = NativeDrawPointParticles.Initialize(null, DrawPointParticlePhase(), DrawPointParticleTables ?? throw new NotSupportedException("Particle presentation restart requires the original tables."));
        if (Scene.DrawPointEnabled != 0)
        {
            NativeDrawPointReset reset = Scene.DrawPointReset ?? throw new NotSupportedException("Active particle restart requires the executed current-field SETDRAWPOINT receipt.");
            if (Scene.DrawPointEnabled != 1 || reset.FieldId != FieldId)
            {
                throw new NotSupportedException("Active particle restart requires current-field native draw-point setup.");
            }
            // Explicit visual restart: establish the known setup anchor before4745D0
            // initializes curves/history from it, then execute474750's active reset.
            restarted = restarted.ResetAnchors(reset.RawPosition.X >> 12, reset.RawPosition.Y >> 12, reset.RawPosition.Z >> 12);
            restarted = NativeDrawPointParticles.Initialize(restarted, DrawPointParticlePhase(), DrawPointParticleTables!).ResetAnchors(reset.RawPosition.X >> 12, reset.RawPosition.Y >> 12, reset.RawPosition.Z >> 12);
        }
        Scene.NativeDrawPointParticles = restarted;
        SampleNativeDrawPointParticlePalette();
    }

    private void SampleNativeDrawPointParticlePalette()
    {
        if (Scene.NativeDrawPointParticles is not NativeDrawPointParticles current)
        {
            return;
        }
        if (Scene.DrawPointIndex is byte index && services.EventRandom?.DrawPoints is NativeDrawPointMemory bank)
        {
            Scene.NativeDrawPointParticles = current with { Palette = (byte)((bank.Depletion[index / 4] >> (2 * (index & 3))) & 3) };
        }
        else if (Scene.DrawPointEnabled != 0)
        {
            throw new NotSupportedException("Active draw-point palette requires its current global index and packed depletion bank.");
        }
    }

    private void StepNativeDrawPointParticles()
    {
        if (Scene.NativeDrawPointParticles is NativeDrawPointParticles current)
        {
            Scene.NativeDrawPointParticles = current.Step(DrawPointParticlePhase(), DrawPointParticleTables ?? throw new NotSupportedException("Active native particles require their original lookup tables."));
        }
        else if (Scene.DrawPointEnabled != 0)
        {
            throw new NotSupportedException("Active draw-point display requires explicit migration of its absent current particle owner.");
        }
    }

    public void StartNativeDrawPointAcquisition(ActorState receiver)
    {
        Scene.NativeDrawPointParticles = PrepareNativeDrawPointAcquisition(receiver);
    }

    private NativeDrawPointParticles PrepareNativeDrawPointAcquisition(ActorState receiver)
    {
        NativeDrawPointParticles current = Scene.NativeDrawPointParticles ?? throw new NotSupportedException("Native draw-point acquisition requires its current eight spark records.");
        FieldFixedPosition raw = ReadNativeScriptPosition(receiver) ?? throw new NotSupportedException("Native draw-point acquisition requires the receiver's current raw body position.");
        NativeSparkPoint origin = current.Sparks[0].Anchor;
        byte direction = ResolveNativeDrawPointDirection?.Invoke(origin.X << 12, origin.Y << 12, raw.X, raw.Y) ?? throw new NotSupportedException("Native draw-point acquisition requires original477380 direction selection.");
        return current.Launch(raw.X >> 12, raw.Y >> 12, raw.Z >> 12, direction, DrawPointParticlePhase(), DrawPointParticleTables ?? throw new NotSupportedException("Native draw-point acquisition requires its original particle tables."));
    }
}
