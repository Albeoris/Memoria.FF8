using System.Collections.Immutable;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void ScriptedAdditionalPartyContract(GameAssets assets)
    {
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals Run(NativeCharacterVitals source, params (Jsm.Opcode Opcode, int Parameter)[] operations)
        {
            GameSession session = new(new GameState { CharacterVitals = source });
            EventEngine engine = new(session);
            List<(Jsm.Opcode, int)> script = [(Jsm.Opcode.LBL, 0), .. operations, (Jsm.Opcode.RET, 0)];
            engine.LoadField(100, SyntheticProgram(script.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Scene.Party.Clear();
            engine.Scene.Party.AddRange([0, 5, 4]);
            engine.Step();
            return session.CharacterVitals!;
        }

        NativeCharacterVitals source = initial with
        {
            CombatParty = [5, 0, 4],
            AdditionalCombatParty = [1, 2],
            AdditionalCombatPartyMinimumCapacities = ImmutableSortedDictionary<byte, byte>.Empty.Add(1, 5).Add(2, 6)
        };
        NativeCharacterVitals reset = Run(source, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0));
        Require(reset.CombatParty.SequenceEqual(new byte[] { 5, 0, 255 }) && reset.AdditionalCombatParty is null && reset.AdditionalCombatPartyMinimumCapacities is null, "SETPARTY retained an additional selection outside the authored party.");

        NativeCharacterVitals removed = Run(source, (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.SUBPARTY, 0));
        Require(removed.CombatParty.SequenceEqual(source.CombatParty) && removed.AdditionalCombatParty!.SequenceEqual(new byte[] { 2 }) && removed.AdditionalCombatPartyMinimumCapacities!.Count == 1 && removed.AdditionalCombatPartyMinimumCapacities[2] == 6, "SUBPARTY failed to remove its low-BYTE identity from additional slots or changed an unrelated choice.");

        NativeCharacterVitals changed = Run(source, (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CHANGEPARTY, 0));
        Require(changed.CombatParty.SequenceEqual(new byte[] { 5, 0, 1 }) && changed.AdditionalCombatParty!.SequenceEqual(new byte[] { 2 }) && changed.AdditionalCombatPartyMinimumCapacities!.Count == 1, "CHANGEPARTY retained a promoted additional row or discarded an unrelated choice.");

        NativeCharacterVitals added = Run(source, (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0));
        Require(added.CombatParty.SequenceEqual(new byte[] { 5, 0, 1 }) && added.AdditionalCombatParty!.SequenceEqual(new byte[] { 2 }) && added.AdditionalCombatPartyMinimumCapacities![2] == 6, "ADDPARTY retained a promoted additional row or changed an unrelated capacity selection.");

        NativeCharacterVital[] mappedRecords = source.Records.ToArray();
        (mappedRecords[1], mappedRecords[2]) = (mappedRecords[2], mappedRecords[1]);
        NativeCharacterVitals mappedRemoval = Run(source with { Records = mappedRecords }, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SUBPARTY, 0));
        Require(mappedRemoval.AdditionalCombatParty!.SequenceEqual(new byte[] { 1 }) && mappedRemoval.AdditionalCombatPartyMinimumCapacities![1] == 5, "SUBPARTY treated an identity operand as a physical additional row.");

        NativeCharacterVitals dreamed = source with { DreamEnabled = true };
        NativeCharacterVitals dreamRemoved = Run(dreamed, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SUBPARTY, 0));
        NativeCharacterVitals dreamReset = Run(dreamed, (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SETPARTY, 0));
        Require(dreamRemoved.AdditionalCombatParty!.SequenceEqual(source.AdditionalCombatParty) && dreamReset.AdditionalCombatParty!.SequenceEqual(source.AdditionalCombatParty) && dreamReset.AdditionalCombatPartyMinimumCapacities!.SequenceEqual(source.AdditionalCombatPartyMinimumCapacities), "Dream scripts changed the dormant normal-party additional selection.");

        foreach ((int field, int operation, byte companion) in new (int, int, byte)[] { (487, 680, 255), (864, 938, 5) })
        {
            JsmProgram original = JsmProgram.Read(assets.LoadField(field).Jsm);
            var commands = original.Operations.Skip(operation - 3).Take(4).ToArray();
            Require(commands.Length == 4 && commands[0].Opcode == Jsm.Opcode.PSHN_L && commands[0].Parameter == 0 && commands[1].Opcode == Jsm.Opcode.PSHN_L && commands[1].Parameter == companion && commands[2].Opcode == Jsm.Opcode.PSHN_L && commands[2].Parameter == 255 && commands[3].Opcode == Jsm.Opcode.SETPARTY, "Original Lunar transition changed its exact authored party operands.");
            NativeCharacterVitals authored = Run(source, commands.Select(command => (command.Opcode, command.Opcode == Jsm.Opcode.SETPARTY ? 0 : command.Parameter)).ToArray());
            Require(authored.CombatParty.SequenceEqual(new byte[] { companion, 0, 255 }) && authored.AdditionalCombatParty is null && authored.AdditionalCombatPartyMinimumCapacities is null, "Original Lunar transition retained a stale additional battle member.");
        }
    }
}
