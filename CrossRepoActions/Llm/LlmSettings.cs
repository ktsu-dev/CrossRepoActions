// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.CrossRepoActions.Llm;

internal sealed class LlmSettings
{
	public string ApiKey { get; set; } = "";
	public string Model { get; set; } = "gpt-5.4-mini";
	public string OrganizationId { get; set; } = "";
	public int MaxDiffChars { get; set; } = 8000;
}
