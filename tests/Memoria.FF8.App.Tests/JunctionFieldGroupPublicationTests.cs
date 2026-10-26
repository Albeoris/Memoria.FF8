using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionFieldGroupPublicationTests
{
    [Fact]
    public void RejectedGroupCannotSupplyItsTailAndDisjointGroupRemainsUsable()
    {
        using IsolatedFieldGame instance = new();
        string game = instance.GameDirectory;
        string root = Path.Combine(Path.GetTempPath(), "memoria-field-groups-" + Guid.NewGuid().ToString("N"));
        try
        {
            using IResourceSource source = GameDataSourceFactory.Open(game);
            ResourceService original = new(source);
            byte[] program = original.Read(new("field/bghall_6/bghall_6.jsm"));
            byte[] messages = original.Read(new("field/bghall_6/bghall_6.msd"));
            byte[] information = original.Read(new("field/bghall_6/bghall_6.inf"));
            WritePackage("A", ("jsm", program), ("msd", messages));
            WritePackage("B", ("jsm", program), ("inf", new byte[] { 0 }));
            WritePackage("C", ("rat", new byte[4]));
            using GameResourceHost host = new(game, Path.Combine(root, "application"), watch: false, modsDirectory: root);
            Assert.Equal(ModEffectiveStatus.Active, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/A").Status);
            Assert.Equal(ModEffectiveStatus.NoUsableEntries, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/B").Status);
            Assert.Equal(ModEffectiveStatus.Active, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/C").Status);
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.FieldGroupConflict" && diagnostic.PackageId == "Junction/B");
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidFieldResource");
            Assert.Equal(information, host.Resources.Read(new("field/bghall_6/bghall_6.inf")));
            Assert.Equal(new byte[4], host.Resources.Read(new("field/bghall_6/bghall_6.rat")));
            Assert.StartsWith("Junction/A:", host.Resources.Get(new("field/bghall_6/bghall_6.jsm")).Provenance);
            Assert.StartsWith("Junction/C:", host.Resources.Get(new("field/bghall_6/bghall_6.rat")).Provenance);
            host.Mods.RequestRefresh();
            host.Pump(true);
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.FieldGroupConflict" && diagnostic.PackageId == "Junction/B");

            void WritePackage(string package, params (string Extension, byte[] Bytes)[] members)
            {
                string directory = Path.Combine(root, "Junction", package);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "mod.xml"), $"<ModInfo><Name>{package}</Name><ModFolder Folder=\"assets\" /></ModInfo>");
                string field = Path.Combine(directory, "assets", "field", "mapdata", "bg", "bghall_6");
                Directory.CreateDirectory(field);
                foreach ((string extension, byte[] bytes) in members)
                {
                    File.WriteAllBytes(Path.Combine(field, "bghall_6." + extension), bytes);
                }
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
