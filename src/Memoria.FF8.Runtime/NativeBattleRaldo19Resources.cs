using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRaldo19Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource Executable, uint Sound, NativeBattleFullscreenOverlayProgram Overlay, ImmutableArray<byte> DebrisHigh, ImmutableArray<byte> DebrisLow, ImmutableArray<byte> ColdShared84, ImmutableArray<byte> ColdBallC4, ImmutableArray<byte> ColdBodyScratch, ImmutableArray<byte> ColdDescriptor48, ImmutableArray<byte> ColdVertexScratch, uint ColdCounter273048C, uint ColdCounter272DDDC)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != NativeBattleRaldo19Profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != Animation.Data.Length || Executable is null || Executable.ResourceAddress != NativeBattleRaldo19Profile.ExecutableAddress || Executable.Data.IsDefault || Executable.Data.Length != NativeBattleRaldo19Profile.ExecutableLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0 || Overlay is null || Overlay.Address != 0x162A470 || !Overlay.Commands.SequenceEqual(new uint[] { 0x404040, 0xFF000000 }))
        {
            throw new InvalidDataException("Raldo requires its complete original camera, node source and fullscreen programme.");
        }
        Sprite(DebrisHigh, 0x1629FB0, 0x110, 10);
        Sprite(DebrisLow, 0x162A0C0, 0x144, 12);
        if (ColdShared84.IsDefault || ColdShared84.Length != 84 || ColdBallC4.IsDefault || ColdBallC4.Length != 0xC4 || ColdBodyScratch.IsDefault || ColdBodyScratch.Length != NativeBattleRaldo19Profile.BodyScratchLength || ColdDescriptor48.IsDefault || ColdDescriptor48.Length != 48 || ColdVertexScratch.IsDefault || ColdVertexScratch.Length != NativeBattleRaldo19Profile.VertexScratchLength)
        {
            throw new InvalidDataException("Raldo requires its actual loader bytes for every retained static block.");
        }
    }

    public NativeBattleCuragaResources GenericNodes()
    {
        return new(Animation, Sound) { Effect = 19, Executable = Executable };
    }

    public NativeBattleModuleResource Debris(uint address)
    {
        return new(0, address, 0, 0, address switch
        {
            0x1629FB0 => DebrisHigh,
            0x162A0C0 => DebrisLow,
            _ => throw new NotSupportedException("Raldo debris leaves its actual resource directory.")
        });
    }

    private static void Sprite(ImmutableArray<byte> bytes, uint address, int length, int count)
    {
        if (bytes.IsDefault || bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Raldo requires its complete original debris frames.");
        }
        NativeBattleModuleResource resource = new(0, address, 0, 0, bytes);
        for (int frame = 0; frame < count; frame++)
        {
            _ = resource.ProjectedFrame571C80(frame);
        }
    }
}
