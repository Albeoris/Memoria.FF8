namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusCureItemProfile(ushort Effect, uint Entry, uint Initializer, uint Setup, uint RootCallback, uint ChildCallback, uint DrawCallback, uint TextureGlobal, uint FilenameAddress, string TextureName, uint SoundAddress, uint MainDescriptor, uint MainBank, uint ChildDescriptor, ushort ChildStride, int WorkspaceBytes, uint SourceAddress, int PrimaryBufferOffset, int SecondaryBufferOffset)
{
    public int SourceLength => checked((int)(SoundAddress - SourceAddress));

    public static NativeBattleStatusCureItemProfile ForEffect(ushort effect)
    {
        return effect switch
        {
            292 => new(292, 0x674D90, 0x674DA0, 0x674DC0, 0x674E30, 0x674F60, 0x674FD0, 0x2507AE8, 0x10673F4, "mag291.tim", 0x10673F0, 0x2507B08, 0x2507B18, 0x2507AF8, 0x3B4, 0x3A0, 0x1065140, 0x6DC, 0x106DC),
            293 => new(293, 0x674870, 0x674880, 0x6748A0, 0x674910, 0x674A30, 0x674AA0, 0x2507A98, 0x1065134, "mag292.tim", 0x1065130, 0x2507AB8, 0x2507AC8, 0x2507AA8, 0x4E4, 0x4D0, 0x10637AC, 0x4EC, 0x104EC),
            294 => new(294, 0x6743F0, 0x674400, 0x674420, 0x674490, 0x6745C0, 0x674630, 0x2507A48, 0x10637A0, "mag293.tim", 0x106379C, 0x2507A68, 0x2507A78, 0x2507A58, 0x8B4, 0x8A0, 0x1060C50, 0xA34, 0x10A34),
            295 => new(295, 0x673E70, 0x673E80, 0x673EA0, 0x673F10, 0x674030, 0x6740A0, 0x25079D8, 0x1060C44, "mag294.tim", 0x1060C40, 0x25079F8, 0x2507A08, 0x25079E8, 0x9E0, 0x9CC, 0x105DAC0, 0x9E8, 0x109E8),
            296 => new(296, 0x673970, 0x673980, 0x6739A0, 0x673A10, 0x673B60, 0x673BD0, 0x2507988, 0x105DAB4, "mag295.tim", 0x105DAB0, 0x25079A8, 0x25079B8, 0x2507998, 0x29C, 0x288, 0x105BAA4, 0x65C, 0x1065C),
            _ => throw new NotSupportedException("Native status-cure item requires its original292..296 profile.")
        };
    }
}
