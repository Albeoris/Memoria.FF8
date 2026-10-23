using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShotgun281Resources(NativeBattleGfAnimationSource ProgrammeA, NativeBattleGfAnimationSource ProgrammeB, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> MuzzlePoint8)
{
    public void Validate()
    {
        RequireProgramme(ProgrammeA, ProgrammeAAddress, ProgrammeALength, ProgrammeAWorkspaceLength, 6, 14);
        RequireProgramme(ProgrammeB, ProgrammeBAddress, ProgrammeBLength, ProgrammeBWorkspaceLength, 4, 32);
        if (Camera is null || Camera.Identity != "FF8_EN:10A7824" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 52 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61B74)
        {
            throw new InvalidDataException("Shotgun requires its complete original camera and positioned sound descriptor.");
        }
        if (MuzzlePoint8.IsDefault || MuzzlePoint8.Length != 8 || BinaryPrimitives.ReadInt16LittleEndian(MuzzlePoint8.AsSpan().Slice(6)) != 23)
        {
            throw new InvalidDataException("Shotgun requires its original packed muzzle point and bone selector23.");
        }
        RequireSprite(SpriteA, SpriteAAddress, SpriteALength, 8);
        RequireSprite(SpriteB, SpriteBAddress, SpriteBLength, 10);
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB })
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
        _ => throw new NotSupportedException("Shotgun selected an unsupported original programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB })
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
                if (vertices is 0 or > 100 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Shotgun requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (3 or 4 or 5 or 6 or 7))
                    {
                        throw new NotSupportedException("Shotgun selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Shotgun geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Shotgun face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Shotgun selected an unsupported addressed geometry front.");
    }

    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Shotgun selected an unsupported projected sprite.")
    };

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int frames)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || Dword(sprite.Bytes, 0) != 0x08010524 || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Shotgun requires its original kind8 projected sprite front.");
        }
        for (int frame = 0; frame < frames; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != (address == ProgrammeAAddress ? 5u : 3u))
        {
            throw new InvalidDataException("Shotgun requires both complete original addressed programme sources.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Shotgun requires its original selected node counts and fourteen/thirty-two-frame programmes.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Shotgun resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
