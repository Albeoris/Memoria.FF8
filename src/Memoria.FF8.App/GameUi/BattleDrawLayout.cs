namespace Memoria.FF8.GameUi;

internal readonly record struct BattleDrawLayout(int Count, int Columns, int Rows)
{
    public static BattleDrawLayout Calculate(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        int columns = 1;
        int capacity = 4;
        bool addColumn = true;
        while (count > columns * capacity)
        {
            if (addColumn)
            {
                columns++;
            }
            else
            {
                capacity += 4;
            }
            addColumn = !addColumn;
        }
        return new(count, columns, Math.Max(Math.Max(4, (columns - 1) * 4), (count + columns - 1) / columns));
    }

    public int ColumnRows(int column)
    {
        return Count / Columns + (column < Count % Columns ? 1 : 0);
    }

    public int GetIndex(int column, int row)
    {
        if (column < 0 || column >= Columns || row < 0 || row >= ColumnRows(column))
        {
            return -1;
        }
        return column * (Count / Columns) + Math.Min(column, Count % Columns) + row;
    }

    public (int Column, int Row) GetCell(int index)
    {
        for (int column = 0; column < Columns; column++)
        {
            int start = GetIndex(column, 0);
            if (index >= start && index < start + ColumnRows(column))
            {
                return (column, index - start);
            }
        }
        return (0, 0);
    }

    public int MoveVertical(int index, int direction)
    {
        (int column, int row) = GetCell(index);
        int rows = ColumnRows(column);
        return rows == 0 ? -1 : GetIndex(column, (row + direction + rows) % rows);
    }

    public int MoveHorizontal(int index, int direction, bool wrap = false)
    {
        (int column, int row) = GetCell(index);
        int next = column + direction;
        if (wrap)
        {
            next = (next + Columns) % Columns;
        }
        return next < 0 || next >= Columns ? -1 : GetIndex(next, Math.Min(row, ColumnRows(next) - 1));
    }
}
