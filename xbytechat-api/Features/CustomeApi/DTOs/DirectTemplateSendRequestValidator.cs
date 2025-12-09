using FluentValidation;

namespace xbytechat.api.Features.CustomeApi.DTOs
{
    public sealed class DirectTemplateSendRequestValidator : AbstractValidator<DirectTemplateSendRequest>
    {
        public DirectTemplateSendRequestValidator()
        {
            RuleFor(x => x.PhoneNumberId).NotEmpty().WithMessage("phoneNumberId is required.");
            RuleFor(x => x.To).NotEmpty().WithMessage("'to' (recipient) is required.");
            RuleFor(x => x.TemplateId).NotEmpty().WithMessage("templateId is required.");
            // videoUrl required only if template header == VIDEO (checked in service)
        }
    }
}
