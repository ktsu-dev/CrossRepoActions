// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Llm;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal sealed class SemanticKernelLlmService : ILlmService
{
	private readonly IChatCompletionService chatService;

	internal SemanticKernelLlmService(LlmSettings settings)
	{
		Ensure.NotNull(settings);
		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			throw new InvalidOperationException("No OpenAI API key configured. Run the ConfigureLlm verb to set one.");
		}

		IKernelBuilder builder = Kernel.CreateBuilder();
		string? orgId = string.IsNullOrWhiteSpace(settings.OrganizationId) ? null : settings.OrganizationId;
		_ = builder.AddOpenAIChatCompletion(settings.Model, settings.ApiKey, orgId);
		Kernel kernel = builder.Build();
		chatService = kernel.GetRequiredService<IChatCompletionService>();
	}

	public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
	{
		ChatHistory history = [];
		history.AddSystemMessage(systemPrompt);
		history.AddUserMessage(userPrompt);

		ChatMessageContent result = await chatService
			.GetChatMessageContentAsync(history, kernel: null, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return result.Content ?? "";
	}
}