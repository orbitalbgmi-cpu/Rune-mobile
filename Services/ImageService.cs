using System.Diagnostics;
using Android.Content;
using Android.Provider;

namespace RuneMobile.Services;

public class ImageService
{
    public string ModelDir => Path.Combine(
        Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath,
        "models");

    public string ModelPath => Path.Combine(ModelDir, "sd-model.gguf");

    public bool ModelExists => File.Exists(ModelPath);

    public async Task<byte[]> GenerateAsync(string prompt, Action<string>? onProgress = null)
    {
        var nativeLibDir = Android.App.Application.Context.ApplicationInfo!.NativeLibraryDir!;
        var sdExe = Path.Combine(nativeLibDir, "libsdcli.so");

        var outputDir = Android.App.Application.Context.CacheDir!.AbsolutePath;
        var outputPath = Path.Combine(outputDir, $"gen-{Guid.NewGuid():N}.png");

        var psi = new ProcessStartInfo
        {
            FileName = sdExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-M"); psi.ArgumentList.Add("img_gen");
        psi.ArgumentList.Add("-m"); psi.ArgumentList.Add(ModelPath);
        psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(prompt);
        psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(outputPath);
        psi.ArgumentList.Add("-W"); psi.ArgumentList.Add("256");
        psi.ArgumentList.Add("-H"); psi.ArgumentList.Add("256");
        psi.ArgumentList.Add("--steps"); psi.ArgumentList.Add("8");
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add("2");

        psi.Environment["LD_LIBRARY_PATH"] = nativeLibDir;

        using var process = new Process { StartInfo = psi };

        var lastLine = "";
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) { lastLine = e.Data; onProgress?.Invoke(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) { lastLine = e.Data; onProgress?.Invoke(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
        var exitTask = process.WaitForExitAsync();
        var finished = await Task.WhenAny(exitTask, timeoutTask);

        if (finished == timeoutTask)
        {
            try { process.Kill(true); } catch { }
            throw new Exception($"Generation timed out after 5 minutes. Last output: {lastLine}");
        }

        if (process.ExitCode != 0 || !File.Exists(outputPath))
            throw new Exception($"Image generation failed (exit {process.ExitCode}). Last output: {lastLine}");

        var bytes = await File.ReadAllBytesAsync(outputPath);
        File.Delete(outputPath);
        return bytes;
    }

    public void SaveToGallery(byte[] pngBytes, string fileName)
    {
        var context = Android.App.Application.Context;
        var resolver = context.ContentResolver!;

        var values = new ContentValues();
        values.Put(MediaStore.MediaColumns.DisplayName, fileName);
        values.Put(MediaStore.MediaColumns.MimeType, "image/png");
        values.Put(MediaStore.MediaColumns.RelativePath, "Pictures/RUNE");

        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri!, values);
        if (uri == null) throw new Exception("Could not create gallery entry");

        using var stream = resolver.OpenOutputStream(uri);
        if (stream == null) throw new Exception("Could not open output stream");
        stream.Write(pngBytes, 0, pngBytes.Length);
    }
}
