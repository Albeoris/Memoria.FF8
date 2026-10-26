using Xunit;

[assembly: AssemblyFixture(typeof(Memoria.FF8.FontInventoryFixture))]

namespace Memoria.FF8;

public sealed class FontInventoryFixture
{
    public FontInventoryFixture()
    {
        SystemFontCatalog.PrepareAsync().GetAwaiter().GetResult();
    }
}
