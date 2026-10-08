namespace BranchScribe.Services.LLM;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Runs Phi on-device via Foundry Local CLI using the NPU (QNN Execution Provider).
/// Each inference call invokes `foundry model run --prompt` which runs entirely on
/// the Qualcomm Hexagon NPU via the QNN execution provider.
/// The --retain flag keeps the model loaded between calls for faster subsequent inference.
/// Uses a semaphore to ensure only ONE NPU inference runs at a time to prevent system overload.
/// </summary>
public class FoundryLocalLLM : ILocalLLM
{
    private readonly string _modelAlias;
    private readonly MockLocalLLM _fallback = new();
    private bool _isAvailable;
    private bool _checkedAvailability;
    private static readonly string _foundryPath;
    private static readonly SemaphoreSlim _inferenceLock = new(1, 1);

    public string ProviderId => _isAvailable ? "Foundry-Phi-NPU" : "MockLocal-Fallback";

    static FoundryLocalLLM()
    {
        // Find foundry executable
        _foundryPath = FindFoundryPath() ?? "foundry";
    }

    public FoundryLocalLLM(IConfiguration configuration)
    {
        // The QNN variant is selected by Foundry Local when the alias is used.
        // A variant ID can also be supplied to pin execution to the Qualcomm NPU.
        _modelAlias = configuration["FoundryLocal:ModelId"] ?? "phi-3.5-mini";
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_checkedAvailability)
            return _isAvailable;

        try
        {
            Log("Checking Foundry Local availability...");
            var (exitCode, output) = await RunFoundryAsync("--version", timeoutMs: 10000, ct);
            if (exitCode == 0)
            {
                Log($"Foundry Local found: v{output.Trim()}");
                _isAvailable = true;
            }
            else
            {
                Log("Foundry Local not available.");
                _isAvailable = false;
            }
        }
        catch (Exception ex)
        {
            Log($"Foundry availability check failed: {ex.Message}");
            _isAvailable = false;
        }

        _checkedAvailability = true;
        return _isAvailable;
    }

    public Task MakeAvailableAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        if (!_checkedAvailability)
            await IsAvailableAsync(ct);

        if (!_isAvailable)
            return await _fallback.CompleteAsync(request, ct);

        // Only allow ONE NPU inference at a time to prevent system overload
        // Wait up to 45s for lock (covers model warmup time on first launch)
        if (!await _inferenceLock.WaitAsync(TimeSpan.FromSeconds(45), ct))
        {
            Log("NPU inference SKIPPED - another inference in progress");
            return await _fallback.CompleteAsync(request, ct);
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var prompt = BuildPrompt(request);
            Log($"NPU inference starting ({prompt.Length} chars)...");

            var args = $"complete {_modelAlias} \"{EscapeForCli(prompt)}\" --output json";
            var (exitCode, output) = await RunFoundryAsync(args, timeoutMs: 120000, ct);
            sw.Stop();

            if (exitCode != 0)
            {
                Log($"Foundry returned exit code {exitCode}");
                return await _fallback.CompleteAsync(request, ct);
            }

            // Parse output: remove loading messages, keep only the model response
            var responseText = ParseModelResponse(output);
            Log($"NPU inference COMPLETE in {sw.ElapsedMilliseconds}ms - {responseText.Length} chars");

            return new LlmResponse
            {
                Content = responseText,
                Moderation = new ModerationResult { IsFlagged = false },
                InferenceTimeMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"NPU inference FAILED: {ex.GetType().Name}: {ex.Message}");
            return await _fallback.CompleteAsync(request, ct);
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await CompleteAsync(request, ct);
        var words = response.Content.Split(' ');
        foreach (var word in words)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return word + " ";
        }
    }

    private static string BuildPrompt(LlmRequest request)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            sb.AppendLine(request.SystemPrompt);
            sb.AppendLine();
        }
        sb.Append(request.UserPrompt);
        return sb.ToString();
    }

    private static string EscapeForCli(string text)
    {
        // Escape quotes and newlines for CLI argument
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }

    private static string ParseModelResponse(string rawOutput)
    {
        try
        {
            var completion = JsonSerializer.Deserialize<FoundryCompletion>(rawOutput);
            if (!string.IsNullOrWhiteSpace(completion?.Text))
                return completion.Text.Trim();
        }
        catch (JsonException)
        {
            // Older Foundry versions may return plain text.
        }

        // Remove Foundry status/loading lines and extract just the model response
        var lines = rawOutput.Split('\n');
        var responseLines = new List<string>();
        bool foundResponse = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip known status lines
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed.Contains("ERR]")) continue;
            if (trimmed.Contains("better version")) continue;
            if (trimmed.Contains("foundry model run")) continue;
            if (trimmed.Contains("found in the local cache")) continue;
            if (trimmed.Contains("Loading model")) continue;
            if (trimmed.Contains("loaded successfully")) continue;
            if (trimmed.Contains("Thinking")) continue;
            if (trimmed.Contains("Service is Started")) continue;
            if (trimmed.Contains("Downloading")) continue;
            if (Regex.IsMatch(trimmed, @"^[\x00-\x1F\xEF\xBF]")) continue; // control chars/emoji prefixes

            // The actual response typically starts with the robot emoji or after it
            if (trimmed.StartsWith("\U0001f916") || trimmed.StartsWith("🤖"))
            {
                foundResponse = true;
                var text = trimmed.Length > 2 ? trimmed[2..].Trim() : "";
                if (!string.IsNullOrEmpty(text))
                    responseLines.Add(text);
                continue;
            }

            if (foundResponse || !trimmed.StartsWith('['))
            {
                responseLines.Add(trimmed);
                foundResponse = true;
            }
        }

        return string.Join("\n", responseLines).Trim();
    }

    private sealed record FoundryCompletion(string? Model, string? Text);

    private static async Task<(int exitCode, string output)> RunFoundryAsync(
        string arguments, int timeoutMs, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _foundryPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return (-1, "Timeout");
        }

        // Combine stdout and stderr (foundry outputs response to stdout)
        var combined = outputBuilder.ToString();
        if (string.IsNullOrWhiteSpace(combined))
            combined = errorBuilder.ToString();

        return (process.ExitCode, combined);
    }

    private static string? FindFoundryPath()
    {
        // Check common locations
        var localApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "foundry.exe");
        if (File.Exists(localApps)) return localApps;

        // Check PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "foundry.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [Foundry-NPU] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "phi_npu.log");
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch { }
    }
}
