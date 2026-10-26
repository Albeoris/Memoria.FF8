using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionFieldGroupPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidSelectedFieldIsSkippedAcrossOwnersWithoutRetryingShadowedGroup(bool unsupportedOpcode)
    {
        using IsolatedFieldGame instance = new();
        string root = Path.Combine(Path.GetTempPath(), "memoria-field-rejection-" + Guid.NewGuid().ToString("N"));
        try
        {
            using IResourceSource source = GameDataSourceFactory.Open(instance.GameDirectory);
            ResourceService original = new(source);
            const string rejected = "bghall_6";
            const string retained = "bghall_1";
            byte[] program = original.Read(Key(rejected, "jsm"));
            byte[] messages = original.Read(Key(rejected, "msd"));
            byte[] information = original.Read(Key(rejected, "inf"));
            byte[] rat = original.Read(Key(rejected, "rat"));
            byte[] retainedInformation = original.Read(Key(retained, "inf"));
            WritePackage("A", (rejected, "jsm", InvalidProgram()), (rejected, "msd", messages), (retained, "inf", retainedInformation));
            WritePackage("B", (rejected, "rat", rat));
            WritePackage("C", (rejected, "jsm", program), (rejected, "inf", information));

            using GameResourceHost host = new(instance.GameDirectory, Path.Combine(root, "application"), watch: false, modsDirectory: root);
            Assert.Equal(ModEffectiveStatus.Active, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/A").Status);
            Assert.Equal(ModEffectiveStatus.NoUsableEntries, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/B").Status);
            Assert.Equal(ModEffectiveStatus.NoUsableEntries, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/C").Status);
            foreach (string owner in new[] { "Junction/A", "Junction/B" })
            {
                Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidFieldResource" && diagnostic.PackageId == owner && diagnostic.Message.Contains("group was skipped", StringComparison.Ordinal));
            }
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.FieldGroupConflict" && diagnostic.PackageId == "Junction/C");
            foreach ((string extension, byte[] expected) in new[] { ("jsm", program), ("msd", messages), ("inf", information), ("rat", rat) })
            {
                Assert.Equal(expected, host.Resources.Read(Key(rejected, extension)));
                Assert.DoesNotContain("Junction/", host.Resources.Get(Key(rejected, extension)).Provenance, StringComparison.Ordinal);
            }
            Assert.StartsWith("Junction/A:", host.Resources.Get(Key(retained, "inf")).Provenance);

            byte[] InvalidProgram()
            {
                (Jsm.Opcode Opcode, int Parameter)[] operations = unsupportedOpcode
                    ? [(Jsm.Opcode.LBL, 0), ((Jsm.Opcode)ushort.MaxValue, 0), (Jsm.Opcode.RET, 0)]
                    : [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0x7FFFFF), (Jsm.Opcode.MES, 0), (Jsm.Opcode.RET, 0)];
                byte[] bytes = new byte[14 + operations.Length * 4];
                bytes[3] = 1;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 14);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
                for (int index = 0; index < operations.Length; index++)
                {
                    (Jsm.Opcode opcode, int parameter) = operations[index];
                    int encoded = opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14 + index * 4), encoded);
                }
                Assert.False(JsmResourceValidation.Validate(bytes, messages.Length).IsValid);
                return bytes;
            }

            void WritePackage(string package, params (string Field, string Extension, byte[] Bytes)[] members)
            {
                string directory = Path.Combine(root, "Junction", package);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "mod.xml"), $"<ModInfo><Name>{package}</Name><ModFolder Folder=\"assets\" /></ModInfo>");
                foreach ((string field, string extension, byte[] bytes) in members)
                {
                    string target = Path.Combine(directory, "assets", "field", "mapdata", field[..2], field);
                    Directory.CreateDirectory(target);
                    File.WriteAllBytes(Path.Combine(target, field + "." + extension), bytes);
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

        static ResourceKey Key(string field, string extension) => new($"field/{field}/{field}.{extension}");
    }

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
