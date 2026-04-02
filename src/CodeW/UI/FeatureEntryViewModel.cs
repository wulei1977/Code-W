namespace CodeW.UI;

using System.Runtime.Serialization;

[DataContract]
internal sealed class FeatureEntryViewModel
{
    [DataMember]
    public string Name { get; init; } = string.Empty;

    [DataMember]
    public string Description { get; init; } = string.Empty;

    [DataMember]
    public string Detail { get; init; } = string.Empty;

    [DataMember]
    public string Status { get; init; } = string.Empty;
}
