using System.Text.Json.Serialization;

namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class LibraryImportRequest
{
    [JsonPropertyName("items")]
    public List<LibraryImportItem> Items { get; set; } = new();
}

public sealed class LibraryImportItem
{
    [JsonPropertyName("industry")]
    public string Industry { get; set; } = default!; // required

    [JsonPropertyName("key")]
    public string Key { get; set; } = default!;      // required (unique per industry)

    [JsonPropertyName("category")]
    public string Category { get; set; } = default!; // required: UTILITY|MARKETING|AUTHENTICATION

    [JsonPropertyName("isFeatured")]
    public bool IsFeatured { get; set; }

    [JsonPropertyName("variants")]
    public List<LibraryImportVariant> Variants { get; set; } = new();
}

public sealed class LibraryImportVariant
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = default!;   // required (e.g., en_US)

    [JsonPropertyName("headerType")]
    public string? HeaderType { get; set; }            // NONE|TEXT|IMAGE|VIDEO|DOCUMENT

    [JsonPropertyName("headerText")]
    public string? HeaderText { get; set; }

    [JsonPropertyName("bodyText")]
    public string BodyText { get; set; } = default!;   // required

    [JsonPropertyName("footerText")]
    public string? FooterText { get; set; }

    // Buttons as simple POCOs (we serialize to JSON string)
    [JsonPropertyName("buttons")]
    public List<LibraryImportButton> Buttons { get; set; } = new();

    // Examples map for {{n}} placeholders
    [JsonPropertyName("examples")]
    public Dictionary<string, string>? Examples { get; set; }
}

public sealed class LibraryImportButton
{
    [JsonPropertyName("type")] public string Type { get; set; } = default!; // QUICK_REPLY|URL|PHONE
    [JsonPropertyName("text")] public string Text { get; set; } = default!;
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
}

public sealed class LibraryImportResult
{
    public bool Success { get; set; }
    public int TotalItems { get; set; }
    public int CreatedItems { get; set; }
    public int UpdatedItems { get; set; }
    public int CreatedVariants { get; set; }
    public int UpdatedVariants { get; set; }
    public List<LibraryImportError> Errors { get; set; } = new();
}

public sealed class LibraryImportError
{
    public int ItemIndex { get; set; }
    public int? VariantIndex { get; set; }
    public string Message { get; set; } = default!;
}
