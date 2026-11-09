using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePlayerEntryTests
{
    public static void Run()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, TalkRadius = 70, MoveSpeed = 777, BaseAnimationSpeed = 48, AnimationSpeed = 24 };
        Ff7Actor other = new() { Slot = 1, Entity = 1, Model = 1, CollisionRadius = 7, MoveSpeed = 19, BaseAnimationSpeed = 32 };
        Ff7FieldProcess process = new() { Actors = [player, other], Lines = [new(0, 14, 0, -100, 0, 0, 100, 0)], BlinkStateInitialized = true };
        Ff7FieldResources field = Field();
        var host = new Ff7FieldHost(field, (_, _) => 1, process with { Actors = [other with { Slot = 0 }] }) { ReadScriptParty = () => [0, 1, 2] };
        var machine = new FieldMachine(FieldProgram.FromPcScripts(field.Scripts), host: host);
        machine.Initialize();
        Equal(0, host.GetActor(0).CollisionRadius);
        Equal(false, host.GetActor(0).CollisionEnabled);
        Equal(48, host.GetActor(0).BaseAnimationSpeed);
        string vm = JsonSerializer.Serialize(machine.Capture());
        host.UpdateLineContacts(host.GetActor(0), host.GetActor(0), moving: true);
        Equal(false, host.Process.Lines[0].Push);
        host.InitializePlayerMovement(512);
        Ff7Actor initialized = host.GetActor(0);
        Equal(34, initialized.CollisionRadius);
        Equal(1024, initialized.MoveSpeed);
        Equal(16, initialized.BaseAnimationSpeed);
        Equal(48, initialized.AnimationSpeed);
        Equal(80, initialized.TalkRadius);
        Equal(false, initialized.CollisionEnabled);
        Equal(other with { Slot = 0 }, host.GetActor(1));
        Equal(vm, JsonSerializer.Serialize(machine.Capture()));
        host.UpdateLineContacts(initialized, initialized, moving: true);
        Equal(true, host.Process.Lines[0].Push);
        host.UpdateLineContacts(initialized with { X4096 = 34 * 4096 }, initialized, moving: false);
        Equal(false, host.Process.Lines[0].Touch);
        host.UpdateLineContacts(initialized with { X4096 = 33 * 4096 }, initialized, moving: false);
        Equal(true, host.Process.Lines[0].Touch);

        var locked = new Ff7FieldHost(field, (_, _) => 1, process with { PlayerLocked = true });
        string lockedBefore = JsonSerializer.Serialize(locked.Capture());
        locked.InitializePlayerMovement(1024);
        Equal(lockedBefore, JsonSerializer.Serialize(locked.Capture()));
        var scaled = new Ff7FieldHost(field, (_, _) => 1, process);
        scaled.InitializePlayerMovement(1024);
        Equal(68, scaled.GetActor(0).CollisionRadius);
        Equal(2048, scaled.GetActor(0).MoveSpeed);
        var signed = new Ff7FieldHost(field, (_, _) => 1, process);
        signed.InitializePlayerMovement(0x8000);
        Equal(63360, signed.GetActor(0).CollisionRadius);
        Equal(0, signed.GetActor(0).MoveSpeed);

        host.Execute(new FieldHostRequest(long.MaxValue - 800, 0, 1, 0, new FieldActorProperty(FieldActorPropertyKind.CollisionRadius, 9)));
        Ff7FieldProcess saved = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        var restored = new Ff7FieldHost(field, (_, _) => 1, saved);
        Equal(9, restored.GetActor(0).CollisionRadius);
        Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(restored.Capture()));
    }

    private static Ff7FieldResources Field()
    {
        byte[] code = [0xA1, 0, 0xA0, 0, 0xC6, 0, 0, 0xC7, 1, 0xBD, 0, 48, 0, 0];
        byte[] section = new byte[106 + code.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(section, 0x0502);
        section[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(4), checked((ushort)(104 + code.Length)));
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(8), 512);
        for (int index = 0; index < 32; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(40 + index * 2), 104);
        }
        code.CopyTo(section, 104);
        PcFieldModels models = new(0, 512, [new("actor", 0, "actor.hrc", "512", [], 0, ["idle.a"])]);
        return new Ff7FieldResources("entry-fixture", null!, PcFieldScripts.Read(section), models, [], [], null!, null!, null!);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Player entry expected {expected}, got {actual}.");
        }
    }
}
