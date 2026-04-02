namespace CodeW.UI;

using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

[DataContract]
internal sealed class ConversationTurnViewModel : NotifyPropertyChangedObject
{
    private string role = string.Empty;
    private string roleLabel = string.Empty;
    private string content = string.Empty;
    private DateTimeOffset createdAt;
    private string timestamp = string.Empty;

    [DataMember]
    public string Role
    {
        get => role;
        set => SetProperty(ref role, value);
    }

    [DataMember]
    public string RoleLabel
    {
        get => roleLabel;
        set => SetProperty(ref roleLabel, value);
    }

    [DataMember]
    public string Content
    {
        get => content;
        set => SetProperty(ref content, value);
    }

    [DataMember]
    public DateTimeOffset CreatedAt
    {
        get => createdAt;
        set => SetProperty(ref createdAt, value);
    }

    [DataMember]
    public string Timestamp
    {
        get => timestamp;
        set => SetProperty(ref timestamp, value);
    }

    public void AppendContent(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        Content += delta;
    }
}
