using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Godot;
using File = System.IO.File;

namespace DohnaDohna.SmokeDriver;

// Test-only adaptation of NinjaSlayer's theater recorder. Real viewport frames,
// QPC request timestamps, asynchronous GPU readback, no simulated clock or audio.
internal sealed class TheaterRecording(Viewport viewport, string directory)
{
    private readonly Channel<(byte[] Pixels, int Repeats)> _frames = Channel.CreateBounded<(byte[], int)>(
        new BoundedChannelOptions(16) { SingleReader = true, SingleWriter = true });
    private readonly List<(long Timestamp, int Frame)> _captured = [];
    private Process? _encoder;
    private Task? _writer;
    private Task<string>? _errors;
    private byte[]? _previous;
    private int _requested, _completed, _pending;
    private long _start;
    private bool _recording;
    private Exception? _failure;
    internal long StartTimestamp => _start;

    internal async Task Start()
    {
        string ffmpeg = System.Environment.GetEnvironmentVariable("DOHNA_FFMPEG")
            ?? throw new InvalidOperationException("Theater needs the configured ffmpeg path.");
        await viewport.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        long timestamp = Stopwatch.GetTimestamp();
        using var image = viewport.GetTexture().GetImage();
        image.Convert(Image.Format.Rgba8);
        var process = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "-y", "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "rgba",
            "-video_size", $"{image.GetWidth()}x{image.GetHeight()}", "-framerate", "60", "-i", "pipe:0",
            "-an", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p",
            System.IO.Path.Combine(directory, "video.mp4") }) process.ArgumentList.Add(arg);
        _encoder = Process.Start(process) ?? throw new IOException("Could not start theater encoder.");
        _errors = _encoder.StandardError.ReadToEndAsync();
        _writer = Task.Run(async () =>
        {
            try
            {
                await foreach (var (pixels, repeats) in _frames.Reader.ReadAllAsync())
                    for (int i = 0; i < repeats; i++)
                        // Windows anonymous pipes can reject a full 5.7 MB
                        // WriteFile with ERROR_NO_SYSTEM_RESOURCES on long
                        // captures. Bounded chunks retain every byte/frame.
                        for (int offset = 0; offset < pixels.Length; offset += 65536)
                            await _encoder.StandardInput.BaseStream.WriteAsync(pixels.AsMemory(offset, Math.Min(65536, pixels.Length - offset)));
            }
            catch (Exception error) { _failure = error; }
            finally { _encoder.StandardInput.Close(); }
        });
        _start = timestamp;
        _previous = image.GetData();
        Enqueue(_previous, 1);
        _requested = _completed = 1;
        _captured.Add((_start, 0));
        File.WriteAllText(System.IO.Path.Combine(directory, "recording-start.json"), JsonSerializer.Serialize(new
        { timestamp = _start, frequency = Stopwatch.Frequency, width = image.GetWidth(), height = image.GetHeight(), fps = 60 }));
        _recording = true;
        RenderingServer.FramePostDraw += Capture;
    }

    private void Enqueue(byte[] pixels, int repeats)
    {
        if (!_frames.Writer.TryWrite((pixels, repeats)))
            throw new IOException("Theater encoder fell behind; refuse to hide missing frames.");
    }

    private void Capture()
    {
        if (!_recording || _failure != null) return;
        long timestamp = Stopwatch.GetTimestamp();
        int expected = (int)Math.Floor((timestamp - _start) * 60d / Stopwatch.Frequency) + 1;
        if (expected <= _requested) return;
        _requested = expected;
        Interlocked.Increment(ref _pending);
        Rid texture = viewport.GetTexture().GetRid();
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            try
            {
                var device = RenderingServer.GetRenderingDevice()
                    ?? throw new InvalidOperationException("Theater requires the selected Vulkan renderer.");
                Rid source = RenderingServer.TextureGetRdTexture(texture);
                using var format = device.TextureGetFormat(source);
                if (format.Format is not (RenderingDevice.DataFormat.R8G8B8A8Unorm or RenderingDevice.DataFormat.R8G8B8A8Srgb))
                    throw new IOException("Unsupported theater pixel format: " + format.Format);
                var result = device.TextureGetDataAsync(source, 0, Callable.From<byte[]>(pixels =>
                {
                    try
                    {
                        if (expected <= _completed || pixels.Length != _previous!.Length)
                            throw new IOException("Out-of-order theater readback or changed viewport size.");
                        if (expected > _completed + 1) Enqueue(_previous, expected - _completed - 1);
                        Enqueue(pixels, 1);
                        _previous = pixels;
                        _completed = expected;
                        _captured.Add((timestamp, expected - 1));
                    }
                    catch (Exception error) { _failure = error; }
                    finally { Interlocked.Decrement(ref _pending); }
                }));
                if (result != Error.Ok) throw new IOException("Theater readback failed: " + result);
            }
            catch (Exception error) { _failure = error; Interlocked.Decrement(ref _pending); }
        }));
    }

    internal async Task Stop()
    {
        if (!_recording) return;
        _recording = false;
        RenderingServer.FramePostDraw -= Capture;
        long stopped = Stopwatch.GetTimestamp();
        while (Volatile.Read(ref _pending) > 0)
        {
            if (Stopwatch.GetElapsedTime(stopped).TotalSeconds > 5) throw new TimeoutException("Theater readback did not drain.");
            await viewport.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        _frames.Writer.TryComplete();
        if (_writer != null) await _writer;
        if (_encoder != null)
        {
            await _encoder.WaitForExitAsync();
            string errors = await _errors!;
            int code = _encoder.ExitCode;
            _encoder.Dispose();
            if (code != 0) throw new IOException("Theater encoder failed: " + errors);
        }
        File.WriteAllLines(System.IO.Path.Combine(directory, "video-frames.csv"),
            new[] { "qpc,frame" }.Concat(_captured.Select(f => $"{f.Timestamp},{f.Frame}")));
        File.WriteAllText(System.IO.Path.Combine(directory, "video-proof.json"), JsonSerializer.Serialize(new
        {
            start = _start, stop = stopped, frequency = Stopwatch.Frequency,
            encodedFrames = _completed, capturedFrames = _captured.Count,
            repeatedFrames = _completed - _captured.Count,
            measuredCaptureFps = _captured.Count / Stopwatch.GetElapsedTime(_start, stopped).TotalSeconds,
            method = "actual viewport asynchronous GPU readback; QPC at request; late frames retain wall-clock duration"
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (_failure != null) throw new IOException("Theater capture failed.", _failure);
    }
}
