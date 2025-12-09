using xbytechat.api.WhatsAppSettings.DTOs;

public class TemplateForUIResponseDto
{
    public string Name { get; set; }
    public string Language { get; set; }
    public string Body { get; set; }

    // ✅ Correct naming for frontend
    public int ParametersCount { get; set; }

    public List<ButtonMetadataDto> ButtonParams { get; set; }
    public bool HasImageHeader { get; set; } // 🆕 Used to detect image templates

    public string? HeaderKind { get; set; }          // "text" | "image" | "video" | "document" | "none"
    public bool RequiresHeaderMediaUrl { get; set; } // true for image/video/document

}
