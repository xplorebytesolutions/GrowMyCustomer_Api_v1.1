using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace xbytechat.api.Infrastructure.Swagger
{
    /// <summary>
    /// Swagger fix: correctly documents endpoints that accept IFormFile (multipart/form-data),
    /// preventing Swagger generation failures and enabling "Choose File" UI.
    /// </summary>
    public sealed class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Find any IFormFile parameters on the action
            var fileParams = context.ApiDescription.ParameterDescriptions
                .Where(p => p.Type == typeof(IFormFile) || p.Type == typeof(IFormFileCollection))
                .ToList();

            if (fileParams.Count == 0)
                return;

            operation.RequestBody ??= new OpenApiRequestBody();
            operation.RequestBody.Required = true;

            var schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>(),
                Required = new HashSet<string>()
            };

            foreach (var p in fileParams)
            {
                // Swagger field name = parameter name (e.g., "file")
                var fieldName = string.IsNullOrWhiteSpace(p.Name) ? "file" : p.Name;

                if (p.Type == typeof(IFormFileCollection))
                {
                    schema.Properties[fieldName] = new OpenApiSchema
                    {
                        Type = "array",
                        Items = new OpenApiSchema { Type = "string", Format = "binary" }
                    };
                }
                else
                {
                    schema.Properties[fieldName] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    };
                }

                schema.Required.Add(fieldName);
            }

            operation.RequestBody.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType { Schema = schema }
            };
        }
    }
}
