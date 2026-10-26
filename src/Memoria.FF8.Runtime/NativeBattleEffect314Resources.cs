using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect314Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableArray<NativeBattleModuleResource> ProjectedQuads, ImmutableDictionary<uint, ImmutableArray<byte>> Programs)
{
    public ImmutableArray<NativeBattleEffectFileAsset> CompletionFiles { get; init; } = [];
    public ImmutableArray<byte> Sound { get; init; } = [];

    public NativeBattleModuleResource ReadProjectedQuads(int index) => (uint)index < ProjectedQuads.Length ? ProjectedQuads[index] : throw new NotSupportedException("Effect314 reached an unsupported projected source selector.");

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in Fronts())
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException("Effect314 static read leaves its selected original resources.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag313.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 199712 || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != 268 || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != 294 || Sound.IsDefault || Sound.Length != 4 || ProjectedQuads.IsDefault || ProjectedQuads.Length != 4 || Programs is null || Programs.Count != ProgramAddresses.Length)
        {
            throw new InvalidDataException("Effect314 requires its original texture, camera, vibration, projected sources and particle programs.");
        }
        if (CompletionFiles.IsDefault || CompletionFiles.Length != 2 || CompletionFiles[0].FileId != 0x168 || CompletionFiles[1].FileId != 0x169 || CompletionFiles.Any(file => string.IsNullOrWhiteSpace(file.Name) || file.Data.IsDefaultOrEmpty))
        {
            throw new InvalidDataException("Effect314 requires its actual ordered completion files.");
        }
        if (Dword(MainTim.Data, 0) != 16 || Dword(MainTim.Data, 4) != 9 || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != 268 || Dword(Vibration.Data, 0) != 0 || Dword(Vibration.Data, 4) != 256 || Word(Vibration.Data, 256) != 32 || Word(Vibration.Data, 258) != 0)
        {
            throw new InvalidDataException("Effect314 resource headers differ from their current consumer contracts.");
        }
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModuleResource source = ProjectedQuads[index];
            if (source.Address != QuadAddresses[index] || source.Bytes.IsDefault || source.Bytes.Length != QuadLengths[index] || Word(source.Bytes, 8) != QuadMaximumLife[index] + 1)
            {
                throw new InvalidDataException("Effect314 projected source leaves its selected life/frame domain.");
            }
            for (int life = QuadMaximumLife[index]; life > 0; life--)
            {
                int offset = Word(source.Bytes, 8 + 2 * (QuadMaximumLife[index] + 1 - life));
                if (offset > source.Bytes.Length - 4 || Dword(source.Bytes, offset) != 1 || offset > source.Bytes.Length - 24)
                {
                    throw new InvalidDataException("Effect314 projected frame leaves its original single-quad payload.");
                }
            }
        }
        foreach (uint address in ProgramAddresses)
        {
            if (!Programs.TryGetValue(address, out ImmutableArray<byte> program) || program.IsDefault || program.Length < 4 || Word(program, 0) >= 56)
            {
                throw new InvalidDataException("Effect314 particle program lacks its actual selected bone owner.");
            }
            int cursor = 2;
            while (cursor < program.Length)
            {
                int opcode = Word(program, cursor);
                cursor += opcode switch
                {
                    0x1002 or 0x1003 or 0x1012 => 8,
                    0x1004 or 0x1014 or 0x10FF => 2,
                    < 0x1000 => 2,
                    _ => throw new NotSupportedException("Effect314 particle program reaches an unsupported command.")
                };
                if (opcode == 0x10FF)
                {
                    break;
                }
            }
            if (cursor != program.Length || Word(program, program.Length - 2) != 0x10FF)
            {
                throw new InvalidDataException("Effect314 particle program does not end at its actual terminal command.");
            }
        }
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Bytes)> Fronts()
    {
        yield return (CameraAddress, Camera.Data);
        yield return (VibrationAddress, Vibration.Data);
        yield return (SoundAddress, Sound);
        foreach (NativeBattleModuleResource source in ProjectedQuads)
        {
            yield return (source.Address, source.Bytes);
        }
        foreach ((uint address, ImmutableArray<byte> bytes) in Programs)
        {
            yield return (address, bytes);
        }
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
