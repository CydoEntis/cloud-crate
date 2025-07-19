namespace CloudCrate.Domain.Enums;

using System.Text.Json.Serialization;
using System.Runtime.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrateRole
{
    Owner,
    Editor,
    Viewer
}