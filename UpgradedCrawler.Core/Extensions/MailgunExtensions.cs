using Mailgun.Core.Messages;
using Mailgun.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UpgradedCrawler.Core.Extensions
{
    public static class MailgunExtensions
    {
        public static IMessageBuilder SetTemplate(this IMessageBuilder messageBuilder, string templateName, JObject templateData)
        {
            ThrowIf.IsArgumentNull(() => templateName);
            ThrowIf.IsArgumentNull(() => templateData);

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            };
            return messageBuilder.AddCustomParameter("template", templateName)
                               .AddCustomParameter("t:variables", JsonConvert.SerializeObject(templateData.ConvertToCamel(), settings));
        }
    }
}