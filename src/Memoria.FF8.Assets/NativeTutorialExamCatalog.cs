using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeTutorialExamQuestion(int Index, byte CorrectAnswer, string Text, ImmutableArray<string> Options, ImmutableArray<byte> Bytes);
public sealed record NativeTutorialExamLevel(int Level, ImmutableArray<NativeTutorialExamQuestion> Questions);
public sealed record NativeTutorialExamProgress(int PassedTests, int SeeDRank, int CharacterLevel, NativeCharacterVitals? Party = null, IReadOnlyDictionary<int, long>? InformationNumbers = null);
public sealed record NativeTutorialExamResult(int Score, bool Passed, int PromptIndex, NativeTutorialExamProgress Progress);
public sealed record NativeTutorialInformationNode(int Id, ushort OriginId, ushort LeftId, ushort RightId, string Title, ImmutableArray<byte> Body);
public sealed record NativeTutorialInformationLink(int TargetId, string Label, int LineIndex = 0);
public sealed record NativeTutorialInformationPage(NativeTutorialInformationNode Node, string Text, ImmutableArray<NativeTutorialInformationLink> Links);

/// <summary>Native 4D4960/4D4D30 exam bank and 4D5E00/4D5F10 Information text graph.</summary>
public sealed class NativeTutorialExamCatalog
{
    private NativeTutorialExamCatalog(ImmutableArray<NativeTutorialExamLevel> levels, ImmutableArray<string> prompts, ImmutableArray<NativeTutorialInformationNode> information)
    {
        Levels = levels;
        Prompts = prompts;
        Information = information;
    }

    public ImmutableArray<NativeTutorialExamLevel> Levels { get; }
    public ImmutableArray<string> Prompts { get; }
    public ImmutableArray<NativeTutorialInformationNode> Information { get; }

