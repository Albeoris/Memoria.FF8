using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

/// <summary>Projects native field saves; exact VM continuations belong to the Memoria session codec.</summary>
public static class Ff7NativeSaveCodec
{
    public static Ff7SessionSave Import(Ff7NativeSaveSlot slot, Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(installation);
        if (slot.IsEmpty || !slot.ChecksumValid)
        {
            throw new InvalidDataException("The selected native FFVII slot is empty or has an invalid checksum.");
        }
        if (slot.Module != 1)
        {
            throw new NotSupportedException("Native FFVII import currently supports field saves; this save requires another game module.");
        }
        return Ff7Session.ImportNativeField(slot, installation);
    }

    public static Ff7NativeSaveSlot Export(Ff7SessionSave save, Ff7Installation installation, Ff7NativeSaveSlot? original = null)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(installation);
        save.Validate();
        if (save.ResourceProfile != installation.Layout.ToString() || save.Language != installation.Language)
        {
            throw new InvalidDataException("The FFVII save requires a different resource profile or language.");
        }
        if (save.World is not null || save.Battle is not null || save.TickContinuation is not null)
        {
            throw new NotSupportedException("Native FFVII export requires a completed field update outside world-map and battle continuations.");
        }
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        LgpEntry entry = fields.Find("maplist").Single();
        string[] maps = PcFieldMapList.Read(fields.ReadEntry(entry)).ToArray();
        int map = Array.IndexOf(maps, save.FieldName);
        if (map < 0 || map > ushort.MaxValue)
        {
            throw new InvalidDataException("The native FFVII save field is absent from the installed maplist.");
        }
        Ff7Actor actor = save.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) ?? throw new NotSupportedException("Native FFVII export requires a controlled field actor.");
        return ExportField(save.Progress, save.Vm.PersistentVariables, checked((ushort)map), checked((short)(actor.X4096 >> 12)), checked((short)(actor.Y4096 >> 12)), checked((ushort)actor.Triangle), unchecked((byte)actor.Direction), original?.Encode() ?? save.NativeSaveData);
    }

    public static Ff7Progress ImportProgress(Ff7NativeSaveSlot slot, byte[] initialKernelData)
    {
        if (slot.IsEmpty || !slot.ChecksumValid)
        {
            throw new InvalidDataException("The selected native FFVII slot is empty or has an invalid checksum.");
        }
        byte[] data = slot.Encode();
        KernelInitialData native = KernelInitialData.Read(data.AsSpan(0x54, 2876));
        Ff7Progress progress = new()
        {
            InitialKernelData = initialKernelData.ToArray(),
            Characters = native.Characters.Select((character, index) => new Ff7CharacterState(index, Ff7NativeSaveSlot.ReadText(character.EncodedName.Span), character.Level, character.Experience, character.CurrentHp, character.CurrentMp, character.Raw.ToArray())).ToArray(),
            Party = native.Party.Select(value => (int)value).ToArray(),
            Inventory = native.Inventory.Where(item => item.Encoded != ushort.MaxValue && item.Quantity > 0).Select(item => new Ff7InventorySlot(item.ItemId, item.Quantity)).ToArray(),
            Materia = native.Materia.Select((materia, index) => (materia, index)).Where(value => value.materia.MateriaId != 255).Select(value => new Ff7MateriaState(value.materia.MateriaId, value.materia.AbilityPoints, value.index)).ToArray(),
            Gil = native.Gil,
            Clock = new(Read32(data, 0xB80), Read32(data, 0xB84), Read16(data, 0xB88), Read16(data, 0xB8C)),
            BattleEscapes = Read16(data, 0xBBE),
            LockedCharacters = Read16(data, 0x10A4),
            AvailableCharacters = Read16(data, 0x10A6),
            NativeMessageSpeed = data[0x10EC],
            Encounters = new(RollIndex: data[0xBA1], RollOffset: data[0xBA2])
        };
        progress.Validate();
        return progress;
    }

    public static Ff7NativeSaveSlot ExportField(Ff7Progress progress, byte[] persistentVariables, ushort map, short x, short y, ushort triangle, byte direction, byte[]? originalData = null)
    {
        progress.Validate();
        if (persistentVariables.Length != 1280 || originalData is not null && originalData.Length != Ff7NativeSaveSlot.Size)
        {
            throw new InvalidDataException("Invalid native FFVII save projection storage.");
        }
        byte[] data = originalData?.ToArray() ?? new byte[Ff7NativeSaveSlot.Size];
        if (originalData is null)
        {
            if (progress.InitialKernelData.Length != 2876)
            {
                throw new InvalidDataException("Native FFVII export requires the original initial game-data layout.");
            }
            progress.InitialKernelData.CopyTo(data, 0x54);
            data.AsSpan(0xA9C, 224).Fill(255);
            data.AsSpan(0x48, 12).Fill(128);
            data[0x4FB] = 255;
            data[0x10D8] = 128;
            data[0x10D9] = 128;
            data[0x10DA] = 1;
        }
        foreach (Ff7CharacterState character in progress.Characters)
        {
            Span<byte> record = data.AsSpan(0x54 + character.Id * 132, 132);
            character.NativeRecord.CopyTo(record);
            record[1] = checked((byte)character.Level);
            BinaryPrimitives.WriteUInt16LittleEndian(record[44..], checked((ushort)character.HitPoints));
            BinaryPrimitives.WriteUInt16LittleEndian(record[48..], checked((ushort)character.MagicPoints));
            BinaryPrimitives.WriteUInt32LittleEndian(record[60..], character.Experience);
        }
        for (int index = 0; index < 3; index++)
        {
            data[0x4F8 + index] = checked((byte)progress.Party[index]);
            data[5 + index] = checked((byte)progress.Party[index]);
        }
        data.AsSpan(0x4FC, 640).Fill(255);
        for (int index = 0; index < progress.Inventory.Length; index++)
        {
            Ff7InventorySlot item = progress.Inventory[index];
            Write16(data, 0x4FC + index * 2, (ushort)(item.ItemId | item.Quantity << 9));
        }
        data.AsSpan(0x77C, 800).Fill(255);
        foreach (Ff7MateriaState materia in Ff7NativeMateriaInventory.Normalize(progress.Materia))
        {
            Write32(data, 0x77C + materia.NativeSlot!.Value * 4, (uint)materia.MateriaId | materia.AbilityPoints << 8);
        }
        persistentVariables.CopyTo(data, 0xBA4);
        Write32(data, 0xB7C, progress.Gil);
        Write32(data, 0x20, progress.Gil);
        Ff7ClockState clock = progress.Clock ?? Ff7NativeClock.RestoreRecorded(new(persistentVariables, new byte[256]));
        Write32(data, 0xB80, clock.ElapsedSeconds);
        Write32(data, 0x24, clock.ElapsedSeconds);
        Write32(data, 0xB84, clock.CountdownSeconds);
        Write32(data, 0xB88, clock.ElapsedPhase);
        Write32(data, 0xB8C, clock.CountdownPhase);
        Write32(data, 0xB90, 2);
        Write16(data, 0xB94, 1);
        Write16(data, 0xB96, map);
        Write16(data, 0xB9A, unchecked((ushort)x));
        Write16(data, 0xB9C, unchecked((ushort)y));
        Write16(data, 0xB9E, triangle);
        data[0xBA0] = direction;
        data[0xBA1] = progress.Encounters.RollIndex;
        data[0xBA2] = progress.Encounters.RollOffset;
        Write16(data, 0xBBE, progress.BattleEscapes);
        Write16(data, 0x10A4, progress.LockedCharacters);
        Write16(data, 0x10A6, progress.AvailableCharacters);
        data[0x10EC] = progress.NativeMessageSpeed;
        Ff7CharacterState? leader = progress.Characters.FirstOrDefault(character => character.Id == progress.Party[0]);
        if (leader is not null)
        {
            ReadOnlySpan<byte> record = data.AsSpan(0x54 + leader.Id * 132, 132);
            data[4] = record[1];
            data.AsSpan(8, 16).Fill(255);
            record.Slice(16, 12).CopyTo(data.AsSpan(8));
            record.Slice(44, 8).CopyTo(data.AsSpan(0x18));
        }
        data.AsSpan(0x28, 32).Fill(255);
        data.AsSpan(0xF0C, 24).CopyTo(data.AsSpan(0x28));
        Write16(data, 0, Ff7NativeSaveSlot.ComputeChecksum(data));
        return Ff7NativeSaveSlot.Decode(data);
    }

    private static ushort Read16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
    private static uint Read32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
    private static void Write16(byte[] data, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
    private static void Write32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
