using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataLabelProject.Application.DTOs.Consensus;

public class ConsensusCreateRequest
{
    [Required]
    public Guid TaskItemId { get; set; }

    [Required]
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    [JsonPropertyName("agreementScore")]
    public double? AgreementScore { get; set; }
}
