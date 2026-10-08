namespace BranchScribe.Services.LLM;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
/// On-device AI using Phi Silica via Windows App SDK NPU acceleration.
/// All calls to Microsoft.Windows.AI.Text types use reflection to avoid JIT
/// TypeLoadException on machines where the Windows AI runtime is not installed.
/// On Copilot+ PCs with NPU: runs entirely on the Neural Processing Unit.
/// On other hardware: falls back to MockLocalLLM.
/// </summary>
public class PhiSilicaLocalLLM : ILocalLLM
{
    private readonly MockLocalLLM _fallback = new();
    private bool _isAvailable;
    private bool _checkedAvailability;
    private object? _languageModel;

    public string ProviderId => _isAvailable ? "PhiSilica-NPU" : "MockLocal-Fallback";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_checkedAvailability)
            return _isAvailable;

        try
        {
            var lmType = FindLanguageModelType();
            if (lmType is null)
            {
                Log("LanguageModel type not found — Windows AI runtime not installed.");
                _isAvailable = false;
                _checkedAvailability = true;
                return false;
            }

            var getReadyState = lmType.GetMethod("GetReadyState", BindingFlags.Public | BindingFlags.Static);
            if (getReadyState is null)
            {
                Log("GetReadyState method not found.");
                _isAvailable = false;
                _checkedAvailability = true;
                return false;
            }

            var state = getReadyState.Invoke(null, null);
            var stateStr = state?.ToString() ?? "Unknown";
            Log($"ReadyState = {stateStr}");

            if (stateStr != "Ready")
            {
                var ensureReady = lmType.GetMethod("EnsureReadyAsync", BindingFlags.Public | BindingFlags.Static);
                if (ensureReady is not null)
                {
                    Log("Calling EnsureReadyAsync...");
                    var operation = ensureReady.Invoke(null, null);
                    if (operation is not null)
                        await AwaitAsyncOperation(operation);

                    state = getReadyState.Invoke(null, null);
                    stateStr = state?.ToString() ?? "Unknown";
                    Log($"ReadyState after EnsureReady = {stateStr}");
                }
            }

            _isAvailable = stateStr == "Ready";
            if (_isAvailable)
                Log("NPU Phi Silica AVAILABLE - all inference will run on-device via NPU.");
            else
                Log($"NPU NOT available (state={stateStr}). Falling back to MockLocalLLM.");
        }
        catch (Exception ex)
        {
            Log($"Availability check FAILED: {ex.GetType().Name}: {ex.Message}");
            _isAvailable = false;
        }

        _checkedAvailability = true;
        return _isAvailable;
    }

    public async Task MakeAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var lmType = FindLanguageModelType();
            var ensureReady = lmType?.GetMethod("EnsureReadyAsync", BindingFlags.Public | BindingFlags.Static);
            if (ensureReady is not null)
            {
                var operation = ensureReady.Invoke(null, null);
                if (operation is not null)
                    await AwaitAsyncOperation(operation);
            }
            _isAvailable = true;
            _checkedAvailability = true;
        }
        catch (Exception ex)
        {
            Log($"MakeAvailable failed: {ex.Message}");
            _isAvailable = false;
        }
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        if (!_checkedAvailability)
            await IsAvailableAsync(ct);

        if (!_isAvailable)
            return await _fallback.CompleteAsync(request, ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var model = await GetOrCreateModelAsync();
            if (model is null)
            {
                _isAvailable = false;
                return await _fallback.CompleteAsync(request, ct);
            }

            var prompt = BuildPrompt(request);
            Log($"NPU inference starting ({prompt.Length} chars prompt)...");

            var generateMethod = model.GetType().GetMethod("GenerateResponseAsync",
                new[] { typeof(string) });
            if (generateMethod is null)
                throw new InvalidOperationException("GenerateResponseAsync not found on model");

            var operation = generateMethod.Invoke(model, new object[] { prompt });
            if (operation is null)
                throw new InvalidOperationException("GenerateResponseAsync returned null");

            var result = await AwaitAsyncOperationWithResult(operation);
            sw.Stop();

            var text = result?.GetType().GetProperty("Text")?.GetValue(result)?.ToString() ?? string.Empty;
            Log($"NPU inference COMPLETE in {sw.ElapsedMilliseconds}ms - {text.Length} chars response");

            return new LlmResponse
            {
                Content = text,
                Moderation = new ModerationResult { IsFlagged = false },
                InferenceTimeMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"NPU inference FAILED: {ex.GetType().Name}: {ex.Message}");
            _isAvailable = false;
            return await _fallback.CompleteAsync(request, ct);
        }
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_checkedAvailability)
            await IsAvailableAsync(ct);

        if (!_isAvailable)
        {
            await foreach (var chunk in _fallback.CompleteStreamingAsync(request, ct))
                yield return chunk;
            yield break;
        }

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
        sb.AppendLine(request.UserPrompt);
        return sb.ToString();
    }

    private async Task<object?> GetOrCreateModelAsync()
    {
        if (_languageModel is not null)
            return _languageModel;

        Log("Creating LanguageModel on NPU...");
        var lmType = FindLanguageModelType()
            ?? throw new InvalidOperationException("LanguageModel type not found");

        var createAsync = lmType.GetMethod("CreateAsync",
            BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException("CreateAsync not found");

        var operation = createAsync.Invoke(null, null)
            ?? throw new InvalidOperationException("CreateAsync returned null");

        _languageModel = await AwaitAsyncOperationWithResult(operation);
        Log("LanguageModel created - NPU ready for inference.");
        return _languageModel;
    }

    private static Type? FindLanguageModelType()
    {
        const string typeName = "Microsoft.Windows.AI.Text.LanguageModel";

        // Check already-loaded assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(typeName);
                if (t is not null) return t;
            }
            catch { }
        }

        // Try loading the projection assembly
        try
        {
            var asm = Assembly.Load("Microsoft.Windows.AI.Text.Projection");
            var t = asm.GetType(typeName);
            if (t is not null) return t;
        }
        catch { }

        try
        {
            var asm = Assembly.Load("Microsoft.Windows.AI.Text");
            var t = asm.GetType(typeName);
            if (t is not null) return t;
        }
        catch { }

        return null;
    }

    private static async Task AwaitAsyncOperation(object operation)
    {
        var asTask = operation.GetType().GetMethod("AsTask", Type.EmptyTypes);
        if (asTask is not null)
        {
            await (Task)asTask.Invoke(operation, null)!;
            return;
        }

        var getAwaiter = operation.GetType().GetMethod("GetAwaiter");
        if (getAwaiter is not null)
        {
            var awaiter = getAwaiter.Invoke(operation, null)!;
            var isCompletedProp = awaiter.GetType().GetProperty("IsCompleted");
            while (!(bool)isCompletedProp!.GetValue(awaiter)!)
                await Task.Delay(50);
            awaiter.GetType().GetMethod("GetResult")!.Invoke(awaiter, null);
        }
    }

    private static async Task<object?> AwaitAsyncOperationWithResult(object operation)
    {
        var opType = operation.GetType();

        // Try AsTask() which returns Task<T>
        var asTaskMethods = opType.GetMethods()
            .Where(m => m.Name == "AsTask" && m.GetParameters().Length == 0)
            .ToArray();
        if (asTaskMethods.Length > 0)
        {
            var task = (Task)asTaskMethods[0].Invoke(operation, null)!;
            await task;
            var resultProp = task.GetType().GetProperty("Result");
            return resultProp?.GetValue(task);
        }

        // Fallback: GetAwaiter().GetResult()
        var getAwaiter = opType.GetMethod("GetAwaiter");
        if (getAwaiter is not null)
        {
            var awaiter = getAwaiter.Invoke(operation, null)!;
            var awaiterType = awaiter.GetType();
            var isCompletedProp = awaiterType.GetProperty("IsCompleted");
            while (!(bool)isCompletedProp!.GetValue(awaiter)!)
                await Task.Delay(50);
            return awaiterType.GetMethod("GetResult")!.Invoke(awaiter, null);
        }

        return null;
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [PhiSilica-NPU] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "phi_npu.log");
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch { }
    }
}
