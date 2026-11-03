using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record EncounterNativeTextCue(byte Text, byte Duration);
public sealed record EncounterNativeEventResources(ImmutableArray<EncounterNativeTextCue> Opening, ImmutableArray<EncounterNativeTextCue> Party, ImmutableArray<EncounterNativeTextCue> LowHpTerminal, ImmutableArray<EncounterNativeTextCue> DeathTerminal);

public static class EncounterNativeEventResourceReader
{
    public static EncounterNativeEventResources Bind(NativeBattleGraphicsResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resource.Validate();
        return new(Read(13, true, 1, 0), Read(14, true, 2, 0), Read(15, true, 1, 1), Read(17, false, 1, 2));

        ImmutableArray<EncounterNativeTextCue> Read(byte sequence, bool title, int messages, int terminalOrder)
        {
            if (!resource.Contains(sequence))
            {
                throw new InvalidDataException($"Native encounter event sequence {sequence} is absent.");
            }
            int start = resource.SequenceStart(sequence);
            int end = resource.SequenceEnd(sequence);
            byte[] code = resource.SequenceSection.AsSpan().Slice(start, end - start).ToArray();
            int cursor = 0;
            if (title)
            {
                Require([0xBB]);
            }
            ImmutableArray<EncounterNativeTextCue>.Builder lines = ImmutableArray.CreateBuilder<EncounterNativeTextCue>(messages);
            for (int index = 0; index < messages; index++)
            {
                Require([0x91]);
                if (cursor > code.Length - 2)
                {
                    Fail();
                }
                byte text = code[cursor++];
                byte duration = code[cursor++];
                _ = resource.ReadText91(text);
                lines.Add(new(text, duration));
                Require([0xC1, duration, 0xE5, 0x7F, 0xA0, 0, 0xA1, 0xC3, 0x7F, 0xC5, 0xFF, 0xE5, 0x7F, 0xE7, 0xF7]);
            }
            if (terminalOrder == 1)
            {
                Require([0x9D, 0xAA, 0xA2]);
            }
            else if (terminalOrder == 2)
            {
                Require([0xAA, 0x9D, 0xA2]);
            }
            else
            {
                Require([0xAA, 0xA2]);
            }
            if (code.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            {
                Fail();
            }
            return lines.MoveToImmutable();

            void Require(ReadOnlySpan<byte> expected)
            {
                if (cursor > code.Length - expected.Length || !code.AsSpan(cursor, expected.Length).SequenceEqual(expected))
                {
                    Fail();
                }
                cursor += expected.Length;
            }

            void Fail()
            {
                throw new InvalidDataException($"Native encounter event sequence {sequence} has incompatible text, wait, contact or terminal control structure at relative byte {cursor}.");
            }
        }
    }
}
