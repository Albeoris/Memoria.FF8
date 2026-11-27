using System.Diagnostics;

namespace Memoria.FF8.App.Tests;

public sealed class PerformanceTraceTests
{
    [Fact]
    public void CaptureStreamsCompleteIntervalsAndFinalizesBeforeStartingANewFile()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        PerformanceTrace trace = new();
        try
        {
            Assert.True(trace.Start(directory.FullName));
            string firstFile = Assert.IsType<string>(trace.FilePath);
            long headerBytes = trace.BytesWritten;
            Assert.Equal(new FileInfo(firstFile).Length, headerBytes);
            Assert.False(trace.Start(directory.FullName));

            long start = Stopwatch.GetTimestamp();
            long end = start + Stopwatch.Frequency;
            trace.Presented("WorldMap", null, null, start, start, default, default);
            trace.Presented("Field", 74, null, start, end, default, default);
            Assert.True(trace.BytesWritten > headerBytes);
            Assert.Equal(new FileInfo(firstFile).Length, trace.BytesWritten);
            trace.Presented("Battle", null, 12, end, end + 1, default, default);
            trace.Stop();
            Assert.False(trace.IsRecording);
            Assert.Null(trace.Error);
            Assert.Equal(new FileInfo(firstFile).Length, trace.BytesWritten);

            string[] lines = File.ReadAllLines(firstFile);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("1,1000.000,Field,74,,1000.000,", lines[1]);
            Assert.Equal(new[] { "Battle", "", "12" }, lines[2].Split(',')[2..5]);
            Assert.Equal(lines[0].Split(',').Length, lines[2].Split(',').Length);

            Assert.True(trace.Start(directory.FullName));
            Assert.NotEqual(firstFile, trace.FilePath);
            trace.Stop();
            Assert.Single(File.ReadAllLines(trace.FilePath!));
            Assert.Equal(lines, File.ReadAllLines(firstFile));
        }
        finally
        {
            trace.Stop();
            directory.Delete(true);
        }
    }

    [Fact]
    public void ActiveByteCountIncludesAutomaticBufferWritesBetweenTimedFlushes()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        PerformanceTrace trace = new();
        try
        {
            Assert.True(trace.Start(directory.FullName));
            long headerBytes = trace.BytesWritten;
            for (int frame = 1; frame <= 256; frame++)
            {
                trace.Presented("Field", 74, null, frame, frame, default, default);
            }
            Assert.True(trace.BytesWritten > headerBytes);
            Assert.Equal(new FileInfo(trace.FilePath!).Length, trace.BytesWritten);
        }
        finally
        {
            trace.Stop();
            directory.Delete(true);
        }
    }

    [Fact]
    public void FailedStartLeavesRecorderAvailableForRetry()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        PerformanceTrace trace = new();
        try
        {
            string occupied = Path.Combine(directory.FullName, "occupied");
            File.WriteAllText(occupied, string.Empty);
            Assert.False(trace.Start(occupied));
            Assert.False(trace.IsRecording);
            Assert.NotNull(trace.Error);
            Assert.True(trace.Start(directory.FullName));
            Assert.Null(trace.Error);
        }
        finally
        {
            trace.Stop();
            directory.Delete(true);
        }
    }
}
