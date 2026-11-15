using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7ChocoboClass(short TopSpeed, short TopSpeedRange, short CruiseSpeed, short CruiseSpeedRange);
public sealed record Ff7ChocoboSoundEvent(short Operation, ushort[] Parameters);
public sealed record Ff7ChocoboNativeData(IReadOnlyList<Ff7ChocoboClass> Classes, IReadOnlyList<short> Angles, IReadOnlyList<byte[]> Names, byte[] SpecialName, IReadOnlyList<int> AnimationLengths, Ff7BattleCameraMath Trigonometry)
{
    public required Ff7ChocoboPresentationData LongPresentation { get; init; }
    public required Ff7ChocoboPresentationData ShortPresentation { get; init; }
    public required IReadOnlyList<Ff7ChocoboSoundEvent> LongSounds { get; init; }
    public required IReadOnlyList<Ff7ChocoboSoundEvent> ShortSounds { get; init; }
    public required IReadOnlyList<int> LongFrameCounts { get; init; }
    public required IReadOnlyList<int> ShortFrameCounts { get; init; }
}

public sealed partial class Ff7PcExecutableData
{
    public static Ff7ChocoboNativeData LoadChocoboData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] classData = ReadVirtual(stream, pe, 0x97A3E0, 32);
        Ff7ChocoboClass[] classes = new Ff7ChocoboClass[4];
        for (int index = 0; index < classes.Length; index++)
        {
            ReadOnlySpan<byte> row = classData.AsSpan(index * 8, 8);
            classes[index] = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]), BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
        }
        byte[] angleData = ReadVirtual(stream, pe, 0x97CDA0, 8193 * 2);
        short[] angles = new short[8193];
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = BinaryPrimitives.ReadInt16LittleEndian(angleData.AsSpan(index * 2));
        }
        byte[] nameData = ReadVirtual(stream, pe, 0x97CC58, 45 * 7);
        byte[][] names = new byte[45][];
        for (int index = 0; index < names.Length; index++)
        {
            names[index] = nameData.AsSpan(index * 7, 7).ToArray();
        }
        byte[] animationData = ReadVirtual(stream, pe, 0x96EBC0, 6 * 4);
        int[] animationLengths = new int[6];
        for (int index = 0; index < animationLengths.Length; index++)
        {
            animationLengths[index] = BinaryPrimitives.ReadInt32LittleEndian(animationData.AsSpan(index * 4));
        }
        Ff7ChocoboPresentationData longPresentation = LoadChocoboPresentationData(installation, false);
        Ff7ChocoboPresentationData shortPresentation = LoadChocoboPresentationData(installation, true);
        using LgpArchive archive = Ff7ChocoboResources.Open(installation);
        Dictionary<string, int> frameCounts = new(StringComparer.OrdinalIgnoreCase);
        return new(Array.AsReadOnly(classes), Array.AsReadOnly(angles), Array.AsReadOnly(names), ReadVirtual(stream, pe, 0x97C8A8, 8), Array.AsReadOnly(animationLengths), LoadBattleCameraMath(installation))
        {
            LongPresentation = longPresentation,
            ShortPresentation = shortPresentation,
            LongSounds = Sounds(0x96E5E8, 21),
            ShortSounds = Sounds(0x96E758, 18),
            LongFrameCounts = Frames(longPresentation),
            ShortFrameCounts = Frames(shortPresentation)
        };

        IReadOnlyList<int> Frames(Ff7ChocoboPresentationData presentation)
        {
            return Array.AsReadOnly(presentation.Models.Select(model =>
            {
                string clip = model.Animations[0];
                if (!frameCounts.TryGetValue(clip, out int count))
                {
                    IReadOnlyList<LgpEntry> entries = archive.Find(clip);
                    if (entries.Count != 1)
                    {
                        throw new InvalidDataException("The FFVII Chocobo model requires an unambiguous original animation.");
                    }
                    count = PcFieldAnimation.Read(archive.ReadEntry(entries[0])).Frames.Count;
                    frameCounts.Add(clip, count);
                }
                return count;
            }).ToArray());
        }

        IReadOnlyList<Ff7ChocoboSoundEvent> Sounds(uint address, int count)
        {
            byte[] bytes = ReadVirtual(stream, pe, address, count * 16);
            Ff7ChocoboSoundEvent[] result = new Ff7ChocoboSoundEvent[count];
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> row = bytes.AsSpan(index * 16, 16);
                ushort[] parameters = new ushort[6];
                for (int operand = 0; operand < parameters.Length; operand++)
                {
                    parameters[operand] = BinaryPrimitives.ReadUInt16LittleEndian(row[(2 + operand * 2)..]);
                }
                result[index] = new(BinaryPrimitives.ReadInt16LittleEndian(row), parameters);
            }
            return Array.AsReadOnly(result);
        }
    }
}
