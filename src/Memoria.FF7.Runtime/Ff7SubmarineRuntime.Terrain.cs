namespace Memoria.FF7.Runtime;

public sealed partial class Ff7SubmarineRuntime
{
    private void InitializeMines()
    {
        int remaining = Math.Min(500, ReadGlobal(0x988504));
        int water = ReadGlobal(0x988500) / 4096;
        int playerCell = ReadGlobal(0x9873C8);
        int playerRow = playerCell / 64;
        int playerColumn = playerCell % 64;
        while (remaining > 0)
        {
            bool candidate = false;
            for (int cell = 0; cell < 4096; cell++)
            {
                int offset = cell * 8;
                if ((state.Cells[offset + 5] & 0x2000) != 0)
                {
                    continue;
                }
                candidate = true;
                int row = cell / 64 - playerRow;
                int column = cell % 64 - playerColumn;
                if (row is >= -2 and <= 2 && column is >= -2 and <= 2 || state.Cells[offset + 7] <= water)
                {
                    continue;
                }
                if (NextRandom() >= 127)
                {
                    continue;
                }
                state.Cells[offset + 5] = 0x2000;
                int height = Math.Clamp(1024 - ((NextRandom() << 3) / 32767) * 64, 0, state.Cells[offset + 7]);
                state.Cells[offset + 6] = checked((ushort)height);
                remaining--;
                if (remaining == 0)
                {
                    break;
                }
            }
            if (!candidate)
            {
                break;
            }
        }
    }

    private void AdvanceTerrain()
    {
        for (int cell = 0; cell < 4096; cell++)
        {
            int offset = cell * 8;
            int height = state.Cells[offset];
            int flags = state.Cells[offset + 5];
            int target = state.Cells[offset + ((flags & 0x1000) != 0 ? 6 : 7)];
            if (height != target)
            {
                if (height < target)
                {
                    height += target - height > 2 ? 2 : 1;
                }
                else
                {
                    height -= height - target > 4 ? (height - target) / 4 : 1;
                }
                state.Cells[offset] = checked((ushort)height);
                if (cell % 64 != 0)
                {
                    state.Cells[offset - 8 + 3] = checked((ushort)height);
                }
                if (cell % 64 != 63)
                {
                    state.Cells[offset + 8 + 2] = checked((ushort)height);
                }
                if (cell / 64 != 63)
                {
                    state.Cells[offset + 512 + 4] = checked((ushort)height);
                }
                if (cell / 64 != 0)
                {
                    state.Cells[offset - 512 + 1] = checked((ushort)height);
                }
            }
            else
            {
                state.Cells[offset + 5] &= 0xEFFF;
            }
            flags = state.Cells[offset + 5];
            if ((flags & 0x8000) != 0)
            {
                int age = (flags & 255) - 1;
                int high = flags & 0xFF00;
                if (age < 0)
                {
                    age = 0;
                    high = 0;
                    state.Cells[offset + 6] = state.Cells[offset + 7];
                }
                state.Cells[offset + 5] = checked((ushort)(high | age));
            }
        }
    }
}
