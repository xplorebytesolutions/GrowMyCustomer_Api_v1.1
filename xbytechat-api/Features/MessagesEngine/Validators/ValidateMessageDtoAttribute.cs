using System;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.MessagesEngine.DTOs;

namespace xbytechat.api.Features.MessagesEngine.DTOs.Validation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ValidateMessageDtoAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not SendMessageDto dto)
                return ValidationResult.Success;

            switch (dto.MessageType)
            {
                case MessageTypeEnum.Text:
                    if (string.IsNullOrWhiteSpace(dto.TextContent))
                        return new ValidationResult("TextContent is required for text messages.", new[] { nameof(dto.TextContent) });
                    break;

                case MessageTypeEnum.Image:
                    if (string.IsNullOrWhiteSpace(dto.MediaUrl))
                        return new ValidationResult("MediaUrl is required for image messages.", new[] { nameof(dto.MediaUrl) });
                    break;

                case MessageTypeEnum.Template:
                    if (string.IsNullOrWhiteSpace(dto.TemplateName))
                        return new ValidationResult("TemplateName is required for template messages.", new[] { nameof(dto.TemplateName) });
                    break;

                case MessageTypeEnum.Cta:
                    if (dto.CtaButtons == null || dto.CtaButtons.Count == 0)
                        return new ValidationResult("CtaButtons is required for CTA messages.", new[] { nameof(dto.CtaButtons) });
                    break;
            }

            return ValidationResult.Success;
        }
    }
}
