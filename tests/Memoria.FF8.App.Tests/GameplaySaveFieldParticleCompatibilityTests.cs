namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldParticleCompatibilityTests
{
    [Fact]
    public void Older_document_without_particle_phase_accepts_new_visual_resource_without_advancing_gameplay()
    {
        GameplaySaveFieldParticleControlTests.VerifyParticleRestart(true, true);
    }
}
