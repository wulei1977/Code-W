namespace CodeW.Models;

internal sealed class CodeWConfiguration
{
    public ConversationMode DefaultMode { get; set; } = ConversationMode.Chat;

    public string ActiveProviderId { get; set; } = ProviderIds.OpenAI;

    public List<ProviderProfile> Providers { get; set; } = [];

    public List<McpServerDefinition> McpServers { get; set; } = [];

    public List<SkillDefinition> Skills { get; set; } = [];
}
