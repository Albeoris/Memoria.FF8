using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private NativeRagnarokTransferCamera? ragnarokLandingCameraTargets;
    private NativeRagnarokTransferCamera? ragnarokTakeoffCameraTargets;

    public NativeRagnarokTransferCamera ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind kind, int groundHeight, short bodyYaw = 0, byte view = 0)
    {
        if (kind is not (NativeRagnarokTransferKind.Landing or NativeRagnarokTransferKind.Takeoff) || view != 0)
        {
            throw new NotSupportedException("Ragnarok transfer targets currently represent original foot128 view0 and controlled50 only.");
        }
        bool takeoff = kind == NativeRagnarokTransferKind.Takeoff;
        int targetHeight = unchecked(groundHeight + ((-3584 - groundHeight) >> 1));
        NativeRagnarokTransferCamera? retained = takeoff ? ragnarokTakeoffCameraTargets : ragnarokLandingCameraTargets;
        if (retained is not null)
        {
            return retained with { Pitch = takeoff ? unchecked((short)(-256 - targetHeight / 24)) : retained.Pitch, Yaw = bodyYaw };
        }
        byte[] executable = contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new InvalidDataException("Ragnarok transfer camera targets require the authenticated original executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        short[] ReadWords(uint address, int count)
        {
            int rva = checked((int)(address - reader.PEHeaders.PEHeader!.ImageBase));
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + count * 2 <= section.VirtualAddress + section.SizeOfRawData);
            short[] result = new short[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = BinaryPrimitives.ReadInt16LittleEndian(executable.AsSpan(rva - section.VirtualAddress + section.PointerToRawData + index * 2));
            }
            return result;
        }
        short pitch = takeoff ? unchecked((short)(-256 - targetHeight / 24)) : ReadWords(0xC76D28, 1)[0];
        short offsetZ = ReadWords(takeoff ? 0xC76D3Cu : 0xC76D34u, 1)[0];
        var camera = new NativeRagnarokTransferCamera(1024, takeoff ? 6144 : 5216, pitch, bodyYaw, offsetZ, ReadWords(takeoff ? 0xC767D0u : 0xC767DCu, 5).ToImmutableArray(), ReadWords(takeoff ? 0xC767E8u : 0xC767F0u, 4).ToImmutableArray());
        camera.Validate();
        if (takeoff)
        {
            ragnarokTakeoffCameraTargets = camera;
        }
        else
        {
            ragnarokLandingCameraTargets = camera;
        }
        return camera;
    }
}
