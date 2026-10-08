namespace BranchScribe;

using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BranchScribe.Models;
using BranchScribe.Services;
using BranchScribe.Services.Interfaces;
using BranchScribe.Services.Transcription;
using BranchScribe.Services.LLM;
using BranchScribe.Services.Extraction;
using BranchScribe.Services.Planning;
using BranchScribe.Services.Agent;
using BranchScribe.Services.Routing;
using BranchScribe.Services.Routing.Evaluators;
using BranchScribe.Services.Metrics;
using BranchScribe.ViewModels;
using System.IO;
using System.Text.Json;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;
    public IServiceProvider Services { get; private set; } = null!;
    public DispatcherQueue? DispatcherQueue { get; private set; }

    private Window? _window;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (s, e) =>
        {
            Program.LogCrash($"App.UnhandledException: {e.Exception}");
            e.Handled = true;
        };


    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            DispatcherQueue = DispatcherQueue.GetForCurrentThread();

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Pre-warm the NPU model in background so first session inference is fast
            _ = Task.Run(async () =>
            {
                try
                {
                    var llm = Services.GetRequiredService<ILocalLLM>();
                    LogToFile("[Warmup] Pre-warming NPU model...");
                    await llm.IsAvailableAsync();
                    var warmup = await llm.CompleteAsync(new LlmRequest
                    {
                        SystemPrompt = "You are a helpful assistant.",
                        UserPrompt = "Say hello.",
                        Temperature = 0.1,
                    });
                    LogToFile($"[Warmup] NPU model warm ({warmup.InferenceTimeMs}ms). Provider: {llm.ProviderId}");
                }
                catch (Exception ex)
                {
                    LogToFile($"[Warmup] Failed: {ex.Message}");
                }
            });

            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            Program.LogCrash($"OnLaunched: {ex}");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true, reloadOnChange: false)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Load form schema
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Schemas", "mortgage_fact_find.json");
        FormSchema schema;
        if (File.Exists(schemaPath))
        {
            var json = File.ReadAllText(schemaPath);
            schema = JsonSerializer.Deserialize<FormSchema>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new FormSchema();
        }
        else
        {
            schema = new FormSchema { SchemaId = "default", Name = "Default Schema" };
        }
        services.AddSingleton(schema);

        // HTTP client
        services.AddHttpClient();

        // Services
        services.AddSingleton<ITranscriptionService, MockTranscriptionService>();
        services.AddSingleton<ILocalLLM>(sp =>
        {
            // Use Foundry Local with NPU (Phi on Qualcomm Hexagon NPU via QNN EP)
            var config = sp.GetRequiredService<IConfiguration>();
            try
            {
                var foundry = new FoundryLocalLLM(config);
                LogToFile("[DI] FoundryLocalLLM created (Phi on NPU via Foundry Local)");
                return foundry;
            }
            catch (Exception ex)
            {
                LogToFile($"[DI] FoundryLocalLLM FAILED: {ex.GetType().Name}: {ex.Message}");
                return new MockLocalLLM();
            }
        });
        services.AddSingleton<IFormExtractor, FormExtractor>();
        services.AddSingleton<INextQuestionPlanner, NextQuestionPlanner>();
        services.AddSingleton<IAssistantAgent, AdvisorAssistantAgent>();
        services.AddSingleton<IPackGenerator, PackGenerator>();
        services.AddSingleton<ISentimentClassifier, SentimentClassifierStub>();
        services.AddSingleton<IPrepGenerator, PrepGenerator>();

        // Routing
        services.AddSingleton<IRoutingRule, PrivacyEvaluator>();
        services.AddSingleton<IRoutingRule, LatencyEvaluator>();
        services.AddSingleton<IRoutingRule, CostEvaluator>();
        services.AddSingleton<IRoutingRule, LocalContextEvaluator>();
        services.AddSingleton<IRoutingEvaluator, HybridRoutingEvaluator>();

        // Metrics
        services.AddSingleton<IMetricsEvaluator, MetricsEvaluator>();

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<SessionViewModel>();
        services.AddSingleton<ReviewPackViewModel>();
        services.AddSingleton<InsightsViewModel>();
    }

    private static void LogToFile(string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "phi_npu.log");
            System.IO.File.AppendAllText(logPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
