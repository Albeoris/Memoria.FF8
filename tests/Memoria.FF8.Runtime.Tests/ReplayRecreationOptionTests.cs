using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ReplayRecreationOptionTests
{
    [Fact]
    public void Recreation_defaults_to_semantic_mode()
    {
        Assert.Equal(ReplayRecreationMode.Semantic, LaunchOptions.Parse([]).RecreateMode);
    }

    [Fact]
    public void Strict_flag_selects_strict_recreation_without_consuming_other_options()
    {
        LaunchOptions options = LaunchOptions.Parse(["--recreate-strict", "--validate", "--frames", "17"]);

        Assert.Equal(ReplayRecreationMode.Strict, options.RecreateMode);
        Assert.True(options.ValidateOnly);
        Assert.Equal(17, options.ExitAfterFrames);
    }
}
