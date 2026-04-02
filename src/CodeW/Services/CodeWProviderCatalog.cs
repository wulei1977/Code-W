namespace CodeW.Services;

using CodeW.Models;

internal sealed class CodeWProviderCatalog : ICodeWProviderCatalog
{
    private readonly IReadOnlyList<ProviderProfile> presets =
    [
        new ProviderProfile
        {
            Id = ProviderIds.OpenAI,
            DisplayName = "OpenAI",
            Kind = ModelProviderKind.OpenAI,
            Description = "OpenAI 官方 Chat Completions / 兼容接口。",
            BaseUrl = "https://api.openai.com/v1",
            DefaultModel = "gpt-4.1-mini",
            Enabled = true,
        },
        new ProviderProfile
        {
            Id = ProviderIds.Kimi,
            DisplayName = "Kimi",
            Kind = ModelProviderKind.Kimi,
            Description = "Moonshot / Kimi 的 OpenAI 兼容接口。",
            BaseUrl = "https://api.moonshot.cn/v1",
            DefaultModel = "moonshot-v1-8k",
            Enabled = false,
        },
        new ProviderProfile
        {
            Id = ProviderIds.Qianwen,
            DisplayName = "Qianwen",
            Kind = ModelProviderKind.Qianwen,
            Description = "通义千问 DashScope 的 OpenAI 兼容接口。",
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            DefaultModel = "qwen-plus",
            Enabled = false,
        },
        new ProviderProfile
        {
            Id = ProviderIds.Custom,
            DisplayName = "Custom",
            Kind = ModelProviderKind.OpenAICompatible,
            Description = "任意兼容 OpenAI Chat Completions 的私有或第三方模型网关。",
            BaseUrl = "https://your-openai-compatible-endpoint/v1",
            DefaultModel = "your-model",
            Enabled = false,
        },
    ];

    public IReadOnlyList<ProviderProfile> GetPresetProfiles()
        => presets;
}
