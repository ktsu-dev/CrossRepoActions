// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Llm;

internal interface ILlmService
{
	public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
