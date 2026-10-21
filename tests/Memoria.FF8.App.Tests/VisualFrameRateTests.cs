using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class VisualFrameRateTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0.5)]
    public void RealTwoSecondStallsReducePresentationFpsIndependentlyOfVisibleChanges(bool changed, double visualFps)
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        PresentSolid(gpu, meter, destination, Color.Red, 0);
        PresentSolid(gpu, meter, destination, changed ? Color.Blue : Color.Red, 2);
        Assert.Equal(0.5, meter.PresentationsPerSecond!.Value);
        Assert.Equal(visualFps, meter.FramesPerSecond!.Value);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(15, 60)]
    [InlineData(30, 60)]
    [InlineData(15, 120)]
    [InlineData(30, 120)]
    [InlineData(60, 60)]
    public void MeasuresChangedOutputInsteadOfPresentationOrTextureUploadCadence(int visualFps, int presentationFps)
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        using SpriteBatch batch = new(gpu.Device);
        using Texture2D texture = new(gpu.Device, 1, 1);
        for (int frame = 0; frame <= presentationFps * 2; frame++)
        {
            int visualFrame = visualFps == 0 ? 0 : frame / (presentationFps / visualFps);
            Color color = new(visualFrame, 28, 50);
            // Decoding or uploading another identical movie/scene texture must not create a visual frame.
            texture.SetData([color]);
            gpu.Device.SetRenderTarget(destination);
            meter.BeginFrame();
            gpu.Device.Clear(Color.Black);
            batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
            batch.Draw(texture, new Rectangle(0, 0, 64, 64), Color.White);
            batch.End();
            meter.CompleteFrame();
            Assert.Equal(color, GpuTestDevice.Read(destination)[32 * 64 + 32]);
            meter.Presented(TimeSpan.FromSeconds(frame / (double)presentationFps));
            if (frame > 0)
            {
                Assert.Equal(presentationFps, meter.PresentationsPerSecond!.Value, 3);
            }
        }
        meter.ResolveCompletedPresentations();
        Assert.Equal(visualFps, meter.FramesPerSecond!.Value, 3);
        Assert.Equal(presentationFps, meter.PresentationsPerSecond!.Value, 3);
    }

    [Fact]
    public void PresentationCadenceIsImmediateWithoutReadbackAndSurvivesComparisonInvalidation()
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        Assert.Null(meter.PresentationsPerSecond);
        for (int frame = 0; frame <= 120; frame++)
        {
            gpu.Device.SetRenderTarget(destination);
            meter.BeginFrame();
            gpu.Device.Clear(Color.Blue);
            meter.CompleteFrame();
            meter.Presented(TimeSpan.FromSeconds(frame / 60d));
            if (frame == 0)
            {
                Assert.Null(meter.PresentationsPerSecond);
                continue;
            }
            // No GetData or explicit query resolution: actual presentations are already observable.
            Assert.Equal(60, meter.PresentationsPerSecond!.Value, 3);
            if (frame == 60)
            {
                typeof(VisualFrameRate).GetMethod("ResetMeasurement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(meter, null);
                Assert.Null(meter.FramesPerSecond);
                Assert.Equal(60, meter.PresentationsPerSecond!.Value, 3);
            }
        }
    }

    [Fact]
    public void PresentationCadenceIncludesAStallAndRecoversWithStillOutput()
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        for (int frame = 0; frame <= 60; frame++)
        {
            PresentSolid(gpu, meter, destination, Color.Blue, frame / 60d);
        }
        Assert.Equal(0, meter.FramesPerSecond!.Value);
        Assert.Equal(60, meter.PresentationsPerSecond!.Value, 3);
        PresentSolid(gpu, meter, destination, Color.Blue, 4);
        Assert.Equal(1d / 3, meter.PresentationsPerSecond!.Value, 6);
        for (int frame = 1; frame <= 60; frame++)
        {
            PresentSolid(gpu, meter, destination, Color.Blue, 4 + frame / 60d);
        }
        Assert.Equal(0, meter.FramesPerSecond!.Value);
        Assert.Equal(60, meter.PresentationsPerSecond!.Value, 3);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    public void OneByteChangeAtTheLastPixelCountsAsAVisualFrame(int red, int green, int blue)
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        using SpriteBatch batch = new(gpu.Device);
        using Texture2D pixel = new(gpu.Device, 1, 1);
        Color background = new(12, 34, 56);
        Color changed = new(12 + red, 34 + green, 56 + blue);
        pixel.SetData([changed]);
        for (int frame = 0; frame <= 1; frame++)
        {
            gpu.Device.SetRenderTarget(destination);
            meter.BeginFrame();
            gpu.Device.Clear(background);
            if (frame == 1)
            {
                batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
                batch.Draw(pixel, new Rectangle(63, 63, 1, 1), Color.White);
                batch.End();
            }
            meter.CompleteFrame();
            Color[] output = GpuTestDevice.Read(destination);
            Assert.Equal(frame == 0 ? background : changed, output[^1]);
            meter.Presented(TimeSpan.FromSeconds(frame));
        }
        meter.ResolveCompletedPresentations();
        Assert.Equal(1, meter.FramesPerSecond!.Value);
    }

    [Fact]
    public void AlphaAndTheOverlayDrawnAfterCompositionDoNotCount()
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        for (int frame = 0; frame <= 120; frame++)
        {
            gpu.Device.SetRenderTarget(destination);
            meter.BeginFrame();
            gpu.Device.Clear(new Color(12, 34, 56, frame));
            meter.CompleteFrame();
            // The changing FPS label is drawn onto the destination after comparison.
            gpu.Device.Clear(new Color(frame, 0, 0));
            GpuTestDevice.Read(destination);
            meter.Presented(TimeSpan.FromSeconds(frame / 60d));
        }
        meter.ResolveCompletedPresentations();
        Assert.Equal(0, meter.FramesPerSecond!.Value);
    }

    [Fact]
    public void ResizeStartsANewBaselineAndRestoresDestinationViewport()
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D original = gpu.CreateTarget(64, 64);
        using RenderTarget2D resized = gpu.CreateTarget(32, 24);
        PresentSolid(gpu, meter, original, Color.Red, 0);
        PresentSolid(gpu, meter, original, Color.Blue, 1);
        Assert.Equal(1, meter.FramesPerSecond!.Value);
        gpu.Device.SetRenderTarget(resized);
        meter.BeginFrame();
        Assert.Null(meter.FramesPerSecond);
        Assert.Null(meter.PresentationsPerSecond);
        Assert.Equal(new Rectangle(0, 0, 32, 24), gpu.Device.Viewport.Bounds);
        gpu.Device.Clear(Color.Green);
        meter.CompleteFrame();
        Assert.Equal(new Rectangle(0, 0, 32, 24), gpu.Device.Viewport.Bounds);
        Assert.All(GpuTestDevice.Read(resized), pixel => Assert.Equal(Color.Green, pixel));
        meter.Presented(TimeSpan.FromSeconds(10));
        Assert.Null(meter.FramesPerSecond);
        Assert.Null(meter.PresentationsPerSecond);
        PresentSolid(gpu, meter, resized, Color.Green, 10.5);
        Assert.Equal(0, meter.FramesPerSecond!.Value);
        Assert.Equal(2, meter.PresentationsPerSecond!.Value);
    }

    [Fact]
    public void CompositionPreservesAnOffsetViewportAndItsSurroundingPixels()
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D destination = gpu.CreateTarget(64, 64);
        for (int frame = 0; frame <= 1; frame++)
        {
            gpu.Device.SetRenderTarget(destination);
            gpu.Device.Clear(Color.Black);
            Viewport viewport = new(7, 11, 32, 24);
            gpu.Device.Viewport = viewport;
            meter.BeginFrame();
            gpu.Device.Clear(Color.Blue);
            meter.CompleteFrame();
            Assert.Equal(viewport, gpu.Device.Viewport);
            Color[] output = GpuTestDevice.Read(destination);
            Assert.Equal(Color.Blue, output[11 * 64 + 7]);
            Assert.Equal(Color.Blue, output[34 * 64 + 38]);
            Assert.Equal(Color.Black, output[11 * 64 + 6]);
            Assert.Equal(Color.Black, output[35 * 64 + 38]);
            meter.Presented(TimeSpan.FromSeconds(frame));
        }
        Assert.Equal(0, meter.FramesPerSecond!.Value);
    }

    [Fact]
    public void DeviceResetDiscardsPendingComparisonsAndThePreviousImage()
    {
        using GpuTestDevice gpu = new();
        using VisualFrameRate meter = new(gpu.Device);
        using RenderTarget2D original = gpu.CreateTarget(64, 64);
        PresentSolid(gpu, meter, original, Color.Red, 0);
        PresentSolid(gpu, meter, original, Color.Blue, 1);
        gpu.Device.Reset(gpu.Device.PresentationParameters);
        Assert.Null(meter.FramesPerSecond);
        Assert.Null(meter.PresentationsPerSecond);
        using RenderTarget2D restored = gpu.CreateTarget(64, 64);
        PresentSolid(gpu, meter, restored, Color.Green, 10);
        Assert.Null(meter.FramesPerSecond);
        Assert.Null(meter.PresentationsPerSecond);
        PresentSolid(gpu, meter, restored, Color.Green, 10.5);
        Assert.Equal(0, meter.FramesPerSecond!.Value);
        Assert.Equal(2, meter.PresentationsPerSecond!.Value);
    }

    private static void PresentSolid(GpuTestDevice gpu, VisualFrameRate meter, RenderTarget2D destination, Color color, double seconds)
    {
        gpu.Device.SetRenderTarget(destination);
        meter.BeginFrame();
        gpu.Device.Clear(color);
        meter.CompleteFrame();
        GpuTestDevice.Read(destination);
        meter.Presented(TimeSpan.FromSeconds(seconds));
        meter.ResolveCompletedPresentations();
    }
}
