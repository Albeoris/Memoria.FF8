namespace Albeoris.Games.FF8.Jsm;

public interface IExpressionStack
{
    int Count { get; }

    void Push(IJsmExpression item);

    IJsmExpression Pop();
}
