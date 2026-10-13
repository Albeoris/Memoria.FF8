using System.Collections;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Legacy integer consumers require an exact value; typed memory operations can preserve uncertainty.</summary>
internal sealed class NativeOperandStack : IEnumerable<int>
{
    private readonly Stack<NativeScalarValue> values = new();
    public int Count => values.Count;
    public bool HasUncertainty => values.Any(value => !value.IsExact);

    public void Push(int value)
    {
        values.Push(NativeScalarValue.Exact(value));
    }

    public void Push(NativeScalarValue value)
    {
        value.Validate();
        values.Push(value);
    }

    public int Peek()
    {
        return values.Peek().RequireExact();
    }

    public int Pop()
    {
        int value = values.Peek().RequireExact();
        values.Pop();
        return value;
    }

    public NativeScalarValue PeekValue()
    {
        return values.Peek();
    }

    public NativeScalarValue PopValue()
    {
        return values.Pop();
    }

    public NativeScalarValue[] Capture()
    {
        return values.ToArray();
    }

    public IEnumerator<int> GetEnumerator()
    {
        return values.Select(value => value.RequireExact()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
