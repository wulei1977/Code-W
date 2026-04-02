namespace CodeW.Models;

internal sealed class ProviderProfile
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public ModelProviderKind Kind { get; set; }

    public string Description { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string DefaultModel { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public ProviderProfile Clone()
    {
        return new ProviderProfile
        {
            Id = Id,
            DisplayName = DisplayName,
            Kind = Kind,
            Description = Description,
            BaseUrl = BaseUrl,
            DefaultModel = DefaultModel,
            ApiKey = ApiKey,
            Enabled = Enabled,
        };
    }
}
