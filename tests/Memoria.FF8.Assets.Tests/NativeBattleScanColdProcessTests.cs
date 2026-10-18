using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleScanColdProcessTests
{
    [Fact]
    public void Authenticated_cold_scan_keeps_physical_input_devices_raw_samples_and_sparse_interactive_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        NativeBattleScanProcess scan = game.ReadColdProcessNativeBattleMemory().EffectMemory!.Scan!;
        scan.Validate();
        Check(scan.Input.Input.Devices[0], 0x1D2B110, 196);
        Check(scan.Input.Input.Devices[1], 0x1D2B1D4, 196);
        Check(scan.Input.RawSamples[0], 0x1D2B298, 36);
        Check(scan.Input.RawSamples[1], 0x1D2B2BC, 36);
        Check(scan.Input.Settings, 0x1D2B2F0, 4);
        Check(scan.Input.PollPointers, 0x209CEE4, 12);
        Check(scan.InteractiveInput269AAA8, 0x269AAA8, 48);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.ReadLoader(0x1CD01FC, 4)), scan.PhysicalButtons1CD01FC);
        Assert.False(scan.InputInitialized);
        Assert.Null(scan.Controller);
        for (int index = 0; index < 8; index++)
        {
            Check(scan.Text.Windows.TextRows[index], checked(0x1D2B330u + (uint)(index * 60)), 60);
            Check(scan.Text.Windows.BackendRows[(byte)index], checked(0x1D2B550u + (uint)(index * 64)), 64);
        }
        Check(scan.Text.Expanded.Prefix, 0x1A79008, 0x2E);
        Check(scan.Text.Expanded.Suffix, 0x1A79038, 0x50);
        Check(scan.Text.Globals1D2B524, 0x1D2B524, 12);
        Assert.Equal(original.ReadLoader(0x1D7660C, 1)[0], scan.Text.Formatting1D7660C);
        Assert.Equal(unchecked((sbyte)original.ReadLoader(0x1D2B51C, 1)[0]), scan.Text.SelectedWindow1D2B51C);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.ReadLoader(0x1D76608, 4)), scan.Text.CurrentContext1D76608);
        Assert.False(scan.Text.Initialized);
        Check(scan.Presentation.Camera269A168, 0x269A168, 0x50);
        Check(scan.Presentation.Selection269A1BC, 0x269A1BC, 28);
        Check(scan.Presentation.SavedFlags269A1D8, 0x269A1D8, 28);
        Check(scan.Presentation.SavedColors269CE34, 0x269CE34, 28);
        Check(scan.Presentation.Name269AA78, 0x269AA78, 24);
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(original.ReadLoader(0x269AA90, 2)), scan.Presentation.Highlight269AA90);
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(original.ReadLoader(0x269D938, 2)), scan.Presentation.Result269D938);
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(original.ReadLoader(0x269CE2C, 2)), scan.Presentation.Exit269CE2C);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.ReadLoader(0x269CE30, 4)), scan.Presentation.PrimaryModel269CE30);

        void Check(NativeBattleScanByteOwner owner, uint address, int length)
        {
            owner.RequireLength(length);
            Assert.Equal(original.ReadLoader(address, length), owner.Bytes.ToArray());
            Assert.All(owner.Known, value => Assert.True(value));
        }
    }
}
