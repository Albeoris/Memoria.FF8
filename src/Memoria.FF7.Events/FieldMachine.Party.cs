namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    /// <summary>Runs the native main-party to script-bank synchronization at the caller's admitted field-entry boundary.</summary>
    public void SynchronizePartyFromMain(IReadOnlyList<int> mainParty)
    {
        ArgumentNullException.ThrowIfNull(mainParty);
        if (mainParty.Count != 3 || mainParty.Any(member => member is < 0 or > 255))
        {
            throw new ArgumentException("FFVII main party requires exactly three byte identities.", nameof(mainParty));
        }
        int[] bank = ReadParty();
        int[] removed = bank.Where(member => !mainParty.Contains(member)).ToArray();
        int[] added = mainParty.Where(member => !bank.Contains(member)).ToArray();
        foreach (int member in removed)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                if (bank[slot] == member)
                {
                    bank[slot] = 255;
                }
            }
        }
        foreach (int member in added)
        {
            int slot = Array.IndexOf(bank, 255);
            if (slot >= 0)
            {
                bank[slot] = member;
            }
        }
        WriteParty(bank);
    }

    private int[] ReadParty() => Enumerable.Range(9, 3).Select(address => (int)Variables.ReadByte(FieldBank.Persistent1Byte, (byte)address)).ToArray();
    private void WriteParty(int[] party)
    {
        for (int slot = 0; slot < 3; slot++)
        {
            Variables.WriteByte(FieldBank.Persistent1Byte, (byte)(9 + slot), (byte)party[slot]);
        }
    }

    private bool ChangeParty(int id, byte opcode)
    {
        var key = (id, entities[id].Priority, entities[id].Pc);
        if (pendingRequests.TryGetValue(key, out FieldHostRequest? pending))
        {
            return Host(id, pending.Command, opcode is 0xC8 or 0xC9 ? 2 : opcode == 0xCA ? 4 : 6);
        }
        if (host is null)
        {
            throw new NotSupportedException("Party synchronization requires an FFVII host.");
        }
        int[] party = ReadParty();
        ushort visibility = 0;
        if (opcode == 0xC8)
        {
            int member = B(id, 1);
            if (!party.Contains(member))
            {
                int slot = Array.IndexOf(party, 255);
                party[slot < 0 ? 2 : slot] = member;
                visibility = MemberMask(member);
            }
        }
        else if (opcode == 0xC9)
        {
            int slot = Array.IndexOf(party, (int)B(id, 1));
            if (slot >= 0)
            {
                party[slot] = 255;
            }
        }
        else
        {
            int[] requested = opcode == 0xCA ? [B(id, 1), B(id, 2), B(id, 3)] : [R8(id, 1, 3), R8(id, 2, 4), R8(id, 3, 5)];
            foreach (int member in requested)
            {
                if (member != 255)
                {
                    for (int slot = 0; slot < 3; slot++)
                    {
                        if (party[slot] == member)
                        {
                            party[slot] = 255;
                        }
                    }
                }
            }
            foreach (int member in party)
            {
                int slot = Array.IndexOf(requested, 255);
                if (member != 255 && slot >= 0)
                {
                    requested[slot] = member;
                }
            }
            party = requested.Select(member => member == 254 ? 255 : member).ToArray();
            foreach (int member in party)
            {
                visibility |= MemberMask(member);
            }
        }
        WriteParty(party);
        return Host(id, new FieldPartyProjection(party[0], party[1], party[2], visibility), opcode is 0xC8 or 0xC9 ? 2 : opcode == 0xCA ? 4 : 6);
    }

    private static ushort MemberMask(int member)
    {
        if (member == 255)
        {
            return 0;
        }
        if (member is < 0 or > 15)
        {
            throw new NotSupportedException("Party identity exceeds the admitted native visibility mask.");
        }
        return (ushort)(1 << member);
    }
}
