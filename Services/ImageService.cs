using System.Diagnostics;

namespace RuneMobile.Services;

public class ImageService
{
    public string ModelDir => Path.Combine(
        Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath,
        "models");

    public string ModelPath => Path.Combine(ModelDir, "sd-model.gguf");

    public bool ModelExists => File.Exists(ModelPath);

    public async Task<byte[]> GenerateAsync(string prompt)
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
        psi.ArgumentList.Add("--steps"); psi.ArgumentList.Add("12");
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add("4");

        psi.Environment["LD_LIBRARY_PATH"] = nativeLibDir;

        using var process = Process.Start(psi);
        if (process == null) throw new Exception("Failed to start image generation process");

        // Read BOTH streams concurrently to avoid a pipe-buffer deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0 || !File.Exists(outputPath))
            throw new Exception($"Image generation failed (exit {process.ExitCode}): {stderr}\n{stdout}");

        var bytes = await File.ReadAllBytesAsync(outputPath);
        File.Delete(outputPath);
        return bytes;
    }
}
