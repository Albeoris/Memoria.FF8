namespace Memoria.FF8.GameUi;

/// <summary>Combines ordinary host text events into committed Unicode text.</summary>
public sealed class LocalizationAuthoringTextInput : IDisposable
{
    private readonly Action<string> commit;
    private char? highSurrogate;
    private bool disposed;
    public LocalizationAuthoringTextInput(Action<string> committedText)
    {
        commit = committedText;
    }
    public void ReceiveCharacter(char character)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ProcessCharacter(character);
    }
    private void ProcessCharacter(char character, Action<string>? output = null, bool allowLineBreaks = false)
    {
        Action<string> emit = output ?? Emit;
        if (char.IsControl(character) && !(allowLineBreaks && character is '\r' or '\n'))
        {
            return;
        }
        if (char.IsHighSurrogate(character))
        {
            if (highSurrogate is not null)
            {
                emit("\uFFFD");
            }
            highSurrogate = character;
            return;
        }
        if (char.IsLowSurrogate(character))
        {
            string value = highSurrogate is char high ? new string([high, character]) : "\uFFFD";
            highSurrogate = null;
            emit(value);
            return;
        }
        if (highSurrogate is not null)
        {
            highSurrogate = null;
            emit("\uFFFD");
        }
        emit(character.ToString());
    }
    public void ReceiveText(string value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        System.Text.StringBuilder batch = new();
        foreach (char character in value)
        {
            ProcessCharacter(character, text => batch.Append(text), allowLineBreaks: true);
        }
        if (batch.Length != 0)
        {
            Emit(batch.ToString());
        }
    }
    private void Emit(string value)
    {
        commit(value);
    }
    public void Reset()
    {
        highSurrogate = null;
    }
    public void Dispose()
    {
        disposed = true;
        highSurrogate = null;
    }
}
