using CsvHelper.Configuration;
using xbytechat.api.Features.CRM.Dtos;

public sealed class ContactDtoCsvMap : ClassMap<ContactDto>
{
    public ContactDtoCsvMap()
    {
        Map(m => m.Name).Name("Name");
        Map(m => m.PhoneNumber).Name("Phone");
        Map(m => m.Email).Name("Email");
        Map(m => m.LeadSource).Name("LeadSource");
        Map(m => m.Notes).Name("Notes");
    }
}
