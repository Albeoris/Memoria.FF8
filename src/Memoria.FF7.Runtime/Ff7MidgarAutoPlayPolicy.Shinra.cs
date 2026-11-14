using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7MidgarAutoPlayPolicy
{
    private bool TryChooseShinraEntrance(Ff7AutoplayDriver driver, string field, int story, out Ff7AutoplayInput input)
    {
        input = default;
        if (field is not ("wcrimb_2" or "md0" or "sinbil_1" or "sinbil_2" or "blinst_1" or "blinst_2" or "blinst_3" or "blin59" or "blinele" or "blin1" or "blin2" or "blin2_i" or "eleout" or "blin60_1" or "blin60_2" or "blin61" or "blin62_1" or "blin63_1" or "blin64" or "blin65_1" or "blin65_2" or "blin66_1" or "blin66_2" or "blin66_3" or "blin66_4" or "blin66_5" or "blin67_1" or "blin67_3" or "blin67_4" or "blin68_1" or "blin68_2" or "blin67_2" or "blin69_1" or "blin70_1" or "blin70_2"))
        {
            return false;
        }
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.Tick == 0 || process.FadeState is { EntryCurve: true, Completed: false })
        {
            return true;
        }
        if (player.Ladder is { } ladder)
        {
            input = ladder.Completed ? default : driver.Steer(new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12));
            return true;
        }
        if (process.PlayerLocked || player.Jump is not null || session.Host.HasScriptedMotion(player))
        {
            return true;
        }
        input = field switch
        {
            "wcrimb_2" => player.Triangle switch
            {
                <= 22 => driver.Gateway(225),
                23 or 24 or >= 25 and <= 27 or >= 29 and <= 47 or >= 51 and <= 60 or >= 101 and <= 103 => ApproachLine(driver, 10, 24),
                >= 63 and <= 74 or >= 97 and <= 100 => ApproachLine(driver, 7, 63),
                _ => ApproachLine(driver, 5, 95)
            },
            "md0" => driver.Gateway(227),
            "sinbil_1" => driver.Gateway(228),
            "sinbil_2" => driver.Gateway(229),
            "blinst_1" => ApproachLine(driver, 6, 103),
            "blinst_2" => ApproachLine(driver, 10),
            "blinst_3" => ApproachLine(driver, 9),
            "blin59" => (ReadFlags(0x43) & 64) == 0 ? ApproachLine(driver, 24) : driver.TalkToLine(NeedsShinraSupplies ? 14 : 16),
            "blinele" => driver.TalkTo(16),
            "blin1" => NeedsShinraSupplies ? driver.Gateway(235, 91) : ApproachLineWithConfirm(driver, 17),
            "blin2" => NeedsShinraSupplies ? driver.Gateway(236) : driver.Gateway(234, 147),
            "blin2_i" => NeedsShinraSupplies ? ApproachLineWithConfirm(driver, 15) : driver.Gateway(235),
            "eleout" => ChooseShinraElevatorInput(driver),
            "blin60_1" => driver.Gateway(241),
            "blin60_2" => ApproachLine(driver, 11),
            "blin61" => ReadFlags(0xE0) < 62 ? driver.TalkTo(12) : driver.Gateway(242),
            "blin62_1" => ReadFlags(0xE0) < 65 ? driver.TalkTo(26) : driver.Gateway(245),
            "blin63_1" => driver.Gateway(247),
            "blin64" => driver.Gateway(248),
            "blin65_1" => ChooseMidgarModelInput(driver, inModelRoom: false),
            "blin65_2" => ChooseMidgarModelInput(driver, inModelRoom: true),
            "blin66_1" => story switch
            {
                < 269 => driver.Gateway(252, run: false),
                269 => ApproachLine(driver, 22),
                270 => ApproachLine(driver, 23),
                _ => ApproachLine(driver, 24)
            },
            "blin66_3" => story < 269 ? ApproachLineWithConfirm(driver, 2) : driver.Gateway(250),
            "blin66_2" or "blin66_4" or "blin66_5" => default,
            "blin67_1" => story switch
            {
                271 or 272 => ApproachLine(driver, 12),
                273 => ApproachLine(driver, 11),
                _ => ApproachLine(driver, 18)
            },
            "blin67_3" => story < 278 ? ApproachLine(driver, 7) : driver.Gateway(256, 43, run: false),
            "blin68_1" => story < 284 ? ApproachLine(driver, 14) : ReadFlags(0xE0) < 68 ? driver.TalkTo(7) : driver.Gateway(264),
            "blin68_2" => story < 284 ? default : driver.Gateway(262, 101),
            "blin67_2" => default,
            "blin69_1" => driver.Gateway(266, 13),
            "blin70_1" or "blin70_2" => default,
            _ => default
        };
        return true;
    }

    private bool NeedsShinraSupplies => Ff7NativeInventory.Quantity(session.Progress.Inventory, 0) < 4 && Ff7NativeInventory.Quantity(session.Progress.Inventory, 7) < 2;

    private int? ChooseDominoPassword() => (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xB6) & 15) switch
    {
        1 => 1,
        8 => 2,
        2 => 4,
        4 => 5,
        _ => null
    };

    private Ff7AutoplayInput ChooseMidgarModelInput(Ff7AutoplayDriver driver, bool inModelRoom)
    {
        byte parts = ReadFlags(0x44);
        int placed = session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xB4) >> 3;
        if (inModelRoom)
        {
            int triangle = (parts >> 3) switch
            {
                8 => 70,
                1 => 26,
                4 => 33,
                2 => 68,
                16 => 75,
                _ => -1
            };
            if (triangle < 0)
            {
                return driver.Gateway(248, run: false);
            }
            var section = session.Field.Walkmesh[triangle];
            return driver.Approach((section.A + section.B + section.C) / 3, triangle, run: false) with { Held = session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle == triangle && session.Host.Process.Tick % 16 == 0 ? (ushort)32 : (ushort)0 };
        }
        if (ReadFlags(0xE0) >= 66)
        {
            return driver.Gateway(250, run: false);
        }
        if ((parts >> 3) != 0)
        {
            return driver.Gateway(249, run: false);
        }
        int chest = placed switch
        {
            0 => 18,
            8 => 16,
            10 => 19,
            11 => 15,
            27 => 17,
            31 => 20,
            _ => throw new InvalidDataException($"No native Shinra Midgar-model chest is available for placed mask {placed}.")
        };
        return driver.TalkTo(chest, run: false);
    }

    private Ff7AutoplayInput ChooseShinraElevatorInput(Ff7AutoplayDriver driver)
    {
        if (session.Machine.Variables.ReadByte(FieldBank.MapByte, 9) == 0)
        {
            return ApproachLineWithConfirm(driver, 7);
        }
        int actor = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0B) == 0 ? 2 : 3;
        return driver.TalkTo(actor, run: false);
    }
}