    public NativeTutorialExamResult Evaluate(NativeTutorialExamProgress progress, int level, IReadOnlyList<byte> answers, bool review)
    {
        ValidateProgress(progress);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 30);
        ArgumentNullException.ThrowIfNull(answers);
        if (answers.Count != 10 || answers.Any(answer => answer > 1))
        {
            throw new ArgumentException("The original SeeD test requires ten YES/NO answer indices.", nameof(answers));
        }
        if (review ? level > progress.PassedTests : level != progress.PassedTests + 1 || level > progress.CharacterLevel || progress.SeeDRank < 0)
        {
            throw new InvalidOperationException("The selected original SeeD test is not available at the current isolated progress.");
        }
        int score = Levels[level - 1].Questions.Count(question => answers[question.Index] == question.CorrectAnswer) * 10;
        bool passed = score == 100;
        NativeTutorialExamProgress next = passed && !review ? progress with { PassedTests = progress.PassedTests + 1, SeeDRank = Math.Min(31, progress.SeeDRank + 1) } : progress;
        int prompt = review ? passed ? 8 : 9 : passed ? progress.SeeDRank == 31 ? 3 : 2 : 4;
        return new NativeTutorialExamResult(score, passed, prompt, next);
    }

    public NativeTutorialInformationPage InformationPage(int id, IReadOnlySet<int> unlocked, NativeTutorialExamProgress progress)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, (uint)Information.Length);
        ArgumentNullException.ThrowIfNull(unlocked);
        HashSet<int> flags = unlocked.ToHashSet();
        // 4D5E00 toggles the two descriptions of each refinement ability in the process-local copy of the availability set.
        int[] abilities = [97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 114, 113];
        int[] absent = [89, 91, 93, 95, 97, 99, 101, 103, 105, 107, 109, 111, 113, 115, 117, 119, 121, 124];
        if (progress.Party is NativeCharacterVitals party)
        {
            HashSet<int> learned = Enumerable.Range(0, 16).Where(gf => (party.GuardianForces[gf].Flags & 1) != 0).SelectMany(gf => party.GuardianForceAbilities(gf).Where(ability => ability.Learned).Select(ability => ability.Ability)).ToHashSet();
            for (int index = 0; index < abilities.Length; index++)
            {
                bool available = learned.Contains(abilities[index]);
                flags.Remove(absent[index] + (available ? 0 : 1));
                flags.Add(absent[index] + (available ? 1 : 0));
            }
        }
        NativeTutorialInformationNode node = Information[id];
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        List<byte> plain = [];
        List<byte> label = [];
        List<NativeTutorialInformationLink> links = [];
        int target = -1;
        int lineIndex = 0;
        int linkLine = 0;
        bool visible = true;
        void FinishLink()
        {
            if (target >= 0)
            {
                links.Add(new NativeTutorialInformationLink(target, encoding.GetString(label.ToArray()).Trim(), linkLine));
                target = -1;
                label.Clear();
            }
        }
        void Append(byte value)
        {
            plain.Add(value);
            if (target >= 0)
            {
                label.Add(value);
            }
        }
        for (int position = 0; position < node.Body.Length; position++)
        {
            byte value = node.Body[position];
            if (value == 0x0A)
            {
                int operand = node.Body[++position];
                if (operand == 0x3F)
                {
                    visible = true;
                }
                else if (operand < 0xC0)
                {
                    visible = operand >= 0x40 && flags.Contains(operand - 0x40);
                }
                else if (visible)
                {
                    long? number = ReadInformationNumber(operand, progress);
                    foreach (byte encoded in encoding.GetBytes(number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"))
                    {
                        Append(encoded);
                    }
                }
                continue;
            }
            if (value == 0x0B)
            {
                int destination = (node.Body[++position] << 7) + node.Body[++position] - 0x1020;
                FinishLink();
                if (visible)
                {
                    if ((uint)destination >= Information.Length)
                    {
                        throw new InvalidDataException("Original Information link is outside its map.");
                    }
                    target = destination;
                    linkLine = lineIndex;
                }
                continue;
            }
            if (value == 2)
            {
                FinishLink();
                if (visible)
                {
                    lineIndex++;
                }
            }
            if (value is >= 3 and <= 15)
            {
                byte operand = node.Body[++position];
                if (visible)
                {
                    Append(value);
                    Append(operand);
                }
            }
            else if (visible)
            {
                Append(value);
            }
        }
        FinishLink();
        return new NativeTutorialInformationPage(node, encoding.GetString(plain.ToArray()), links.ToImmutableArray());
    }

    public static void ValidateProgress(NativeTutorialExamProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.PassedTests is < 0 or > 30 || progress.SeeDRank is < -1 or > 31 || progress.CharacterLevel is < 1 or > 100)
        {
            throw new ArgumentException("Original SeeD test progress requires thirty levels, native rank and Squall's actual character level.", nameof(progress));
        }
    }

    private static long? ReadInformationNumber(int operand, NativeTutorialExamProgress progress)
    {
        if (progress.InformationNumbers?.TryGetValue(operand, out long value) == true)
        {
            return value;
        }
        NativeCharacterVitals? party = progress.Party;
        return operand switch
        {
            >= 0xD0 and < 0xD8 when party is not null => party.Records[operand - 0xD0].KillCount,
            >= 0xD8 and < 0xE0 when party is not null => party.Records[operand - 0xD8].DeathCount,
            >= 0xE0 and < 0xF0 when party is not null => party.GuardianForces[operand - 0xE0].KillCount,
            >= 0xF0 when party is not null => party.GuardianForces[operand - 0xF0].DeathCount,
            _ => null
        };
    }

    public static NativeTutorialExamCatalog Read(byte[] executable, byte[] header, byte[] content)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash || header.Length != 2048)
        {
            throw new NotSupportedException("Original SeeD tests require the authenticated English executable and native menu directory.");
        }
        return Read(header, content);
    }

    public static NativeTutorialExamCatalog Read(byte[] header, byte[] content)
    {
        if (header.Length != 2048)
        {
            throw new InvalidDataException("Native SeeD tests require the complete menu directory.");
        }
        byte[] Resource(int slot)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8 + 4));
            if (offset is 0 or uint.MaxValue || (ulong)offset - 1 + length > (ulong)content.Length)
            {
                throw new InvalidDataException("Original tutorial resource exceeds mngrp.bin.");
            }
            return content.AsSpan(checked((int)offset - 1), checked((int)length)).ToArray();
        }
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        ImmutableArray<(byte Prefix, byte[] Bytes)> ReadRecords(int slot)
        {
            byte[] bytes = Resource(slot);
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            if (count > (bytes.Length - 2) / 2)
            {
                throw new InvalidDataException("Original SeeD test lacks its text offset table.");
            }
            ImmutableArray<(byte, byte[])>.Builder records = ImmutableArray.CreateBuilder<(byte, byte[])>(count);
            for (int index = 0; index < count; index++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + index * 2));
                int end = Array.IndexOf(bytes, (byte)0, offset + 1);
                if (offset < 2 + count * 2 || end < offset)
                {
                    throw new InvalidDataException("Original SeeD test record lacks its prefix or terminator.");
                }
                records.Add((bytes[offset], bytes[(offset + 1)..end]));
            }
            return records.MoveToImmutable();
        }
        ImmutableArray<NativeTutorialExamLevel>.Builder levels = ImmutableArray.CreateBuilder<NativeTutorialExamLevel>(30);
        for (int level = 1; level <= 30; level++)
        {
            ImmutableArray<(byte Prefix, byte[] Bytes)> rows = ReadRecords(95 + level);
            if (rows.Length != 10 || rows.Any(row => row.Prefix > 1))
            {
                throw new InvalidDataException("Original SeeD level must contain ten binary answer prefixes.");
            }
            levels.Add(new NativeTutorialExamLevel(level, rows.Select((row, index) =>
            {
                string text = encoding.GetString(row.Bytes);
                MatchCollection choices = Regex.Matches(text, @"\{Option (\d+)\}([^\{]*)");
                if (choices.Count != 2 || choices[0].Groups[1].Value != "32" || choices[1].Groups[1].Value != "33")
                {
                    throw new InvalidDataException("Original SeeD question has no YES/NO option pair.");
                }
                return new NativeTutorialExamQuestion(index, row.Prefix, text[..choices[0].Index].Trim(), choices.Select(choice => choice.Groups[2].Value.Trim()).ToImmutableArray(), row.Bytes.ToImmutableArray());
            }).ToImmutableArray()));
        }
        ImmutableArray<string> prompts = ReadRecords(95).Select(row => encoding.GetString(row.Bytes)).ToImmutableArray();
        byte[] map = Resource(127);
        int references = BinaryPrimitives.ReadInt32LittleEndian(map);
        if (references != 425 || 4 + references * 4 > map.Length)
        {
            throw new InvalidDataException("Original Information map lacks its 425 native references.");
        }
        byte[][] blocks = Enumerable.Range(128, 6).Select(Resource).ToArray();
        ImmutableArray<NativeTutorialInformationNode>.Builder information = ImmutableArray.CreateBuilder<NativeTutorialInformationNode>(references);
        for (int id = 0; id < references; id++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(4 + id * 4));
            int section = BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(6 + id * 4));
            if ((uint)section >= blocks.Length || offset + 8 > blocks[section].Length)
            {
                throw new InvalidDataException("Original Information node is outside its text section.");
            }
            byte[] bytes = blocks[section];
            int length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 6));
            int titleEnd = Array.IndexOf(bytes, (byte)0, offset + 8);
            int bodyEnd = titleEnd < 0 ? -1 : Array.IndexOf(bytes, (byte)0, titleEnd + 1);
            if (length < 10 || offset + length > bytes.Length || titleEnd < 0 || bodyEnd < 0 || bodyEnd >= offset + length)
            {
                throw new InvalidDataException("Original Information node lacks its bounded title/body pair.");
            }
            information.Add(new NativeTutorialInformationNode(id, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 4)), encoding.GetString(bytes.AsSpan(offset + 8, titleEnd - offset - 8)), bytes[(titleEnd + 1)..bodyEnd].ToImmutableArray()));
        }
        return new NativeTutorialExamCatalog(levels.MoveToImmutable(), prompts, information.MoveToImmutable());
    }
}

public sealed partial class GameAssets
{
    private NativeTutorialExamCatalog? nativeTutorialExams;

    public NativeTutorialExamCatalog ReadNativeTutorialExamCatalog()
    {
        using IFlArchive menu = OpenArchive("menu");
        return nativeTutorialExams ??= NativeTutorialExamCatalog.Read(Read(menu, "mngrphd.bin"), Read(menu, "mngrp.bin"));
    }
}
