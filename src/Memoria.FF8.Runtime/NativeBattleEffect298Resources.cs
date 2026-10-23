using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect298VibrationResource(uint Address, NativeVibrationBank Bank);

public sealed record NativeBattleEffect298Resources(NativeBattleGfAnimationSource ProgrammeA, NativeBattleGfAnimationSource ProgrammeB, NativeBattleGfAnimationSource ProgrammeC, NativeBattleGfAnimationSource ProgrammeD, ImmutableArray<NativeBattleStaticMeshResource> DebrisMeshes, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleEffectHeapAllocation Camera, uint Sound, NativeBattleEffect298VibrationResource Vibration, NativeBattleEffectFileAsset CompletionFile, ImmutableArray<byte> ScriptBytes, ImmutableArray<byte> ProgrammeTable32, ImmutableArray<byte> SpriteTable16, ImmutableArray<byte> TrigPairs16384, ImmutableArray<byte> NormalizationConsumed384)
{
    public void Validate()
    {
        RequireProgramme(ProgrammeA, ProgrammeAAddress, ProgrammeALength, ProgrammeAWorkspaceLength, 1, 22);
        RequireProgramme(ProgrammeB, ProgrammeBAddress, ProgrammeBLength, ProgrammeBWorkspaceLength, 3, 22);
        RequireProgramme(ProgrammeC, ProgrammeCAddress, ProgrammeCLength, ProgrammeCWorkspaceLength, 4, 18);
        RequireProgramme(ProgrammeD, ProgrammeDAddress, ProgrammeDLength, ProgrammeDWorkspaceLength, 4, 18);
        if (Camera is null || Camera.Identity != "FF8_EN:1052298" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 24 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x64230)
        {
            throw new InvalidDataException("Effect298 requires its complete original camera and positioned sound descriptor.");
        }
        if (ScriptBytes.IsDefault || ScriptBytes.Length != ScriptLength || ProgrammeTable32.IsDefault || ProgrammeTable32.Length != 32 || SpriteTable16.IsDefault || SpriteTable16.Length != 16 || TrigPairs16384.IsDefault || TrigPairs16384.Length != TrigLength || NormalizationConsumed384.IsDefault || NormalizationConsumed384.Length != NormalizationLength || DebrisMeshes.IsDefault || DebrisMeshes.Length != 3 || Sprites.IsDefault || Sprites.Length != 4)
        {
            throw new InvalidDataException("Effect298 requires complete selected tables, scripts, meshes and lookup fronts.");
        }
        var sources = new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD };
        int[] workspaceLengths = [ProgrammeAWorkspaceLength, ProgrammeBWorkspaceLength, ProgrammeCWorkspaceLength, ProgrammeDWorkspaceLength];
        for (int index = 0; index < sources.Length; index++)
        {
            if (Dword(ProgrammeTable32, index * 8) != sources[index].Address || Dword(ProgrammeTable32, index * 8 + 4) != workspaceLengths[index] || Dword(SpriteTable16, index * 4) != SpriteAddresses[index])
            {
                throw new InvalidDataException("Effect298 resource tables disagree with their addressed sources.");
            }
            RequireSprite(Sprites[index], SpriteAddresses[index], SpriteLengths[index], SpriteFrames[index], index == 1 ? (byte)8 : (byte)6);
        }
        for (int index = 0; index < DebrisMeshes.Length; index++)
        {
            ValidateDebris(DebrisMeshes[index], MeshAddresses[index]);
        }
        foreach (uint address in ScriptCounterAddresses)
        {
            int offset = checked((int)(address - ScriptAddress));
            if (Word(ScriptBytes, offset - 2) != 0x10F0 || Word(ScriptBytes, offset) != Word(ScriptBytes, offset + 2))
            {
                throw new InvalidDataException("Effect298 pristine script counters require their actual loop and reset WORDs.");
            }
        }
        if (Vibration is null || Vibration.Address != VibrationAddress || Vibration.Bank is null || Vibration.Bank.Bytes.IsDefault || Vibration.Bank.Bytes.Length != VibrationLength || Vibration.Bank.Offsets.IsDefault || !Vibration.Bank.Offsets.SequenceEqual(new[] { 0, 256 }))
        {
            throw new InvalidDataException("Effect298 requires its complete selected vibration bank.");
        }
        Vibration.Bank.Validate();
        if (Vibration.Bank.ReadWord(256) != 44 || Vibration.Bank.ReadWord(258) != 0)
        {
            throw new InvalidDataException("Effect298 vibration channel lengths changed.");
        }
        ValidateCompletionFile();
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        ProgrammeAAddress => ProgrammeA,
        ProgrammeBAddress => ProgrammeB,
        ProgrammeCAddress => ProgrammeC,
        ProgrammeDAddress => ProgrammeD,
        _ => throw new NotSupportedException("Effect298 selected an unsupported original programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                uint vertices = Dword(source.Data, offset + 4);
                uint shapes = Dword(source.Data, offset + 8);
                int polygon = checked((int)Dword(source.Data, offset));
                if (vertices is 0 or > 64 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Effect298 requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [14, 18, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (2 or 3 or 4 or 5 or 7))
                    {
                        throw new NotSupportedException("Effect298 selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Effect298 geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Effect298 face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect298 selected an unsupported addressed geometry front.");
    }

    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.SingleOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Effect298 selected an unsupported projected sprite.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => DebrisMeshes.SingleOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Effect298 selected an unsupported debris mesh.");

    public ImmutableArray<byte> ReadScript(uint address)
    {
        int length = address switch
        {
            0x1056560 => 500,
            0x1056754 or 0x10567E8 => 148,
            _ => throw new NotSupportedException("Effect298 selected an unsupported script front.")
        };
        return ReadStatic(address, length);
    }

    public ushort ReadNormalizationWord(int index)
    {
        if (index is < 64 or > 255)
        {
            throw new NotSupportedException("Effect298 normalization index leaves the consumed nonzero domain.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(NormalizationConsumed384.AsSpan().Slice((index - 64) * 2, 2));
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect298 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect298 static read leaves its selected immutable fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        foreach (var source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            yield return (source.Address, source.Data);
        }
        foreach (var sprite in Sprites)
        {
            yield return (sprite.Address, sprite.Bytes);
        }
        foreach (var mesh in DebrisMeshes)
        {
            yield return (mesh.Address, mesh.Bytes);
        }
        yield return (CameraAddress, Camera.Data);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (VibrationAddress, Vibration.Bank.Bytes);
        yield return (ScriptAddress, ScriptBytes);
        yield return (ProgrammeTableAddress, ProgrammeTable32);
        yield return (SpriteTableAddress, SpriteTable16);
        yield return (TrigAddress, TrigPairs16384);
        yield return (NormalizationAddress, NormalizationConsumed384);
    }

    private static void ValidateDebris(NativeBattleStaticMeshResource mesh, uint address)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != 248 || Dword(mesh.Bytes, 0) != 72 || Dword(mesh.Bytes, 4) != 8 || Dword(mesh.Bytes, 72) != 12)
        {
            throw new InvalidDataException("Effect298 requires its complete eight-vertex debris front.");
        }
        for (int face = 0; face < 12; face++)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                uint offset = (uint)Word(mesh.Bytes, 76 + face * 12 + 4 + corner * 2) * 4;
                if ((offset & 7) != 0 || offset >= 64)
                {
                    throw new InvalidDataException("Effect298 debris face leaves its selected vertex bank.");
                }
            }
        }
        for (int group = 1; group < 8; group++)
        {
            if (Dword(mesh.Bytes, 220 + (group - 1) * 4) != 0)
            {
                throw new InvalidDataException("Effect298 debris selected an unsupported face group.");
            }
        }
    }

