using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;

namespace NotesApi.Rulesets;

public static class RollChainOrchestrator
{
    public static ActionRollPrompt? TryCreateFirstPrompt(
        ActionRequest action,
        Game game,
        string definitionJson,
        DateTime now)
    {
        var chain = RollChainCatalog.GetChain(definitionJson, action.ActionKey);
        if (chain.Count == 0 || !action.ActorCharacterId.HasValue)
        {
            return null;
        }

        var actor = game.Characters.FirstOrDefault(c => c.Id == action.ActorCharacterId.Value);
        if (actor is null)
        {
            return null;
        }

        action.RollChainStateJson = RollChainCatalog.SerializeState(new RollChainState { StepIndex = 0 });
        return RollChainPromptBuilder.CreatePrompt(chain[0], action, actor, definitionJson, now);
    }

    public static Task<ChainProcessResult> ProcessCompletedPromptAsync(
        ApplicationDbContext db,
        ActionRollPrompt prompt,
        ActionRequest action,
        Game game,
        string definitionJson,
        string rollSummary,
        string? rollResultJson,
        DateTime now)
    {
        var chain = RollChainCatalog.GetChain(definitionJson, action.ActionKey);
        if (chain.Count == 0 || string.IsNullOrWhiteSpace(prompt.ChainStepKey))
        {
            return Task.FromResult(ChainProcessResult.Empty);
        }

        var stepIndex = RollChainCatalog.IndexOfStep(chain, prompt.ChainStepKey!);
        if (stepIndex < 0)
        {
            return Task.FromResult(ChainProcessResult.Empty);
        }

        var step = chain[stepIndex];
        var rollData = RollResultParser.TryParseJson(rollResultJson)
            ?? RollResultParser.ParseFromSummary(rollSummary, null, prompt.ResultKind);

        var outcome = RollChainEvaluator.EvaluateAutoResolve(
            step.AutoResolve,
            rollData,
            prompt.ResultKind,
            rollSummary,
            action,
            game);

        prompt.AutoResolveOutcome = outcome;

        var resolvedOutcome = outcome ?? RollChainOutcomes.NeedsDm;
        var isSuccess = resolvedOutcome == RollChainOutcomes.Success;
        var isFailure = resolvedOutcome == RollChainOutcomes.Failure;

        var result = new ChainProcessResult
        {
            AutoResolveOutcome = outcome,
            AutoResolveMessage = BuildAutoResolveMessage(step, outcome, rollData, prompt.ResultKind, rollSummary, action, game),
        };

        var rollValue = RollResultParser.GetPrimaryValue(rollData, prompt.ResultKind, rollSummary) ?? 0;

        if (step.ApplyEffects.Any())
        {
            var effects = RollChainEvaluator.BuildEffectChanges(step.ApplyEffects, action, rollValue).ToList();
            if (effects.Count > 0)
            {
                var merged = RollChainEvaluator.MergePendingEffects(action.PendingChainEffectsJson, effects);
                action.PendingChainEffectsJson = RollChainEvaluator.SerializePendingEffects(merged);
                result.SuggestedStatChanges = effects;
            }
        }

        if (step.ApplyStatuses.Any())
        {
            var statusKeys = RollChainEvaluator.BuildStatusKeys(
                step.ApplyStatuses,
                isSuccess ? RollChainOutcomes.Success : RollChainOutcomes.Failure,
                rollData,
                prompt.ResultKind,
                rollSummary).ToList();
            if (statusKeys.Count > 0)
            {
                result.SuggestedStatusKeys = statusKeys;
            }
        }

        string? nextStepKey = null;
        if (isSuccess && !string.IsNullOrWhiteSpace(step.OnSuccess) && !step.OnSuccess.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            nextStepKey = step.OnSuccess;
        }
        else if (isFailure && !string.IsNullOrWhiteSpace(step.OnFailure) && !step.OnFailure.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            nextStepKey = step.OnFailure;
        }
        else if (!string.IsNullOrWhiteSpace(step.OnComplete) && !step.OnComplete.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            nextStepKey = step.OnComplete;
        }

        if (string.IsNullOrWhiteSpace(nextStepKey) || !action.ActorCharacterId.HasValue)
        {
            return Task.FromResult(result);
        }

        var nextStep = RollChainCatalog.GetStep(definitionJson, action.ActionKey, nextStepKey);
        if (nextStep is null)
        {
            return Task.FromResult(result);
        }

        var actor = game.Characters.FirstOrDefault(c => c.Id == action.ActorCharacterId.Value);
        if (actor is null)
        {
            return Task.FromResult(result);
        }

        // Only auto-queue follow-up on clear success (or unconditional onComplete steps like damage after hit)
        if (isFailure && !string.Equals(step.OnComplete, nextStepKey, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(result);
        }

        if (!isSuccess && step.AutoResolve is not null && outcome != RollChainOutcomes.Success)
        {
            return Task.FromResult(result);
        }

        var nextIndex = RollChainCatalog.IndexOfStep(chain, nextStepKey);
        action.RollChainStateJson = RollChainCatalog.SerializeState(new RollChainState
        {
            StepIndex = nextIndex >= 0 ? nextIndex : stepIndex + 1,
            LastOutcome = resolvedOutcome,
        });

        var nextPrompt = RollChainPromptBuilder.CreatePrompt(nextStep, action, actor, definitionJson, now);
        db.ActionRollPrompts.Add(nextPrompt);
        result.QueuedNextPrompt = nextPrompt;

        return Task.FromResult(result);
    }

    private static string? BuildAutoResolveMessage(
        RulesetRollChainStepDefinition step,
        string? outcome,
        RollResultData? rollData,
        string resultKind,
        string rollSummary,
        ActionRequest action,
        Game game)
    {
        if (outcome is null)
        {
            return null;
        }

        var primary = RollResultParser.GetPrimaryValue(rollData, resultKind, rollSummary);
        if (primary is null)
        {
            return "DM must confirm the outcome.";
        }

        if (outcome == RollChainOutcomes.NeedsDm)
        {
            return "Auto-resolve could not determine the outcome — please confirm.";
        }

        if (step.AutoResolve?.Condition.Contains("successes", StringComparison.OrdinalIgnoreCase) == true)
        {
            return outcome == RollChainOutcomes.Success
                ? $"Hit — {primary} success(es) (auto-resolved)."
                : $"Miss — {primary} success(es) (auto-resolved).";
        }

        if (step.AutoResolve?.Condition.Contains("target", StringComparison.OrdinalIgnoreCase) == true)
        {
            var defense = action.TargetName ?? "target";
            return outcome == RollChainOutcomes.Success
                ? $"Hit — total {primary} met or beat {defense}'s defence (auto-resolved)."
                : $"Miss — total {primary} did not meet {defense}'s defence (auto-resolved).";
        }

        return outcome == RollChainOutcomes.Success ? "Success (auto-resolved)." : "Failure (auto-resolved).";
    }
}

public class ChainProcessResult
{
    public static ChainProcessResult Empty { get; } = new();

    public string? AutoResolveOutcome { get; set; }
    public string? AutoResolveMessage { get; set; }
    public ActionRollPrompt? QueuedNextPrompt { get; set; }
    public List<StatChangeRequest> SuggestedStatChanges { get; set; } = new();
    public List<string> SuggestedStatusKeys { get; set; } = new();
}
