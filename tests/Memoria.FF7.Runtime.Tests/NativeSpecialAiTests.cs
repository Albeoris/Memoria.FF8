using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeSpecialAiTests
{
    public static void RunContracts()
    {
        byte[] kernel = new byte[3988];
        Ff7BattleRng random = new(kernel, new(new byte[8]));
        SceneBattleData formation = new(1, [], [], [], null);
        Ff7BattleAiActor[] actors = [new(0, 0, 8, 10, 20, 10, 100), new(4, 0x200000, 8, 30, 40, 300, 900, Variant: 3, EnemyId: 259, ElementHalve: 7, PreviousStatuses: 0x40), new(5, 0, 8, 0, 80, 1, 1000, EnemyId: 260)];
        Ff7BattleAiContext context = new(4, actors, 0x31, 0, GlobalMemory: new byte[64], SaveBank1: new byte[256]);
        byte[] inherited = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        Ff7BattleAiResult absent = Ff7BattleAi.Run([], context with { GlobalMemory = inherited }, [], formation, random);
        Check(absent.GlobalMemory.SequenceEqual(inherited) && absent.Commands.Length == 0, "An absent callback must preserve the preceding native script context for formation AI.");
        byte[] selectors =
        [
            0x11, 0, 0, 0x61, 0xFF, 0xFF, 0x83, 0x90,
            0x11, 8, 0, 0x02, 0x60, 0x20, 0x01, 0x60, 0x40, 0x80, 0x83, 0x90,
            0x12, 0x10, 0, 0x60, 5, 0x87, 0x90,
            0x12, 0x20, 0, 0x02, 0x60, 0x20, 0x02, 0x20, 0x41, 0x80, 0x83, 0x90,
            0x73
        ];
        Ff7BattleAiResult selected = Ff7BattleAi.Run(selectors, context, [], formation, random);
        Check(selected.LocalMemory[0] == 10 && selected.LocalMemory[1] == 3 && BinaryPrimitives.ReadUInt16LittleEndian(selected.LocalMemory.AsSpan(2)) == 32 && BinaryPrimitives.ReadUInt16LittleEndian(selected.LocalMemory.AsSpan(4)) == 259, "Count, set collapse, index mask or native enemy-ID selector changed.");

        byte[] lifeCycle =
        [
            0x12, 0, 0, 0x60, 0x20, 0x90,
            0x12, 0, 0, 0x13, 0x60, 0x41, 0x80, 0x02, 0x60, 0x20, 0x03, 0x80, 0x41, 0x80, 0x90,
            0x12, 0, 0, 0x10, 0x1F, 0x40, 0x80, 0x60, 1, 0x90,
            0x12, 0, 0, 0x13, 0xA0, 0x41, 0x80, 0x62, 0x56, 0x34, 0x12, 0x90,
            0x60, 0x20, 0x60, 0x10, 0x94,
            0x73
        ];
        Ff7BattleAiResult changed = Ff7BattleAi.Run(lifeCycle, context, [], formation, random);
        Ff7BattleAiActor destination = changed.ActorUpdates.Single(actor => actor.Slot == 5);
        Check(destination.HitPoints == 300 && destination.MaxHitPoints == 1000 && destination.MagicPoints == 30 && destination.MaxMagicPoints == 80 && destination.Statuses == 0x200000 && destination.PreviousStatuses == 0x40 && destination.ElementHalve == 0x123456, "HP restoration, capture bit, elemental mask or transfer ownership changed.");
        Check(changed.TransferUpdates.SequenceEqual(new Ff7BattleAiTransfer[] { new(4, 5) }), "Opcode94 must report the first source and all destinations for encounter-owned turn-state transfer.");
        Check(actors[2].HitPoints == 1 && actors[2].ElementHalve is null, "AI actor projections must not mutate caller state.");

        byte[] globalAliasesAndSave =
        [
            0x11, 0x10, 0x20, 0x60, 0xA5, 0x90,
            0x60, 1, 0x60, 7, 0x95,
            0x11, 0x10, 0x20, 0x60, 0, 0x90,
            0x60, 0, 0x60, 7, 0x95,
            0x10, 0x10, 0x20, 0x60, 0, 0x90,
            0x12, 0, 0, 0x02, 0x10, 0x20, 0x90,
            0x12, 0xA0, 0x21, 0x61, 0x34, 0x12, 0x90,
            0x73
        ];
        Ff7BattleAiResult global = Ff7BattleAi.Run(globalAliasesAndSave, context, [], formation, random);
        Check(global.SaveBank1Writes.Count == 1 && global.SaveBank1Writes[7] == 0xA5 && global.GlobalMemory[2] == 0xA4 && BinaryPrimitives.ReadUInt16LittleEndian(global.LocalMemory) == 0xA4 && BinaryPrimitives.ReadUInt16LittleEndian(global.GlobalMemory.AsSpan(52)) == 0x1234, "Global bit/byte/word aliases or ordered save-bank read-after-write changed.");
        Check(context.GlobalMemory![2] == 0 && context.SaveBank1![7] == 0, "AI global/save exchange must not mutate caller state.");
        byte[] nativeProperties =
        [
            0x12, 0x60, 0x20, 0x11, 0x78, 0x40, 0x80, 0x60, 37, 0x90,
            0x12, 0x60, 0x20, 0x11, 0xB0, 0x42, 0x80, 0x60, 19, 0x90,
            0x12, 0x60, 0x20, 0x12, 0x30, 0x41, 0x80, 0x61, 0xCD, 0xAB, 0x90,
            0x11, 0, 0, 0x02, 0x60, 0x20, 0x01, 0x78, 0x40, 0x80, 0x83, 0x90,
            0x11, 8, 0, 0x02, 0x60, 0x20, 0x01, 0xB0, 0x42, 0x80, 0x83, 0x90,
            0x73
        ];
        Ff7BattleAiResult projected = Ff7BattleAi.Run(nativeProperties, context, [], formation, random);
        Ff7BattleAiActor projectedSelf = projected.ActorUpdates.Single(actor => actor.Slot == 4);
        Check(projected.LocalMemory[0] == 37 && projected.LocalMemory[1] == 19 && projectedSelf.ExtraElementAbsorb == 0xABCD, "Physical evasion, hit effect or extra elemental absorb projection changed.");
        byte[] redirected = [0x12, 0x60, 0x20, 0x60, 1, 0x90, 0x60, 1, 0x60, 0, 0x92, 0x73];
        Ff7BattleAiResult playerCommand = Ff7BattleAi.Run(redirected, context, [], formation, random);
        Check(playerCommand.Commands.Single() is { ActorSlot: 0, CommandId: 1, AttackId: 0, SceneAttackSlot: -1 }, "A native self-mask write must redirect a queued player command to its selected actor.");
        Ff7BattleAiResult callbackCommand = Ff7BattleAi.Run([0x60, 0x22, 0x60, 4, 0x92, 0x73], context, [], formation, random);
        Check(callbackCommand.Commands.Single() is { ActorSlot: 4, CommandId: 0x22, AttackId: 4, SceneAttackSlot: -1 }, "Native callback invocation commands must retain callback index rather than resolve a scene attack.");
        byte[] formationGlobals = selected.GlobalMemory.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(formationGlobals.AsSpan(14), 0x20);
        byte[] formationScript = [0x11, 0, 0, 0x60, 71, 0x90, 0x12, 0x60, 0x20, 0x11, 0xB0, 0x42, 0x80, 0x60, 11, 0x90, 0x73];
        Ff7BattleAiResult formationResult = Ff7BattleAi.Run(formationScript, context with { ActorSlot = 3, GlobalMemory = formationGlobals }, [], formation, random);
        Check(formationResult.LocalMemory[0] == 71 && BinaryPrimitives.ReadUInt16LittleEndian(formationResult.GlobalMemory.AsSpan(14)) == 0x20 && formationResult.ActorUpdates.Single().Slot == 4 && formationResult.ActorUpdates.Single().HitEffect == 11, "Reserved formation local owner must retain the preceding actor's shared self/target masks.");
        byte[] wideMemory = new byte[Ff7BattleAi.LocalMemorySize];
        BinaryPrimitives.WriteUInt32LittleEndian(wideMemory, 0xF1234567);
        Ff7BattleAiResult truncated = Ff7BattleAi.Run([0x13, 0x20, 0, 0x03, 0, 0, 0x90, 0x73], context, wideMemory, formation, random);
        Check(BinaryPrimitives.ReadUInt32LittleEndian(truncated.LocalMemory.AsSpan(4)) == 0x234567, "Native32-bit memory reads must pass through the24-bit operand stack before stores.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
