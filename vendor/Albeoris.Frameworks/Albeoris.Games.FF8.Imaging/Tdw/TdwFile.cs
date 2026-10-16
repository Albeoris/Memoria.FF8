using Albeoris.Games.Core.NsStreams;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Albeoris.Games.FF8.Imaging.Tdw;

/// <summary>A TDW font-width table followed by an optional TIM file.</summary>
public sealed class TdwFile
{
    public TdwFile(Byte[] widths, TimFile? tim)
    {
        ArgumentNullException.ThrowIfNull(widths);
        if (widths.Length % 2 != 0)
            throw new ArgumentException("TDW stores two four-bit widths in every byte.", nameof(widths));
        if (widths.Any(width => width > 0x0F))
            throw new ArgumentOutOfRangeException(nameof(widths), "A TDW width cannot exceed 15.");

        Widths = widths;
        Tim = tim;
    }

    /// <summary>Expanded four-bit character widths, in file order.</summary>
    public Byte[] Widths { get; }

    public TimFile? Tim { get; }
    public IReadOnlyList<TimImage> Images => Tim?.Images ?? [];

    public static TdwFile Read(Byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (MemoryStream input = new(content, writable: false))
        {
            return Read(input);
        }
    }

    public static TdwFile Read(ReadOnlySpan<Byte> content) => Read(content.ToArray());

    public Byte[] Write()
    {
        using (MemoryStream output = new())
        {
            Int32 tableOffset = TdwHeader.Size;
            Int32 dataOffset = checked(tableOffset + Widths.Length / 2);
            output.WriteStruct(new TdwHeader(tableOffset, dataOffset));

            using (HalfByteStream widths = new(output, leaveOpen: true))
                widths.Write(Widths);

            if (Tim is not null)
                output.Write(Tim.Write());
            return output.ToArray();
        }
    }

    private static TdwFile Read(Stream input)
    {
        TdwHeader header;
        try
        {
            header = input.ReadStruct<TdwHeader>();
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The TDW file is too short to contain its header.", exception);
        }

        if (header.TableOffset < TdwHeader.Size || header.TableOffset > input.Length)
            throw new InvalidDataException($"The TDW table offset {header.TableOffset} is outside the file.");
        if (header.DataOffset < header.TableOffset || header.DataOffset > input.Length)
        {
            throw new InvalidDataException(
                $"The TDW image offset {header.DataOffset} is outside the file or precedes the width table.");
        }

        input.Position = header.TableOffset;
        Int32 packedWidthCount = checked(header.DataOffset - header.TableOffset);
        Byte[] widths = new Byte[checked(packedWidthCount * 2)];
        using (HalfByteStream halfBytes = new(input, leaveOpen: true))
        {
            try
            {
                halfBytes.ReadExactly(widths);
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("The TDW width table ended unexpectedly.", exception);
            }
        }

        if (header.DataOffset == input.Length)
            return new TdwFile(widths, null);

        input.Position = header.DataOffset;
        TimFile tim = TimFile.Read(input);
        if (input.Position != input.Length)
            throw new InvalidDataException("TDW reading did not finish at the end of the stream.");
        return new TdwFile(widths, tim);
    }
}