using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public bool CanUsePlayerMenu => Battle is null && !HasPendingTick && !tickTailInProgress && (World is { } world
        ? CanOpenWorldPlayerMenu(world.Process)
        : CanOpenFieldPlayerMenu(Host.Process));

    public static bool CanOpenFieldPlayerMenu(Ff7FieldProcess process) => !process.MenuDisabled && !process.PlayerLocked && process.GlobalCommand == Ff7GlobalCommand.None && process.Transition is null && process.Movie is not { Playing: true } && process.Dialogues.All(dialogue => dialogue.Completed);

    public static bool CanOpenWorldPlayerMenu(Ff7WorldProcess process) => process.MainState == 1 && process.FadeRate == 0 && process.FadeDisplayedAlpha == 0 && process.MovementEnabled && process.ControlLock == 0 && process.Dialogue is not { Completed: false };

    public bool CanUseMenuPhs
    {
        get
        {
            byte visible = World?.ReadPersistentByte(29) ?? Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 29);
            byte disabled = World?.ReadPersistentByte(31) ?? Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31);
            return CanUsePlayerMenu && (visible & 1) != 0 && (disabled & 1) == 0;
        }
    }

    public bool TryChangeMenuOrder(int characterId, int otherCharacterId)
    {
        if (!CanUsePlayerMenu || characterId == 255 || otherCharacterId == 255)
        {
            return false;
        }

        int first = Array.IndexOf(Progress.Party, characterId);
        int second = Array.IndexOf(Progress.Party, otherCharacterId);
        if (first < 0 || second < 0)
        {
            return false;
        }
        if (first != second)
        {
            int[] members = (int[])Progress.Party.Clone();
            (members[first], members[second]) = (members[second], members[first]);
            PublishFieldParty(Progress with { Party = members });
            return true;
        }
        if (GuestFieldVitals?.StoryCharacters.Contains(characterId) == true)
        {
            return false;
        }

        Ff7CharacterState[] characters = (Ff7CharacterState[])Progress.Characters.Clone();
        int index = Array.FindIndex(characters, character => character.Id == characterId);
        byte[] record = (byte[])characters[index].NativeRecord.Clone();
        record[32] ^= 1;
        characters[index] = characters[index] with { NativeRecord = record };
        Progress = Progress with { Characters = characters };
        return true;
    }

    public bool TryReplaceMenuPartyMember(int activeId, int reserveId)
    {
        if (!CanUseMenuPhs || (uint)activeId > 8 || (uint)reserveId > 8)
        {
            return false;
        }
        int index = Array.IndexOf(Progress.Party, activeId);
        int reserveMask = 1 << reserveId;
        if (index < 0 || Progress.Party.Contains(reserveId) || (Progress.AvailableCharacters & reserveMask) == 0 || (Progress.LockedCharacters & (1 << activeId | reserveMask)) != 0)
        {
            return false;
        }

        int[] members = (int[])Progress.Party.Clone();
        members[index] = reserveId;
        PublishFieldParty(Progress with { Party = members });
        return true;
    }
}