    private void ValidateCompletionFile()
    {
        if (CompletionFile is null || CompletionFile.FileId != CompletionFileId || !string.Equals(CompletionFile.Name, "MA8DEF_0.TIM", StringComparison.OrdinalIgnoreCase) || CompletionFile.Data.IsDefault || CompletionFile.Data.Length != 33824 || Dword(CompletionFile.Data, 0) != 16 || Dword(CompletionFile.Data, 4) != 8)
        {
            throw new InvalidDataException("Effect298 completion requires its selected original indexed texture.");
        }
        if (Dword(CompletionFile.Data, 8) != 1036 || Dword(CompletionFile.Data, 1044) != 32780 || Word(CompletionFile.Data, 12) != 624 || Word(CompletionFile.Data, 14) != 224 || Word(CompletionFile.Data, 16) != 16 || Word(CompletionFile.Data, 18) != 32 || Word(CompletionFile.Data, 1048) != 704 || Word(CompletionFile.Data, 1050) != 256 || Word(CompletionFile.Data, 1052) != 64 || Word(CompletionFile.Data, 1054) != 256)
        {
            throw new InvalidDataException("Effect298 completion texture rectangles changed.");
        }
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int frames, byte kind)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || Dword(sprite.Bytes, 0) != ((uint)kind << 24 | 0x010524) || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Effect298 requires its original selected projected sprite front.");
        }
        for (int frame = 0; frame < frames; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != (address == ProgrammeAAddress ? 1u : 2u))
        {
            throw new InvalidDataException("Effect298 requires complete original addressed programme sources.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Effect298 requires its original selected node counts and programme durations.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 2)
        {
            throw new InvalidDataException("Effect298 WORD read leaves its complete resource.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Effect298 resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
