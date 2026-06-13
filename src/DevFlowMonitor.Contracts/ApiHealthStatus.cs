using System.Text.Json.Serialization;

namespace DevFlowMonitor.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<ApiHealthStatus>))]
public enum ApiHealthStatus
{
    [JsonStringEnumMemberName("healthy")]
    Healthy,

    [JsonStringEnumMemberName("degraded")]
    Degraded,

    [JsonStringEnumMemberName("unhealthy")]
    Unhealthy
}
