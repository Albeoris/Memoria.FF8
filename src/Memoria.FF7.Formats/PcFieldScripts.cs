using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public sealed record PcFieldEntity(string Name, IReadOnlyList<ushort> ScriptOffsets);

public sealed class PcFieldScripts
{
    private PcFieldScripts(byte[] data, byte modelCount, short scale, ushort textOffset, string creator, string name, PcFieldEntity[] entities, uint[] akaoOffsets, ReadOnlyMemory<byte>[] texts)
    {
        Data = data;
        ModelCount = modelCount;
        Scale = scale;
        TextOffset = textOffset;
        Creator = creator;
        Name = name;
        Entities = Array.AsReadOnly(entities);
        AkaoOffsets = Array.AsReadOnly(akaoOffsets);
        Texts = Array.AsReadOnly(texts);
    }
    public ReadOnlyMemory<byte> Data { get; }
    public byte ModelCount { get; }
    public short Scale { get; }
    public ushort TextOffset { get; }
    public string Creator { get; }
    public string Name { get; }
    public IReadOnlyList<PcFieldEntity> Entities { get; }
    public IReadOnlyList<uint> AkaoOffsets { get; }
    public IReadOnlyList<ReadOnlyMemory<byte>> Texts { get; }

    public static PcFieldScripts Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        if (cursor.U16() != 0x0502)
        {
            throw new InvalidDataException("Unsupported FF7 PC script section version.");
        }
        byte entityCount = cursor.U8();
        byte models = cursor.U8();
        ushort textOffset = cursor.U16();
        ushort akaoCount = cursor.U16();
        short scale = cursor.I16();
        cursor.Skip(6);
        string creator = cursor.Ascii(8);
        string name = cursor.Ascii(8);
        string[] names = new string[entityCount];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = cursor.Ascii(8);
        }
        uint[] akao = new uint[akaoCount];
        for (int i = 0; i < akao.Length; i++)
        {
            akao[i] = cursor.U32();
            if (akao[i] > source.Length)
            {
                throw new InvalidDataException("FF7 script AKAO offset exceeds its section.");
            }
        }
        int codeStart = checked(cursor.Position + entityCount * 64);
        if (textOffset < codeStart || textOffset > source.Length - 2)
        {
            throw new InvalidDataException("FF7 script text offset overlaps its directory or exceeds its section.");
        }
        PcFieldEntity[] entities = new PcFieldEntity[entityCount];
        for (int i = 0; i < entities.Length; i++)
        {
            ushort[] entries = new ushort[32];
            for (int script = 0; script < entries.Length; script++)
            {
                entries[script] = cursor.U16();
                if (entries[script] < codeStart || entries[script] >= textOffset)
                {
                    throw new InvalidDataException($"FF7 entity {i} script {script} entry offset is outside the code region.");
                }
            }
            entities[i] = new PcFieldEntity(names[i], Array.AsReadOnly(entries));
        }
        byte[] data = source.ToArray();
        cursor.Seek(textOffset);
        ushort textCount = cursor.U16();
        if (textCount > cursor.Remaining / 2)
        {
            throw new InvalidDataException("FF7 text directory exceeds its section.");
        }
        ReadOnlyMemory<byte>[] texts = new ReadOnlyMemory<byte>[textCount];
        int minimumTextOffset = 2 + textCount * 2;
        int textEnd = akao.Where(offset => offset > textOffset).Select(offset => (int)offset).DefaultIfEmpty(source.Length).Min();
        for (int i = 0; i < texts.Length; i++)
        {
            int relative = cursor.U16();
            int start = textOffset + relative;
            if (relative < minimumTextOffset || start >= textEnd)
            {
                throw new InvalidDataException($"FF7 text {i} offset is invalid.");
            }
            int terminator = FieldText.FindTerminatedLength(source[start..]);
            texts[i] = data.AsMemory(start, terminator);
        }
        return new PcFieldScripts(data, models, scale, textOffset, creator, name, entities, akao, texts);
    }
}
