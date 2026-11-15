using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleCommandGrantTests
{
    public static void Run()
    {
        RankAndBackwardGrantSelection();
        NativeOrderDedupAndSelectors();
        MalformedResourcesAreRejected();
        OriginalSenseAndManipulate();
        Console.WriteLine("PASS native equipped battle command grants");
    }

    private static void RankAndBackwardGrantSelection()
    {
        byte[] materia = Materia();
        Set(materia, 2, [1, 2, 3, 4], [5, 255, 7, 255, 11]);
        foreach ((uint ap, byte rank, byte command) in new (uint, byte, byte)[] { (0, 1, 5), (99, 1, 5), (100, 2, 5), (199, 2, 5), (200, 3, 7), (300, 4, 7), (400, 5, 11), (0xFFFFFF, 5, 11) })
        {
            Ff7CharacterState character = Character();
            Equip(character, 0, 2, ap);
            string before = JsonSerializer.Serialize(character);
            Ff7NativeBattleCommandGrant grant = Ff7NativeBattleCommandGrants.Read(character, [new(8, materia)]).Single();
            Check(grant == new Ff7NativeBattleCommandGrant(command, 2, rank, 0), "Command grant lost native AP rank, descendingFF fallback or source identity.");
            Check(before == JsonSerializer.Serialize(character), "Command projection mutated its source character.");
        }
        Set(materia, 2, [3, 65535, 2, 4], [5, 6, 7, 11, 12]);
        Ff7CharacterState irregular = Character();
        Equip(irregular, 0, 2, 200);
        Check(Ff7NativeBattleCommandGrants.Read(irregular, [new(8, materia)]).Single() is { Rank: 4, Command: 11 }, "Command ranks did not use the shared descending threshold scan over an interiorFFFF.");
    }

    private static void NativeOrderDedupAndSelectors()
    {
        byte[] materia = Materia();
        Set(materia, 2, [1, 65535, 65535, 65535], [11, 255, 255, 255, 255]);
        Set(materia, 3, [1, 65535, 65535, 65535], [11, 255, 255, 255, 255], 0xF6);
        Set(materia, 4, [65535, 65535, 65535, 65535], [6, 255, 255, 255, 255]);
        Set(materia, 5, [65535, 65535, 65535, 65535], [31, 255, 255, 255, 255], 0x19);
        Ff7CharacterState character = Character();
        Equip(character, 0, 3, 100);
        Equip(character, 1, 2, 0);
        Equip(character, 5, 5, 0);
        Equip(character, 15, 4, 0);
        IReadOnlyList<Ff7NativeBattleCommandGrant> grants = Ff7NativeBattleCommandGrants.Read(character, [new(8, materia)]);
        Check(grants.SequenceEqual(new Ff7NativeBattleCommandGrant[] { new(11, 3, 2, 0), new(6, 4, 1, 15) }), "Command grants were sorted, duplicated, high-nibble gated or projected from a different materia category.");
        Check(Ff7NativeBattleCommandGrants.Read(Character(), [new(8, materia)]).Count == 0, "Empty equipment invented a command grant.");
        Set(materia, 4, [65535, 65535, 65535, 65535], [255, 255, 255, 255, 255]);
        Ff7CharacterState emptyGrant = Character();
        Equip(emptyGrant, 0, 4, 0);
        Reject(emptyGrant, [new(8, materia)]);
    }

    private static void MalformedResourcesAreRejected()
    {
        Ff7CharacterState character = Character();
        Equip(character, 0, 2, 0);
        foreach ((ushort[] thresholds, byte[] commands) in new (ushort[], byte[])[]
        {
            ([1, 65535, 65535, 65535], [6, 32, 255, 255, 255])
        })
        {
            byte[] materia = Materia();
            Set(materia, 2, thresholds, commands);
            Reject(character, [new(8, materia)]);
        }
        Reject(character, []);
        Reject(character, [new(8, Materia()), new(8, Materia())]);
        Reject(character, [new(8, new byte[96 * 20 - 1])]);
        Reject(character with { NativeRecord = new byte[131] }, [new(8, Materia())]);
        Equip(character, 0, 96, 0);
        Reject(character, [new(8, Materia())]);
    }

    private static void OriginalSenseAndManipulate()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original command grants: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7CharacterState character = Character();
        Equip(character, 0, 42, 0);
        Equip(character, 8, 37, 0);
        IReadOnlyList<Ff7NativeBattleCommandGrant> grants = Ff7NativeBattleCommandGrants.Read(character, kernel);
        Check(grants.SequenceEqual(new Ff7NativeBattleCommandGrant[] { new(0x0B, 42, 1, 0), new(6, 37, 1, 8) }), "Original Manipulate or Sense materia lost its native command or source slot.");
    }

    private static Ff7CharacterState Character()
    {
        byte[] data = new byte[132];
        data.AsSpan(64, 64).Fill(255);
        return new(0, "Sample", 1, 0, 1, 1, data);
    }

    private static byte[] Materia()
    {
        byte[] data = new byte[96 * 20];
        for (int id = 0; id < 96; id++)
        {
            data.AsSpan(id * 20, 8).Fill(255);
            data.AsSpan(id * 20 + 14, 5).Fill(255);
        }
        return data;
    }

    private static void Set(byte[] materia, int id, ushort[] thresholds, byte[] grants, byte category = 0x16)
    {
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(materia.AsSpan(id * 20 + index * 2), thresholds[index]);
        }
        materia[id * 20 + 13] = category;
        grants.CopyTo(materia, id * 20 + 14);
    }

    private static void Equip(Ff7CharacterState character, int slot, byte id, uint ap)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(character.NativeRecord.AsSpan(64 + slot * 4), id | (ap << 8));
    }

    private static void Reject(Ff7CharacterState character, IReadOnlyList<KernelSection> kernel)
    {
        try
        {
            Ff7NativeBattleCommandGrants.Read(character, kernel);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Malformed native command grant data was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
