using System.Globalization;
using System.Runtime.InteropServices;
using Memoria.FF8.Platform;
using Memoria.FF8.TitleScene;
using SkiaSharp;

namespace Memoria.FF8;

public sealed class PortablePresentationTests
{
    [Theory]
    [InlineData("Arial", "AVATAR É e\u0301")]
    [InlineData("sans-serif", "سلام عالم")]
    [InlineData("sans-serif", "日本語")]
    public void Shaped_advances_and_raster_masks_share_one_portable_engine(string family, string text)
    {
        using PortableTextShaper shaper = new(24, family);
        Assert.True(shaper.Measure(text) > 0);
        Assert.True(shaper.Measure(text + " ") > shaper.Measure(text));
        using SKBitmap bitmap = PortableTextRasterizer.Rasterize(text, 24, float.PositiveInfinity, family);
        Assert.Contains(bitmap.Pixels, color => color.Alpha > 0);
        Assert.InRange(bitmap.Width, 1, (int)MathF.Ceiling(shaper.Measure(text) * PortableTextRasterizer.RasterScale) + 48);
        using SKBitmap repeat = PortableTextRasterizer.Rasterize(text, 24, float.PositiveInfinity, family);
        Assert.Equal(PortableRaster.Rgba(bitmap), PortableRaster.Rgba(repeat));
    }

    [Fact]
    public void Missing_requested_family_renders_exactly_as_the_prepared_default_family()
    {
        const string missing = "Memoria Missing Family For Fallback Regression";
        Assert.DoesNotContain(missing, SystemFontCatalog.Families);
        Assert.Equal(SystemFontCatalog.DefaultFamily, SystemFontCatalog.ResolveFamily(missing));
        using SKBitmap requested = PortableTextRasterizer.Rasterize("Fallback É e\u0301 سلام", 24, 900, missing);
        using SKBitmap actualDefault = PortableTextRasterizer.Rasterize("Fallback É e\u0301 سلام", 24, 900, SystemFontCatalog.DefaultFamily);
        Assert.Equal(actualDefault.Width, requested.Width);
        Assert.Equal(actualDefault.Height, requested.Height);
        Assert.Equal(PortableRaster.Rgba(actualDefault), PortableRaster.Rgba(requested));
    }

