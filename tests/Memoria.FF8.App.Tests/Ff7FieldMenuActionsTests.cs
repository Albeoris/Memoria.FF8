using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7FieldMenuActionsTests
{
    [Fact]
    public void Resident_field_actions_apply_through_the_menu_and_survive_a_cold_save()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");

        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session opening = Ff7Session.NewGame(installation);
        for (int tick = 0; tick < 5000 && (opening.Progress.CompletedBattles.Length == 0 || !opening.CanUsePlayerMenu); tick++)
        {
            if (opening.Host.Process.Movie is { Playing: true } movie)
            {
                opening.Host.PublishMovieFrame(Math.Min(movie.Frame + tick % 2, 1799), movie.Frame >= 1799);
            }
            if (tick % 30 == 0)
            {
                foreach (Ff7Dialogue dialogue in opening.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
                {
                    opening.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
                }
            }
            if (tick >= 4110 && !opening.Host.Process.PlayerLocked)
            {
                opening.MovePlayer(0, -1, true);
            }
            opening.AdvanceTick();
        }
        Assert.True(opening.CanUsePlayerMenu);
        Assert.Equal(300, Assert.Single(opening.Progress.CompletedBattles).Formation);

        Ff7SessionSave source = opening.Capture();
        Ff7CharacterState cloud = source.Progress.Characters.Single(character => character.Id == 0);
        byte[] native = cloud.NativeRecord.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(48), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(native.AsSpan(64), 0x35);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(34), (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(native.AsSpan(34)) | 1 << 3));
        native[15] = 128;
        Ff7Progress progress = source.Progress with
        {
            Characters = source.Progress.Characters.Select(character => character.Id == 0 ? cloud with { NativeRecord = native, HitPoints = 1, MagicPoints = 20 } : character).ToArray(),
            Inventory = [.. source.Progress.Inventory.Where(item => item.ItemId is not (0 or 129)), new(0, 2), new(129, 1)]
        };
        using GpuTestDevice device = new();
        using SpriteBatch batch = new(device.Device);
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), _ => throw new InvalidOperationException("The post-battle menu fixture cannot request a movie."), audioEnabled: false);
        scene.ConfigureSharedDialogueDefaults(batch, ff8Root!, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        scene.Continue(new() { ActiveGame = SessionGame.Ff7, Ff7 = source with { Progress = progress } });

        Press(Keys.V);
        MenuController menu = Assert.IsType<MenuController>(scene.ActivePlayerMenu);
        int startingMp = Resident().MagicPoints;
        Choose("main", "items");
        Choose("ff7-items", "ff7-item/0");
        Choose("ff7-item-target", "ff7-character/0");
        Assert.Equal(101, Resident().HitPoints);
        Assert.Equal(1, scene.Session!.Progress.Inventory.Single(item => item.ItemId == 0).Quantity);

        Root();
        Choose("main", "magic");
        Choose("ff7-magic", "ff7-character/0");
        Choose("ff7-magic-character", "spell-0");
        Choose("ff7-magic-target", "ff7-character/0");
        Assert.True(Resident().HitPoints > 101);
        Assert.Equal(startingMp - 5, Resident().MagicPoints);

        Root();
        Choose("main", "equip");
        Choose("ff7-equip", "ff7-character/0");
        Choose("ff7-equip-character", "Weapon");
        Choose("ff7-equip-choice", "129");
        Assert.Equal(1, Resident().NativeRecord[28]);
        Assert.Contains(scene.Session!.Progress.Inventory, item => item.ItemId == 128);
        Assert.DoesNotContain(scene.Session.Progress.Inventory, item => item.ItemId == 129);

        Root();
        Choose("main", "limit");
        Choose("ff7-limit", "ff7-character/0");
        Choose("ff7-limit-character", "set");
        Choose("ff7-limit-set", "level-2");
        Choose("ff7-limit-confirm", "no");
        Assert.Equal(128, Resident().NativeRecord[15]);
        Choose("ff7-limit-set", "level-2");
        Choose("ff7-limit-confirm", "yes");
        Assert.Equal(2, Resident().NativeRecord[14]);
        Assert.Equal(0, Resident().NativeRecord[15]);

        CombinedSessionSave saved = scene.Capture();
        Assert.Equal(JsonSerializer.Serialize(source.Vm), JsonSerializer.Serialize(saved.Ff7!.Vm));
        using MemoryStream stream = new();
        CombinedSessionCodec.Write(stream, saved);
        stream.Position = 0;
        scene.Continue(CombinedSessionCodec.Read(stream));
        Assert.Equal(JsonSerializer.Serialize(saved.Ff7.Progress), JsonSerializer.Serialize(scene.Session!.Progress));

        Ff7CharacterState Resident() => scene.Session!.Progress.Characters.Single(character => character.Id == 0);

        void Choose(string screen, string entry)
        {
            menu.SelectEntry(screen, entry);
            Press(Keys.X);
        }

        void Root()
        {
            while (menu.ScreenId != "main")
            {
                Assert.True(menu.IsOpen);
                menu.Back();
            }
        }

        void Press(Keys key)
        {
            GameTime time = new(TimeSpan.Zero, TimeSpan.Zero);
            scene.Update(time, default);
            scene.Update(time, new KeyboardState(key));
            Assert.Null(scene.Error);
        }
    }
}
