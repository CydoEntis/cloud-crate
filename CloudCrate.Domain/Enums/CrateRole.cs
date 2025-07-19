namespace CloudCrate.Domain.Enums;

using System.Text.Json.Serialization;
using System.Runtime.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrateRole
{
    [EnumMember(Value = "Owner")] 
    Owner,

    [EnumMember(Value = "Editor")] 
    Editor,

    [EnumMember(Value = "Viewer")] 
    Viewer
}