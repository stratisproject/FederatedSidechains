using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    public class ClosestHeightModel
    {
        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }
    }
}
