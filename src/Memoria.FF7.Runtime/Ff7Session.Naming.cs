using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private Ff7CharacterNamingResources? namingResources;

    private Ff7CharacterNamingResources NamingResources => namingResources ??= Ff7CharacterNamingResources.Load(installation);

    private byte NamingIdentity(Ff7Menu menu)
    {
        if (menu.Mode != 6 || (uint)menu.Parameter >= NamingResources.SaveRecordIds.Count)
        {
            throw new InvalidDataException("The naming menu selects an unsupported native character identity.");
        }
        return checked((byte)menu.Parameter);
    }

    private void BeginNativeNaming(Ff7Menu menu)
    {
        byte identity = NamingIdentity(menu);
        if (!NamingResources.ResetsRecord(identity))
        {
            return;
        }
        int physical = NamingResources.RecordId(identity);
        Ff7CharacterState original = Progress.Characters.Single(character => character.Id == physical);
        byte[] record = NamingResources.CreateTemplate(identity);
        Ff7CharacterState initialized = ProjectNamingRecord(original, record, Ff7NativeSaveSlot.ReadText(record.AsSpan(16, 12)));
        Ff7Progress prepared = Progress with { Characters = Progress.Characters.Select(character => character.Id == physical ? initialized : character).ToArray() };
        prepared.Validate();
        Progress = prepared;
    }

    private void CompleteNativeNaming(Ff7Menu menu, string? nativeName)
    {
        byte identity = NamingIdentity(menu);
        int physical = NamingResources.RecordId(identity);
        Ff7CharacterState original = Progress.Characters.Single(character => character.Id == physical);
        if (original.NativeRecord[0] != identity)
        {
            throw new NotSupportedException("The naming menu's saved character profile has not undergone its required native initialization.");
        }
        byte[] record = original.NativeRecord.ToArray();
        string name = original.Name;
        if (nativeName is not null)
        {
            if (nativeName.Length is < 1 or > 9 || nativeName.Any(character => character is < ' ' or > '~'))
            {
                throw new ArgumentException("FFVII names require one through nine supported font characters.", nameof(nativeName));
            }
            record.AsSpan(16, 12).Fill(byte.MaxValue);
            for (int index = 0; index < nativeName.Length; index++)
            {
                record[16 + index] = (byte)(nativeName[index] - 32);
            }
            name = nativeName;
        }
        Ff7BattleRngState? randomState = Progress.BattleRng;
        if (!NamingResources.SkipsJoin(identity))
        {
            battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
            Ff7BattleRng random = new(battleKernel[2].Data.Span, randomState ?? new(new byte[8]));
            Ff7NativeGrowth growth = new(KernelGrowthTables.Read(battleKernel[2].Data.Span));
            record = growth.InitializeJoin(record, Progress.Characters, Progress.AvailableCharacters, random);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(46)));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(50)));
            randomState = random.Capture();
        }
        Ff7CharacterState completed = ProjectNamingRecord(original, record, name);
        Ff7Progress prepared = Progress with { Characters = Progress.Characters.Select(character => character.Id == physical ? completed : character).ToArray(), BattleRng = randomState };
        prepared.Validate();
        Progress = prepared;
    }

    private static Ff7CharacterState ProjectNamingRecord(Ff7CharacterState original, byte[] record, string name)
    {
        return original with
        {
            Name = name,
            Level = record[1],
            Experience = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(60)),
            HitPoints = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)),
            MagicPoints = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(48)),
            NativeRecord = record
        };
    }

    private void FlushNativePartyStatistics()
    {
        if (!Progress.NativePartyStatisticsDirty)
        {
            return;
        }
        PublishNativeMenuPartyStatistics();
        Progress = Progress with { NativePartyStatisticsDirty = false };
    }
}
