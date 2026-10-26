using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218StateTests
{
    [Fact]
    public void Flare_uses_canonical_previous_camera_eye_after_snapshot_without_an_effect_local_copy()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess();
        process = process with { Eye = process.Eye with { Current = new(11, 22, 33) }, PreviousPose = new(new(321, -654, 987), new(-1, -2, -3), 320, 12) };
        NativeBattleGuardianCameraProcess restored = JsonSerializer.Deserialize<NativeBattleGuardianCameraProcess>(JsonSerializer.Serialize(process))!;
        NativeBattleEffect218Frame frame = Frame(NativeBattleEffectMemory.FromColdProcess() with { GuardianCameraProcess = restored });
        Assert.Equal(new NativeBattleVector(321, -654, 987), PreviousCameraEye6338B0(frame));
        Assert.Throws<InvalidDataException>(() => frame.State.ReadView(0x24FD358, 6));
        Assert.DoesNotContain("FlareAnchor", JsonSerializer.Serialize(frame.State));
    }

    [Fact]
    public void Missing_actual_previous_camera_eye_is_unknown_even_when_effect_storage_is_cold()
    {
        NativeBattleEffect218Frame frame = Frame(NativeBattleEffectMemory.FromColdProcess());
        Assert.Throws<NotSupportedException>(() => PreviousCameraEye6338B0(frame));
        frame = frame with { Memory = frame.Memory with { GuardianCameraProcess = NativeBattleGuardianCameraProgram.FromColdProcess() with { PreviousPose = null } } };
        Assert.Throws<NotSupportedException>(() => PreviousCameraEye6338B0(frame));
    }

    private static NativeBattleEffect218Frame Frame(NativeBattleEffectMemory memory) => new(memory, new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor), new(1), NativeBattleEffect218State.FromColdProcess());
}
