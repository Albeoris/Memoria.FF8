using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect338ParticleBank([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] int? NextOffset)
{
    public const int Stride = 24;
    public const int Capacity = 64;
    public const int Length = Stride * Capacity;

    public static NativeBattleEffect338ParticleBank FromColdProcess() => new(NativeBattleEffectArena.Clear(), null);
    public static NativeBattleEffect338ParticleBank FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null);

    public void Validate()
    {
        Storage.Validate();
        if (Storage.Regions.Any(region => region.Offset < 0 || region.Offset > Length - region.Data.Length) || Storage.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Length - region.Length) || NextOffset is int next && (next < 0 || next >= Length || next % Stride != 0))
        {
            throw new InvalidDataException("Effect338 particle storage or allocation cursor leaves its original bank.");
        }
    }

    public NativeBattleEffect338ParticleBank Reset6EC0C0()
    {
        NativeBattleEffect338ParticleBank bank = this;
        for (int slot = 0; slot < Capacity; slot++)
        {
            bank = bank.WriteWord(slot * Stride + 10, 0);
        }
        return bank with { NextOffset = 0 };
    }

    public (NativeBattleEffect338ParticleBank Bank, int? Offset) Allocate6EC980()
    {
        Validate();
        int next = NextOffset ?? throw new NotSupportedException("Effect338 particle allocation requires its executed reset cursor.");
        int selected = next;
        if (ReadWord(selected + 10) != 0)
        {
            selected = 0;
            while (selected < Length && ReadWord(selected + 10) != 0)
            {
                selected += Stride;
            }
            if (selected == Length)
            {
                return (this, null);
            }
        }
        return (this with { NextOffset = selected < Length - Stride ? selected + Stride : 0 }, selected);
    }

    public NativeBattleEffect338ParticleBank Advance6EC9D0()
    {
        NativeBattleEffect338ParticleBank bank = this;
        for (int row = 0; row < Capacity; row++)
        {
            int offset = row * Stride;
            ushort life = bank.ReadWord(offset + 10);
            if (life == 0)
            {
                continue;
            }
            ushort xVelocity = bank.ReadWord(offset + 12);
            ushort yVelocity = bank.ReadWord(offset + 14);
            bank = bank.WriteWord(offset + 4, unchecked((ushort)(bank.ReadWord(offset + 4) + xVelocity)));
            bank = bank.WriteWord(offset + 6, unchecked((ushort)(bank.ReadWord(offset + 6) + yVelocity)));
            bank = bank.WriteWord(offset + 12, unchecked((ushort)(xVelocity + unchecked((sbyte)bank.ReadByte(offset + 20)) * 4)));
            bank = bank.WriteWord(offset + 14, unchecked((ushort)(yVelocity + unchecked((sbyte)bank.ReadByte(offset + 21)) * 4)));
            ushort zVelocity = unchecked((ushort)(bank.ReadWord(offset + 16) + unchecked((sbyte)bank.ReadByte(offset + 22)) * 4));
            bank = bank.WriteWord(offset + 8, unchecked((ushort)(bank.ReadWord(offset + 8) + zVelocity)));
            bank = bank.WriteWord(offset + 10, unchecked((ushort)(life - 1)));
        }
        return bank;
    }

    public byte ReadByte(int offset) => Storage.Read(Require(offset, 1), 1)[0];
    public ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Storage.Read(Require(offset, 2), 2));
    public uint ReadDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Storage.Read(Require(offset, 4), 4));
    public NativeBattleEffect338ParticleBank WriteByte(int offset, byte value) => this with { Storage = Storage.Write(Require(offset, 1), [value]) };
    public NativeBattleEffect338ParticleBank WriteWord(int offset, ushort value) => this with { Storage = Storage.Write(Require(offset, 2), BitConverter.GetBytes(value)) };
    public NativeBattleEffect338ParticleBank WriteDword(int offset, uint value) => this with { Storage = Storage.Write(Require(offset, 4), BitConverter.GetBytes(value)) };

    private static int Require(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > Length - length)
        {
            throw new InvalidDataException("Effect338 particle access leaves its original physical bank.");
        }
        return offset;
    }
}
