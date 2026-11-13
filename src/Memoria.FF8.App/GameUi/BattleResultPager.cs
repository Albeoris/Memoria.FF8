namespace Memoria.FF8.GameUi;

internal sealed class BattleResultPager(int rowsPerPage)
{
    private int page;

    public IEnumerable<T> Rows<T>(IEnumerable<T> rows) => rows.Skip(page * rowsPerPage).Take(rowsPerPage);

    public bool MoveNext(int rowCount)
    {
        if ((page + 1) * rowsPerPage >= rowCount)
        {
            return false;
        }
        page++;
        return true;
    }

    public void MoveBack()
    {
        page = Math.Max(0, page - 1);
    }
}
