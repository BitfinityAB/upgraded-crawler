using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace UpgradedCrawler.Core.Extensions;

public static class JsonExtensions
{
    public static JObject ConvertToCamel(this JObject jsonObject)
    {
        var settings = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });

        return JObject.FromObject(jsonObject.ToObject<ExpandoObject>()!, settings);
    }
}
