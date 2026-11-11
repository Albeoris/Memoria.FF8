using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleActorModelResources(Ff7BattleActorDeathResources Death, IReadOnlyList<byte> PlayerHurtActions);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7BattleActorModelResources LoadBattleActorModelResources(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] values = ReadVirtual(stream, pe, 0x8FE1F6, 15 * sizeof(short));
        short[] lift = new short[15];
        for (int index = 0; index < lift.Length; index++)
        {
            lift[index] = BinaryPrimitives.ReadInt16LittleEndian(values.AsSpan(index * sizeof(short)));
        }
        Ff7BattleActorDeathResources death = new(BitConverter.ToSingle(ReadVirtual(stream, pe, 0x7B7680, sizeof(float))), Array.AsReadOnly(lift));
        return new(death, Array.AsReadOnly(ReadVirtual(stream, pe, 0x7B76C0, 5)));
    }
}
