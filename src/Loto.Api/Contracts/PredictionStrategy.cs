using System.Text.Json.Serialization;

namespace Loto.Api.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PredictionStrategy
{
    Uniform,
    FrequencyGlobal,
    FrequencyRecent,
    Cold,
    Cooccurrence
}
