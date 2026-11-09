using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private void InitializePaletteState()
    {
        if (field.Palettes is null || field.Background is null)
        {
            return;
        }
        if (Process.PaletteRowsUseCachedIndices && Process.PaletteRows.Length != 0)
        {
            return;
        }
        bool legacy = Process.PaletteRows.Length != 0;
        Process = Process with
        {
            PaletteRows = field.Palettes.CreateCachedBackgroundRows(field.Background),
            PaletteRowsUseCachedIndices = true,
            PaletteBuffer = legacy ? new ushort[1024] : Process.PaletteBuffer,
            PaletteVersion = legacy ? Process.PaletteVersion + 1 : Process.PaletteVersion
        };
        if (legacy)
        {
            Console.Error.WriteLine("FFVII compatibility: legacy palette presentation and scratch colors were reset from the selected field resources; recorded progress is preserved.");
        }
    }

    private void Palette(FieldPaletteCommand command)
    {
        if (command.Source is < 0 or > 255 || command.Destination is < 0 or > 255 || command.Count is < 1 or > 256 || command.First is < 0 or > 255)
        {
            throw new InvalidDataException("FFVII palette command operands are invalid.");
        }
        ushort[] buffer = (ushort[])Process.PaletteBuffer.Clone();
        int source = command.Source * 16 + command.First;
        int destination = command.Destination * 16 + command.First;
        if (command.Operation == FieldPaletteOperation.Store)
        {
            if (command.Source >= Process.PaletteRows.Length || command.First + command.Count > Process.PaletteRows[command.Source].Length || destination + command.Count > buffer.Length)
            {
                throw new InvalidDataException("FFVII palette store exceeds its native row or buffer.");
            }
            Process.PaletteRows[command.Source].AsSpan(command.First, command.Count).CopyTo(buffer.AsSpan(destination));
        }
        else if (command.Operation == FieldPaletteOperation.Load)
        {
            if (command.Destination >= Process.PaletteRows.Length || command.First + command.Count > Process.PaletteRows[command.Destination].Length || source + command.Count > buffer.Length)
            {
                throw new InvalidDataException("FFVII palette load exceeds its native row or buffer.");
            }
            ushort[][] rows = Process.PaletteRows.Select(row => (ushort[])row.Clone()).ToArray();
            buffer.AsSpan(source, command.Count).CopyTo(rows[command.Destination].AsSpan(command.First));
            Process = Process with { PaletteRows = rows, PaletteVersion = Process.PaletteVersion + 1 };
        }
        else
        {
            if (source + command.Count > buffer.Length || destination + command.Count > buffer.Length)
            {
                throw new InvalidDataException("FFVII palette operation exceeds the native buffer.");
            }
            for (int index = 0; index < command.Count; index++)
            {
                ushort color = buffer[source + index];
                if (command.Operation == FieldPaletteOperation.Copy)
                {
                    buffer[destination + index] = color;
                    continue;
                }
                if (command.Operation == FieldPaletteOperation.Multiply && color == 0)
                {
                    continue;
                }
                int red;
                int green;
                int blue;
                if (command.Operation == FieldPaletteOperation.Add)
                {
                    red = Math.Clamp((color & 31) + unchecked((sbyte)command.Red), 0, 31);
                    green = Math.Clamp(((color >> 5) & 31) + unchecked((sbyte)command.Green), 0, 31);
                    blue = Math.Clamp(((color >> 10) & 31) + unchecked((sbyte)command.Blue), 0, 31);
                }
                else if (command.Operation == FieldPaletteOperation.Multiply)
                {
                    red = Math.Min(31, (((color << 1) & 62) * command.Red) >> 7);
                    green = Math.Min(31, (((color >> 4) & 63) * command.Green) >> 7);
                    blue = Math.Min(31, (((color >> 9) & 63) * command.Blue) >> 7);
                }
                else
                {
                    throw new NotSupportedException("The FFVII palette operation is unsupported.");
                }
                int adjusted = red | green << 5 | blue << 10 | color & 32768;
                if (adjusted == 0 && (color != 0 || command.Operation == FieldPaletteOperation.Multiply))
                {
                    adjusted = 32768;
                }
                buffer[destination + index] = (ushort)adjusted;
            }
        }
        Process = Process with { PaletteBuffer = buffer };
    }
}
