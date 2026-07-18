using System.Text.Json.Serialization;

namespace DevFlowMonitor.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<PipelineStatus>))]
public enum PipelineStatus
{
    [JsonStringEnumMemberName("success")]
    Success,

    [JsonStringEnumMemberName("failed")]
    Failed,

    [JsonStringEnumMemberName("running")]
    Running,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled
}
