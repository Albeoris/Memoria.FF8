using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    internal string CaptureTransitionProgressWitness()
    {
        return CaptureTransitionProgressWitness(Machine.Variables.CopyPersistent(), Progress);
    }

    internal static string CaptureTransitionProgressWitness(byte[] persistent, Ff7Progress progress)
    {
        FieldVariables variables = new(persistent, new byte[256]);
        foreach (FieldVariableRead publication in navigationExternalWrites)
        {
            if (publication.Word)
            {
                variables.WriteWord(publication.Bank, checked((byte)publication.Operand), 0);
            }
            else
            {
                variables.WriteByte(publication.Bank, checked((byte)publication.Operand), 0);
            }
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Persistent = variables.CopyPersistent(), progress.Inventory, progress.Materia, progress.Gil, Characters = progress.Characters.Select(character => new { character.Id, character.Level, character.Experience, character.HitPoints, character.MagicPoints }), progress.Party, progress.AvailableCharacters, progress.LockedCharacters, progress.CompletedBattles });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
