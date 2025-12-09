using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace xbytechat.api.Shared.utility
{
    public static class TemplateParameterHelper
    {
        // ✅ Used when parsing stored JSON parameters
        public static List<string> ParseTemplateParams(string? jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString)) return new List<string>();
            try
            {
                return JsonConvert.DeserializeObject<List<string>>(jsonString) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        // ✅ NEW: Fills {{1}}, {{2}} with parameter values
        public static string FillPlaceholders(string template, List<string> parameters)
        {
            if (string.IsNullOrWhiteSpace(template) || parameters == null || parameters.Count == 0)
                return template;

            // Replace {{1}}, {{2}} ... with values
            return Regex.Replace(template, @"\{\{(\d+)\}\}", match =>
            {
                var index = int.Parse(match.Groups[1].Value) - 1;
                return index >= 0 && index < parameters.Count ? parameters[index] : match.Value;
            });
        }
    }
}
