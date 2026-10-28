namespace Memoria.FF8.App.Tests;

public sealed class ModDirectoryResourceLeaseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mod-resource-lease-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriterDowngradesAndEveryReaderPreventsStartupReplacement()
    {
        string mods = Path.Combine(root, "Mods");
        using (ModDirectoryResourceLease first = ModDirectoryResourceLease.Acquire(mods, true))
        {
            Assert.True(first.IsWriter);
            first.FinishStartup();
            Assert.False(first.IsWriter);
            using (ModDirectoryResourceLease second = ModDirectoryResourceLease.Acquire(mods, false))
            using (ModDirectoryResourceLease third = ModDirectoryResourceLease.Acquire(mods, true))
            {
                Assert.False(second.IsWriter);
                Assert.False(third.IsWriter);
                first.Dispose();
                using ModDirectoryResourceLease fourth = ModDirectoryResourceLease.Acquire(mods, true);
                Assert.False(fourth.IsWriter);
                Assert.Throws<IOException>(() =>
                {
                    using FileStream exclusive = new(Path.Combine(ModDirectoryResourceLease.StateDirectory(mods), "runtime.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                });
            }
        }
        using ModDirectoryResourceLease nextStartup = ModDirectoryResourceLease.Acquire(mods, true);
        Assert.True(nextStartup.IsWriter);
    }

    [Fact]
    public void ExplicitReaderAlsoPinsDirectoryWithoutAnExistingWriter()
    {
        string mods = Path.Combine(root, "Mods");
        using ModDirectoryResourceLease reader = ModDirectoryResourceLease.Acquire(mods, false);
        Assert.False(reader.IsWriter);
        reader.FinishStartup();
        using ModDirectoryResourceLease other = ModDirectoryResourceLease.Acquire(mods, true);
        Assert.False(other.IsWriter);
    }

    [Fact]
    public void StateIdentityNormalizesTrailingDirectorySeparator()
    {
        string mods = Path.Combine(root, "Mods");
        Assert.Equal(ModDirectoryResourceLease.StateDirectory(mods), ModDirectoryResourceLease.StateDirectory(mods + Path.DirectorySeparatorChar));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(ModDirectoryResourceLease.StateDirectory(mods).ToUpperInvariant(), ModDirectoryResourceLease.StateDirectory(mods.ToUpperInvariant()).ToUpperInvariant());
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
