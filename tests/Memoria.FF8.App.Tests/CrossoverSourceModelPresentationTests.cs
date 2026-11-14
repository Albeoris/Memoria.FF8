using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSourceModelPresentationTests
{
    private static readonly NativeBattleSummonColors Colors = new(0x02000000, 0x808080, 1f / 4096, 4503601774854144d);

    [Theory]
    [InlineData((byte)0, null, (byte)0)]
    [InlineData((byte)1, null, null)]
    [InlineData((byte)2, null, null)]
    [InlineData((byte)5, null, null)]
    [InlineData((byte)0, (byte)93, (byte)93)]
    [InlineData((byte)1, (byte)93, (byte)93)]
    public void Completed_visible_source_derives_only_missing_neutral_alpha_and_retains_active_or_known_values(byte kind, byte? alpha, byte? expected)
    {
        NativeBattleGraphicsModel source = new(0, 3)
        {
            Kind5 = kind,
            Alpha7 = alpha,
            Color40 = 0x11223344,
            Color44 = 0x55667788,
            Sequence = 4,
            SequenceFrame = 23,
            SequenceState = new() { Pose = 2, PoseFrame114 = 7, PoseLimit115 = 9 }
        };
        NativeBattleGraphicsModel presented = CrossoverGunbladeHostAttack.PresentModel(source, Colors);
        Assert.Equal(expected, presented.Alpha7);
        Assert.Equal(source with { Alpha7 = expected }, presented);
        if (kind == 0 && alpha is null)
        {
            NativeBattleGraphicsModel completed = NativeBattleGraphicsModels.AdvanceAppearance(source with { Kind5 = 1, Phase6 = 14 }, false, Colors);
            Assert.Equal(completed.Alpha7, presented.Alpha7);
        }
    }

    [Fact]
    public void Older_source_model_without_alpha_restores_visible_presentation_without_replaying_pose_progress()
    {
        NativeBattleGraphicsModel saved = JsonSerializer.Deserialize<NativeBattleGraphicsModel>("""{"Index":0,"Flags0":3,"Kind5":0,"Color40":287454020,"Color44":1432778632,"Sequence":4,"SequenceFrame":23,"SequenceState":{"Pose":2,"PoseFrame114":7,"PoseLimit115":9}}""")!;
        Assert.Null(saved.Alpha7);
        NativeBattleGraphicsModel restored = CrossoverGunbladeHostAttack.PresentModel(saved, Colors);
        Assert.Equal((byte)0, restored.Alpha7);
        Assert.Equal(saved with { Alpha7 = 0 }, restored);
        Assert.Same(saved.SequenceState, restored.SequenceState);
    }
}
