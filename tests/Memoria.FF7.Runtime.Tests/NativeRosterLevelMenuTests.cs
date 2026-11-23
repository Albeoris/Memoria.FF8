using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRosterLevelMenuTests
{
    internal static void Run()
    {
        Ff7FieldResources field = new("roster-level-menu-fixture", null!, null!, null!, [], [], null!, null!, null!);
        FieldProgram program = new([0], [new ushort[32]]);
        Ff7Progress progress = new()
        {
            InitialKernelData = [1],
            Characters = [Character(8, 99, 105), Character(4, 54, 54), Character(3, 63, 63), Character(2, 42, 42), Character(0, 10, 10)],
            Party = [0, 4, 8],
            Inventory = [],
            Materia = [],
            AvailableCharacters = (1 << 0) | (1 << 3) | (1 << 4) | (1 << 8)
        };
        Ff7FieldHost host = new(field, (_, _) => 1, new() { BlinkStateInitialized = true });
        FieldMachine machine = new(program, host: host);
        FieldVariables variables = machine.Variables;
        for (byte row = 0; row < 8; row++)
        {
            variables.WriteByte(FieldBank.Persistent1Byte, (byte)(0xC4 + row), (byte)(70 + row));
        }
        variables.WriteByte(FieldBank.Persistent1Byte, 9, 8);
        variables.WriteByte(FieldBank.Persistent1Byte, 10, 2);
        variables.WriteByte(FieldBank.Persistent1Byte, 11, 0);
        byte[] before = variables.CopyPersistent();
        byte[] expected = before.ToArray();
        expected[0x10A] = 4;
        expected[0x1C4] = 10;
        expected[0x1C7] = 54;
        expected[0x1CB] = 99;

        FieldHostRequest capture = new(7, 0, 1, 4, new FieldMenuCommand(24, 127));
        Check(!host.Execute(capture).Completed && variables.CopyPersistent().SequenceEqual(before), "MENU24 must yield for its native menu service before publishing levels.");
        Ff7FieldProcess coldProcess = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Process))!;
        Ff7FieldHost coldHost = new(field, (_, _) => 1, coldProcess);
        FieldMachine coldMachine = new(program, new FieldVariables(before, variables.CopyMap()), host: coldHost);

        foreach ((Ff7FieldHost currentHost, FieldMachine currentMachine) in new[] { (host, machine), (coldHost, coldMachine) })
        {
            int applications = 0;
            currentHost.ExternalCommand = command =>
            {
                applications++;
                Check(command == capture.Command, "The native menu changed the admitted level capture request.");
                Ff7Session.CaptureNativeRosterLevels(progress, currentMachine);
                return new(true);
            };
            currentHost.AdvanceNativeMenu();
            currentHost.AdvanceNativeMenu();
            Check(applications == 1 && currentHost.Process.Menu is { Completed: true } && currentHost.Process.GlobalRequestId == capture.Id && currentMachine.Variables.CopyPersistent().SequenceEqual(expected), "MENU24 must publish only available roster levels in original row order and synchronize the script party once.");
            Check(currentHost.Execute(capture).Completed && currentHost.Process.GlobalCommand == Ff7GlobalCommand.None, "The original MENU24 poll did not release its owner.");

            currentMachine.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 2);
            byte[] beforeAcknowledge = currentMachine.Variables.CopyPersistent();
            FieldHostRequest acknowledge = new(8, 0, 1, 8, new FieldMenuCommand(25, 255));
            Check(!currentHost.Execute(acknowledge).Completed, "MENU25 must yield for its automatic acknowledgment.");
            currentHost.AdvanceNativeMenu();
            Check(currentHost.Execute(acknowledge).Completed && applications == 1 && currentMachine.Variables.CopyPersistent().SequenceEqual(beforeAcknowledge), "MENU25 must acknowledge its owner without capturing levels or synchronizing the party.");

            FieldHostRequest ending = new(9, 0, 1, 12, new FieldMenuCommand(5, 0));
            Check(!currentHost.Execute(ending).Completed, "MENU5 must admit its pending presentation owner.");
            currentHost.AdvanceNativeMenu();
            Check(!currentHost.Execute(ending).Completed && currentHost.Process.GlobalRequestId == ending.Id, "MENU5 must remain pending until its presentation owner acknowledges it.");
            currentHost.CompleteMenu(ending.Id);
            Check(currentHost.Execute(ending).Completed && currentHost.Process.GlobalCommand == Ff7GlobalCommand.None, "The original MENU5 poll did not release its acknowledged owner.");
        }
    }

    private static Ff7CharacterState Character(int identity, byte nativeLevel, int level)
    {
        byte[] record = new byte[132];
        record[1] = nativeLevel;
        return new(identity, $"Character {identity}", level, 0, 1, 1, record);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
