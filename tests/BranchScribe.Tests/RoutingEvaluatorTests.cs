namespace BranchScribe.Tests;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using BranchScribe.Services.Routing;
using BranchScribe.Services.Routing.Evaluators;
using Xunit;

public class RoutingEvaluatorTests
{
    [Fact]
    public async Task PrivacyEvaluator_WithSensitiveDigits_PrefersLocal()
    {
        var evaluator = new PrivacyEvaluator();
        var request = new LlmRequest
        {
            UserPrompt = "Account number is 12345678901234",
        };

        var (target, weight) = await evaluator.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, target);
        Assert.Equal(1.0, weight);
    }

    [Fact]
    public async Task PrivacyEvaluator_WithPiiKeywords_PrefersLocal()
    {
        var evaluator = new PrivacyEvaluator();
        var request = new LlmRequest
        {
            UserPrompt = "Customer's national insurance number",
        };

        var (target, weight) = await evaluator.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, target);
        Assert.Equal(1.0, weight);
    }

    [Fact]
    public async Task PrivacyEvaluator_WithNormalContent_LowWeight()
    {
        var evaluator = new PrivacyEvaluator();
        var request = new LlmRequest
        {
            UserPrompt = "What mortgage rate should I recommend?",
        };

        var (target, weight) = await evaluator.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, target);
        Assert.Equal(0.3, weight);
    }

    [Fact]
    public async Task LatencyEvaluator_ShortPrompt_HighLocalWeight()
    {
        var evaluator = new LatencyEvaluator();
        var request = new LlmRequest { UserPrompt = "Next question?" };

        var (target, weight) = await evaluator.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, target);
        Assert.Equal(0.7, weight);
    }

    [Fact]
    public async Task LatencyEvaluator_LongPrompt_LowerWeight()
    {
        var evaluator = new LatencyEvaluator();
        var request = new LlmRequest { UserPrompt = new string('x', 600) };

        var (target, weight) = await evaluator.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, target);
        Assert.Equal(0.3, weight);
    }

    [Fact]
    public async Task HybridRouter_WithSensitiveContent_RoutesToLocal()
    {
        var rules = new IRoutingRule[]
        {
            new PrivacyEvaluator(),
            new LatencyEvaluator(),
            new CostEvaluator(),
            new LocalContextEvaluator(),
        };
        var router = new HybridRoutingEvaluator(rules);

        var request = new LlmRequest
        {
            UserPrompt = "Sort code 123456 and account number 12345678",
        };

        var decision = await router.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, decision.Target);
        Assert.NotEmpty(decision.Reason);
    }

    [Fact]
    public async Task HybridRouter_WithNormalContent_StillPrefersLocal()
    {
        // Because local-first is the default strategy
        var rules = new IRoutingRule[]
        {
            new PrivacyEvaluator(),
            new LatencyEvaluator(),
            new CostEvaluator(),
            new LocalContextEvaluator(),
        };
        var router = new HybridRoutingEvaluator(rules);

        var request = new LlmRequest { UserPrompt = "Hello" };

        var decision = await router.EvaluateAsync(request);

        Assert.Equal(InferenceTarget.Local, decision.Target);
    }
}
