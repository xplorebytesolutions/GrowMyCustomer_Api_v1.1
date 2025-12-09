using CsvHelper.Configuration;

namespace xbytechat.api.Features.CRM.Dtos
{
    public class ContactDtoCsvMap : ClassMap<ContactDto>
    {
        public ContactDtoCsvMap()
        {
            Map(m => m.Name).Name("name", "Name", "full name");
            Map(m => m.PhoneNumber).Name("phone", "Phone", "mobile", "mobile number");
            Map(m => m.Email).Name("email", "Email").Optional();
            Map(m => m.Notes).Name("notes", "Notes").Optional();
        }
    }
}