    [Fact]
    public void Published_unicode_bracket_embedding_and_isolate_cases_match_resolved_levels()
    {
        using Stream resource = typeof(PortablePresentationTests).Assembly.GetManifestResourceStream("Memoria.FF8.Presentation.Tests.BidiConformanceCases.txt")!;
        using StreamReader reader = new(resource);
        Memoria.FF8.Text.BidiLayout bidi = new();
        int cases = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            string[] fields = line.Split(';');
            int[] codePoints = fields[0].Split(' ').Select(value => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
            sbyte[] actual = bidi.ResolveLevels(codePoints, sbyte.Parse(fields[1], CultureInfo.InvariantCulture));
            string[] expected = fields[3].Split(' ');
            Assert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                if (expected[index] != "x")
                {
                    Assert.Equal(sbyte.Parse(expected[index], CultureInfo.InvariantCulture), actual[index]);
                }
            }
            cases++;
        }
        Assert.Equal(96, cases);
    }

    [Fact]
    public async Task In_game_confirmation_remains_pending_until_explicit_selection_and_defaults_to_cancel()
    {
        using GameConfirmationOverlay overlay = new();
        Task<bool> cancelled = overlay.ShowAsync("Review the model changes", "Import", TestContext.Current.CancellationToken);
        Assert.True(overlay.IsOpen);
        Assert.False(overlay.AcceptSelected);
        Assert.False(cancelled.IsCompleted);
        overlay.Handle(Ui.UiKey.Confirm);
        Assert.False(await cancelled);
        Assert.False(overlay.IsOpen);
        Task<bool> accepted = overlay.ShowAsync("Review the model changes", "Import", TestContext.Current.CancellationToken);
        overlay.Handle(Ui.UiKey.Right);
        Assert.False(accepted.IsCompleted);
        overlay.Handle(Ui.UiKey.Confirm);
        Assert.True(await accepted);
        Task<bool> closed = overlay.ShowAsync("Review", "Export", TestContext.Current.CancellationToken);
        overlay.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closed);
        Assert.False(overlay.IsOpen);
    }

    [Fact]
    public void Mixed_latin_arabic_and_nested_numeric_runs_use_resolved_direction_and_script()
    {
        using PortableTextShaper shaper = new(32, "Arial");
        Assert.Equal(shaper.Measure("Latin ") + shaper.Measure("سلام"), shaper.Measure("Latin سلام"), 3);
        // Installed fallback fonts may split a directional run around spaces.
        // Compare visual character order and direction independently of those font boundaries.
        Assert.Equal(VisualCharacters([("Latin ", false), ("سلام", true)]), VisualCharacters(shaper.VisualRuns("Latin سلام")));
        Assert.Equal(VisualCharacters([(" عالم", true), ("123", false), ("سلام ", true)]), VisualCharacters(shaper.VisualRuns("سلام 123 عالم")));
        using SKBitmap raster = PortableTextRasterizer.Rasterize("Latin سلام 123 (עברית) e\u0301 日本語", 32, 1200, "Arial");
        Assert.Contains(raster.Pixels, color => color.Alpha > 0);

        static IEnumerable<(char Character, bool RightToLeft)> VisualCharacters((string Text, bool RightToLeft)[] runs)
        {
            foreach ((string text, bool rightToLeft) in runs)
            {
                IEnumerable<char> characters = rightToLeft ? text.Reverse() : text;
                foreach (char character in characters)
                {
                    yield return (character, rightToLeft);
                }
            }
        }
    }

    [Theory]
    [InlineData("日本", "日", "本")]
    [InlineData("عربي", "ع", "ب")]
    public void Installed_script_fallback_draws_distinct_real_glyphs(string sample, string first, string second)
    {
        using SKTypeface? face = SKFontManager.Default.MatchCharacter("Arial", System.Text.Rune.GetRuneAt(sample, 0).Value);
        Assert.SkipWhen(face is null, "This host has no installed font for the requested optional script.");
        using SKBitmap one = PortableTextRasterizer.Rasterize(first, 32, 300, "Arial");
        using SKBitmap two = PortableTextRasterizer.Rasterize(second, 32, 300, "Arial");
        using SKBitmap missing = PortableTextRasterizer.Rasterize("\U0010FFFF", 32, 300, "Arial");
        Assert.NotEqual(PortableRaster.Rgba(one), PortableRaster.Rgba(two));
        Assert.NotEqual(PortableRaster.Rgba(one), PortableRaster.Rgba(missing));
    }

    [Fact]
    public void Portable_multilingual_contact_sheet_uses_the_production_shaper_and_alpha_masks()
    {
        string[] samples = ["É e\u0301 · Я Ω · 日本語", "عربي سلام 123 · Latin (עברית)", "👩🏽‍🚀 👨‍👩‍👧‍👦 · AVATAR"];
        using SKBitmap sheet = new(1200, 360, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKCanvas canvas = new(sheet);
        canvas.Clear(new SKColor(16, 24, 32));
        using SKPaint paint = new() { Color = SKColors.White, IsAntialias = true };
        for (int index = 0; index < samples.Length; index++)
        {
            using PortableTextShaper shaper = new(38, "Arial");
            shaper.Draw(canvas, samples[index], 24, 75 + index * 110, paint);
        }
        Assert.Contains(sheet.Pixels, color => color.Red > 180);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_PRESENTATION_OUTPUT") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            sheet.SavePng(Path.Combine(directory, "portable-multilingual.png"));
        }
    }

    [Fact]
    public void Arabic_shaping_connects_letters_and_does_not_measure_as_isolated_codepoints()
    {
        using PortableTextShaper shaper = new(32, "sans-serif");
        const string word = "سلام";
        Assert.True(shaper.Measure(word) < word.Sum(character => shaper.Measure(character.ToString())));
    }

    [Theory]
    [InlineData("e\u0301a\u0308o\u0302")]
    [InlineData("👩🏽‍🚀👨‍👩‍👧‍👦🇩🇪")]
    [InlineData("क्‍षक्‍षक्‍ष")]
    public void Wrapping_preserves_complete_graphemes(string text)
    {
        List<string> elements = [];
        TextElementEnumerator iterator = StringInfo.GetTextElementEnumerator(text);
        while (iterator.MoveNext())
        {
            elements.Add(iterator.GetTextElement());
        }
        Assert.Equal(elements, PortableTextRasterizer.Wrap(text, value => value.Length, 1));
    }

    [Fact]
    public void Full_line_masks_keep_blank_lines_and_baselines()
    {
        using SKBitmap blank = PortableTextRasterizer.Rasterize(" ", 20, 400, preserveLineMetrics: true);
        using SKBitmap capital = PortableTextRasterizer.Rasterize("É", 20, 400, preserveLineMetrics: true);
        using SKBitmap descender = PortableTextRasterizer.Rasterize("gyp", 20, 400, preserveLineMetrics: true);
        Assert.Equal(capital.Height, descender.Height);
        Assert.Equal(capital.Height, blank.Height);
        Assert.All(blank.Pixels, pixel => Assert.Equal(0, pixel.Alpha));
        using SKBitmap two = PortableTextRasterizer.Rasterize("É\n\ngyp", 20, 400, preserveLineMetrics: true);
        Assert.True(two.Height > capital.Height * 2 - PortableTextRasterizer.RasterMargin(20) * 2);
    }

    [Theory]
    [InlineData("sony")]
    [InlineData("xbox")]
    public void Controller_contours_scale_without_a_gpu_or_window(string family)
    {
        foreach (int icon in Enumerable.Range(128, 12).Where(icon => ControllerVectorAtlas.Supports(icon, family)))
        {
            using SKBitmap small = ControllerVectorAtlas.Render(icon, family, 24);
            using SKBitmap large = ControllerVectorAtlas.Render(icon, family, 192);
            Assert.Equal(24, small.Width);
            Assert.Equal(192, large.Width);
            Assert.Equal(0, large.GetPixel(0, 0).Alpha);
            Assert.Contains(large.Pixels, pixel => pixel.Alpha > 0);
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    public void Silver_crest_is_a_portable_vector_raster(int height)
    {
        using SKBitmap crest = TitleArtworkRenderer.CreateSilverCrestImage(height);
        Assert.Equal(height, crest.Height);
        Assert.InRange(crest.Width, 2, height);
        Assert.Equal(0, crest.GetPixel(0, 0).Alpha);
        Assert.Contains(crest.Pixels, pixel => pixel.Alpha > 0);
    }

    [Fact]
    public void Straight_rgba_survives_png_encoding_with_transparent_and_translucent_pixels()
    {
        byte[] rgba = [17, 45, 93, 128, 201, 151, 101, 255, 0, 0, 0, 0];
        using SKBitmap source = PortableRaster.FromRgba(rgba, 3, 1);
        Assert.Equal(rgba, PortableRaster.Rgba(source));
        using MemoryStream stream = new();
        source.SavePng(stream);
        stream.Position = 0;
        using SKBitmap decoded = SKBitmap.Decode(stream, new SKImageInfo(3, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Assert.Equal(rgba, PortableRaster.Rgba(decoded));
    }

    [Fact]
    public void Dialog_callback_distinguishes_cancel_error_and_copied_utf8_path()
    {
        Assert.Equal(DialogStatus.Failed, SdlDesktopDialogs.ReadResult(0, () => "native failure").Status);
        nint list = Marshal.AllocHGlobal(nint.Size);
        nint filename = Marshal.StringToCoTaskMemUTF8("/tmp/日本語.glb");
        try
        {
            Marshal.WriteIntPtr(list, 0);
            Assert.Equal(DialogStatus.Cancelled, SdlDesktopDialogs.ReadResult(list, () => "unused").Status);
            Marshal.WriteIntPtr(list, filename);
            DialogResult result = SdlDesktopDialogs.ReadResult(list, () => "unused");
            Assert.Equal(DialogStatus.Selected, result.Status);
            Assert.Equal("/tmp/日本語.glb", result.Path);
        }
        finally
        {
            Marshal.FreeCoTaskMem(filename);
            Marshal.FreeHGlobal(list);
        }
    }

    [Fact]
    public void Dialog_continuations_wait_for_the_game_thread_pump()
    {
        GameDialogContext context = new();
        int thread = Environment.CurrentManagedThreadId;
        bool invoked = false;
        context.OperationStarted();
        Assert.True(context.IsBusy);
        context.Post(_ =>
        {
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            invoked = true;
            context.OperationCompleted();
        }, null);
        Assert.False(invoked);
        context.Drain();
        Assert.True(invoked);
        Assert.False(context.IsBusy);
    }

    [Fact]
    public async Task Cancelled_dialog_keeps_its_native_callback_rooted_until_late_completion()
    {
        SDL3.SDL.DialogFileCallback callback = null!;
        nint id = 0;
        using SdlDesktopDialogs dialogs = new(start: (_, _, requestId, completed) =>
        {
            id = requestId;
            callback = completed;
        });
        using CancellationTokenSource cancellation = new();
        Task<DialogResult> result = dialogs.OpenFileAsync(new("Open model"), cancellation.Token);
        Assert.False(result.IsCompleted);
        Assert.Equal(1, dialogs.NativeCallbacksPending);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
        Assert.Equal(1, dialogs.NativeCallbacksPending);
        nint list = Marshal.AllocHGlobal(nint.Size);
        try
        {
            Marshal.WriteIntPtr(list, 0);
            await Task.Run(() => callback(id, list, -1), TestContext.Current.CancellationToken);
            Assert.Equal(0, dialogs.NativeCallbacksPending);
            Assert.True(result.IsCanceled);
        }
        finally
        {
            Marshal.FreeHGlobal(list);
        }
    }

    [Fact]
    public async Task Host_disposal_cancels_pending_dialogs_and_late_native_callbacks_release_the_request()
    {
        SDL3.SDL.DialogFileCallback callback = null!;
        nint id = 0;
        SdlDesktopDialogs dialogs = new(start: (_, _, requestId, completed) =>
        {
            id = requestId;
            callback = completed;
        });
        Task<DialogResult> result = dialogs.SelectFolderAsync(new("Folder"), TestContext.Current.CancellationToken);
        dialogs.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
        Assert.Throws<ObjectDisposedException>(() => { _ = dialogs.SelectFolderAsync(new("Closed"), TestContext.Current.CancellationToken); });
        nint list = Marshal.AllocHGlobal(nint.Size);
        try
        {
            Marshal.WriteIntPtr(list, 0);
            callback(id, list, -1);
            Assert.Equal(0, dialogs.NativeCallbacksPending);
        }
        finally
        {
            Marshal.FreeHGlobal(list);
        }
    }

    [Fact]
    public async Task Main_thread_keeps_pumping_while_a_native_picker_is_pending_and_drops_work_after_disposal()
    {
        using GameDialogContext context = new();
        SDL3.SDL.DialogFileCallback callback = null!;
        nint id = 0;
        int pumps = 0;
        using SdlDesktopDialogs dialogs = new(start: (_, _, requestId, completed) =>
        {
            id = requestId;
            callback = completed;
        }, pump: () => pumps++);
        Task<DialogResult> result = dialogs.SaveFileAsync(new("Save model"), TestContext.Current.CancellationToken);
        int frames = 0;
        for (int frame = 0; frame < 4; frame++)
        {
            dialogs.PumpPendingEvents();
            context.Post(_ => frames++, null);
            context.Drain();
            Assert.False(result.IsCompleted);
        }
        Assert.Equal(4, frames);
        Assert.Equal(4, pumps);
        context.Dispose();
        context.Post(_ => frames++, null);
        context.Drain();
        Assert.Equal(4, frames);
        nint list = Marshal.AllocHGlobal(nint.Size);
        try
        {
            Marshal.WriteIntPtr(list, 0);
            callback(id, list, -1);
            Assert.True(result.IsCompletedSuccessfully);
            Assert.Equal(DialogStatus.Cancelled, (await result).Status);
        }
        finally
        {
            Marshal.FreeHGlobal(list);
        }
    }

    [Fact]
    public async Task Long_frame_wait_cancels_promptly_without_waiting_for_the_frame_deadline()
    {
        using PresentationPacer pacer = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task wait = Task.Run(() => pacer.WaitForNextFrame(1, cancellation.Token), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        System.Diagnostics.Stopwatch stop = System.Diagnostics.Stopwatch.StartNew();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.True(stop.Elapsed < TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Missed_display_deadlines_advance_to_the_next_future_period()
    {
        Assert.Equal(110, PresentationPacer.AdvanceDeadline(100, 102, 10));
        Assert.Equal(160, PresentationPacer.AdvanceDeadline(100, 159, 10));
        using PresentationPacer pacer = new();
        pacer.WaitForNextFrame(0, TestContext.Current.CancellationToken);
        pacer.Reset();
    }
}
