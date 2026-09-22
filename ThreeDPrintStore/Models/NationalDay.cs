using System.Text.Json.Serialization;
namespace ThreeDPrintStore.Models
{
  public class NationalDay
  {
    [JsonPropertyName("date")]
  public DateTime Date { get; set; }
  [JsonPropertyName("name")]
  public string? Name { get; set; }
  [JsonPropertyName("image")]
  public string? Image { get; set; }
}
}