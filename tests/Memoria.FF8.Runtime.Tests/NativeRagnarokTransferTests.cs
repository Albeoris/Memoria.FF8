using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRagnarokTransferTests
{
    private static NativeRagnarokTransferCamera LandedCamera => new(1024, 5216, -112, 0, -7168, [2196, 2496, 2746, 2946, 4095], [1896, 2196, 2496, 2746]);
    private static NativeRagnarokTransferCamera FlyingCamera => new(1024, 6144, -176, 400, -14336, [2896, 3296, 3596, 3846, 4095], [2996, 3196, 3396, 3596]);

    [Fact]
    public void TakeoffRunsFortyOneYawCallbacksThenTwentyCameraCallbacksAndSnapsAtSixtyOne()
    {
        NativeRagnarokTransferState state = new(NativeRagnarokTransferKind.Takeoff, new(200, -300, 900), -300, 400, LandedCamera, FlyingCamera);
        NativeRagnarokTransferStep step = state.Advance();
        Assert.Equal(-327, step.Position.Y);
        Assert.Equal(307, step.TransitionPhase);
        Assert.Equal(311, step.AnimationCounter);
        Assert.Equal(120, step.InitialVerticalScale);
        Assert.Equal((short)-112, step.Camera.Pitch);
        Assert.True(step.CameraProjectionMode);
        for (int count = 2; count <= 61; count++)
        {
            step = step.State.Advance();
            Assert.Equal(count == 61, step.Completed);
            if (count == 41)
            {
                Assert.Equal((short)410, step.Camera.Yaw);
                Assert.Equal((short)-112, step.Camera.Pitch);
                Assert.Equal(5216, step.Camera.CurveThreshold);
                Assert.Equal(0, step.AnimationCounter);
            }
            if (count == 42)
            {
                Assert.Equal((short)400, step.Camera.Yaw);
                Assert.Equal((short)-115, step.Camera.Pitch);
                Assert.Equal(5262, step.Camera.CurveThreshold);
                Assert.Equal((short)2231, step.Camera.ProjectionWords[0]);
                Assert.Equal((short)-7526, step.Camera.OffsetZ);
                Assert.False(step.CameraProjectionMode);
            }
        }
        Assert.Equal(new NativeWorldPathPosition(200, -1947, 900), step.Position);
        Assert.Equal((short)-175, step.Camera.Pitch);
        Assert.Equal(6144, step.Camera.CurveThreshold);
        Assert.Equal((short)-14336, step.Camera.OffsetZ);
        Assert.Equal(new short[] { 2896, 3296, 3586, 3846, 4095 }, step.Camera.ProjectionWords);
        Assert.Equal(0, step.TransitionPhase);
        Assert.Throws<InvalidOperationException>(() => step.State.Advance());
    }

    [Fact]
    public void LandingAddsSixtyOneTruncatedStepsAndDoesNotSnapTheCameraOrBodyToTargets()
    {
        NativeRagnarokTransferState state = new(NativeRagnarokTransferKind.Landing, new(200, -1947, 900), -301, 400, FlyingCamera with { Pitch = -175, Yaw = 0 }, LandedCamera with { ProjectionDistance = 640 });
        NativeRagnarokTransferStep step = state.Advance();
        Assert.Equal(8, step.AnimationCounter);
        Assert.Equal(16, step.InitialVerticalScale);
        Assert.Null(step.CameraProjectionMode);
        for (int count = 2; count <= 61; count++)
        {
            step = step.State.Advance();
            Assert.Equal(count == 61, step.Completed);
            if (count == 40)
            {
                Assert.Equal(319, step.AnimationCounter);
            }
        }
        Assert.Equal(-300, step.Position.Y);
        Assert.Equal(658, step.Camera.ProjectionDistance);
        Assert.Equal(5229, step.Camera.CurveThreshold);
        Assert.Equal((short)-114, step.Camera.Pitch);
        Assert.Equal((short)366, step.Camera.Yaw);
        Assert.Equal((short)2225, step.Camera.ProjectionWords[0]);
        Assert.Equal((short)-7077, step.Camera.OffsetZ);
        Assert.Equal(0, step.AnimationCounter);
        Assert.Equal(0, step.TransitionPhase);
    }

    [Fact]
    public void NativeDwordDifferencesAreDividedBeforeWordStorageAndHeightStepCanWrap()
    {
        NativeRagnarokTransferCamera initial = FlyingCamera with { ProjectionDistance = 100000, CurveThreshold = 100000, OffsetZ = 32760 };
        NativeRagnarokTransferState state = new(NativeRagnarokTransferKind.Landing, new(0, 0, 0), 2000000, 0, initial, LandedCamera with { OffsetZ = -32760 });
        NativeRagnarokTransferStep first = state.Advance();
        Assert.Equal(-32203, first.Position.Y);
        Assert.Equal(98351, first.Camera.ProjectionDistance);
        Assert.Equal(98421, first.Camera.CurveThreshold);
        Assert.Equal((short)31668, first.Camera.OffsetZ);
    }

    [Theory]
    [InlineData(NativeRagnarokTransferKind.Landing)]
    [InlineData(NativeRagnarokTransferKind.Takeoff)]
    public void SerializedPureComponentContinuesTheSameNativeCallbackPhase(NativeRagnarokTransferKind kind)
    {
        NativeRagnarokTransferState state = new(kind, new(0, -300, 0), -300, 2048, LandedCamera, kind == NativeRagnarokTransferKind.Takeoff ? FlyingCamera : LandedCamera);
        for (int count = 0; count < 25; count++)
        {
            state = state.Advance().State;
        }
        NativeRagnarokTransferState copy = JsonSerializer.Deserialize<NativeRagnarokTransferState>(JsonSerializer.Serialize(state))!;
        for (int count = 25; count < 61; count++)
        {
            NativeRagnarokTransferStep expected = state.Advance();
            NativeRagnarokTransferStep actual = copy.Advance();
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.Camera with { ProjectionWords = actual.Camera.ProjectionWords, PerspectiveWords = actual.Camera.PerspectiveWords }, actual.Camera);
            Assert.Equal(expected.Camera.ProjectionWords, actual.Camera.ProjectionWords);
            Assert.Equal(expected.Camera.PerspectiveWords, actual.Camera.PerspectiveWords);
            Assert.Equal(expected.Completed, actual.Completed);
            state = expected.State;
            copy = actual.State;
        }
    }

    [Theory]
    [InlineData(199, true, true)]
    [InlineData(200, true, false)]
    [InlineData(199, false, false)]
    public void BoardingRequiresStrictGroundHeightAndTheActualPlacementClearResult(int difference, bool clear, bool expected)
    {
        Assert.Equal(expected, NativeRagnarokTransferInput.PassesBoardingGate(0, 0x8000, 0, difference, true, clear));
        Assert.False(NativeRagnarokTransferInput.PassesBoardingGate(0, 0, 0, difference, true, clear));
    }

    [Fact]
    public void AdmissionKeepsRisingButtonsSeparateFromRagnarokScriptRequests()
    {
        NativeGardenTransferInput input = new(0x40, 0, 50, 0, 0, false, -1, 0, 0, false);
        Assert.True(NativeRagnarokTransferInput.Evaluate(input).Action);
        Assert.False(NativeRagnarokTransferInput.Evaluate(input with { PreviousButtons = 0x40 }).Action);
        Assert.False(NativeRagnarokTransferInput.Evaluate(input with { ScriptMode = 5 }).Action);
        Assert.True(NativeRagnarokTransferInput.Evaluate(input with { ScriptMode = 5, ScriptRequest = 0x40 }).Action);
    }

    [Fact]
    public void ComponentTargetsComeFromTheAuthenticatedOriginalExecutableTables()
    {
        byte[] executable = File.ReadAllBytes(TestResources.GameFile("FF8_EN.exe"));
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.ToHexString(SHA256.HashData(executable)));
        using PEReader reader = new(new MemoryStream(executable));
        short[] Words(int address, int count)
        {
            int rva = address - checked((int)reader.PEHeaders.PEHeader!.ImageBase);
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(item => rva >= item.VirtualAddress && rva < item.VirtualAddress + item.SizeOfRawData);
            int offset = section.PointerToRawData + rva - section.VirtualAddress;
            return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(executable.AsSpan(offset + 2 * index))).ToArray();
        }
        Assert.Equal(FlyingCamera.ProjectionWords, Words(0xC767D0, 5));
        Assert.Equal(FlyingCamera.PerspectiveWords, Words(0xC767E8, 4));
        Assert.Equal(LandedCamera.ProjectionWords, Words(0xC767DC, 5));
        Assert.Equal(LandedCamera.PerspectiveWords, Words(0xC767F0, 4));
        Assert.Equal((short)-112, Words(0xC76D28, 1)[0]);
        Assert.Equal(LandedCamera.OffsetZ, Words(0xC76D34, 1)[0]);
        Assert.Equal(FlyingCamera.OffsetZ, Words(0xC76D3C, 1)[0]);
    }

    [Fact]
    public void MissingProjectionTablesAndInvalidPhasesFailExplicitly()
    {
        NativeRagnarokTransferState state = new(NativeRagnarokTransferKind.Takeoff, default, 0, 0, LandedCamera, FlyingCamera);
        Assert.Throws<InvalidDataException>(() => (state with { Counter = 62 }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { TargetCamera = FlyingCamera with { ProjectionDistance = 640 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { InitialCamera = LandedCamera with { PerspectiveWords = [] } }).Validate());
    }
}
