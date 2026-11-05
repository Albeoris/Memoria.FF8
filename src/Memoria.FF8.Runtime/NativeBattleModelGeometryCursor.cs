namespace Memoria.FF8.Runtime;

/// <summary>A call-scoped source coordinate. Numeric projection is available only for an actual guest/shared owner.</summary>
internal readonly struct NativeBattleModelGeometryCursor
{
    private readonly NativeBattleModelGeometrySource source;
    internal int Offset { get; }
    internal long Identity => source.PresentationIdentity;
    internal uint PresentationCursor => source.NumericCursor(Offset) ?? unchecked((uint)Offset);
    internal uint? NumericAddress => source.NumericCursor(Offset);

    internal NativeBattleModelGeometryCursor(NativeBattleModelGeometrySource source, int offset = 0)
    {
        _ = source.NumericCursor(offset);
        this.source = source;
        Offset = offset;
    }

    internal NativeBattleEffectArena Read(int length) => source.ReadArena(Offset, length);
    internal NativeBattleEffectArena Write(NativeBattleEffectArena destination, int field) => source.WriteCursor(destination, field, Offset);

    internal NativeBattleModelGeometryCursor Align4()
    {
        long aligned = ((long)Offset + source.AlignmentResidue + 3) & ~3L;
        return new(source, checked((int)(aligned - source.AlignmentResidue)));
    }

    internal NativeBattleModelGeometryCursor ReadCursor(NativeBattleEffectArena memory, int field)
    {
        long relative;
        if (memory.TryGetResourcePointer(field, out NativeBattleResourcePointer? pointer))
        {
            if (pointer?.Cpu is { } reference && source.Owner.Cpu is { } owner && reference.Bank == owner.Bank && reference.ResourceOnly == owner.ResourceOnly && reference.EffectiveLayout == owner.EffectiveLayout)
            {
                relative = (long)reference.Offset - owner.Offset;
            }
            else if ((pointer?.Cpu?.TryPhysicalAddress() ?? pointer?.StagingAddress) is uint address && source.NumericCursor(0) is uint basis)
            {
                relative = (long)address - basis;
            }
            else
            {
                throw new InvalidDataException("Model source cursor belongs to another owner or address domain.");
            }
        }
        else
        {
            uint basis = source.NumericCursor(0) ?? throw new NotSupportedException("Logical model source cursor requires its retained pointer metadata.");
            relative = (long)memory.ReadUInt32(field) - basis;
        }
        return new(source, checked((int)relative));
    }

    public static NativeBattleModelGeometryCursor operator +(NativeBattleModelGeometryCursor cursor, int count) => new(cursor.source, checked(cursor.Offset + count));
    public static NativeBattleModelGeometryCursor operator +(NativeBattleModelGeometryCursor cursor, uint count) => new(cursor.source, checked(cursor.Offset + (int)count));
    public static NativeBattleModelGeometryCursor operator ++(NativeBattleModelGeometryCursor cursor) => cursor + 1;
    public static bool operator <(NativeBattleModelGeometryCursor left, NativeBattleModelGeometryCursor right) => Compare(left, right) < 0;
    public static bool operator >(NativeBattleModelGeometryCursor left, NativeBattleModelGeometryCursor right) => Compare(left, right) > 0;

    private static int Compare(NativeBattleModelGeometryCursor left, NativeBattleModelGeometryCursor right)
    {
        if (!ReferenceEquals(left.source.Owner, right.source.Owner))
        {
            throw new InvalidDataException("Model source cursor comparison crosses its retained call scope.");
        }
        return left.Offset.CompareTo(right.Offset);
    }
}
