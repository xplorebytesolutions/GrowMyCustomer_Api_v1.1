using CsvHelper.Configuration;

namespace xbytechat.api.Features.CRM.Dtos
{
    /// <summary>
    /// CSV mapping for ContactDto import.
    /// Keeps header matching flexible (multiple header aliases).
    /// Header normalization is applied in CsvConfiguration (not here).
    /// </summary>
    public sealed class ContactDtoCsvMap : ClassMap<ContactDto>
    {
        public ContactDtoCsvMap()
        {
            // Required core columns
            Map(m => m.Name).Name("name", "fullname", "contactname", "customername");
            Map(m => m.PhoneNumber).Name("phone", "phonenumber", "mobile", "mobilenumber", "whatsapp", "whatsappnumber");

            // Optional columns
            Map(m => m.Email).Name("email", "emailid").Optional();
            Map(m => m.Notes).Name("notes", "note", "remark", "remarks", "comment", "comments").Optional();

            // Optional but useful for CRM analytics
            Map(m => m.LeadSource).Name("leadsource", "lead source", "source", "lead", "leadorigin").Optional();

            // We are intentionally NOT importing Tags yet because Tags is a List<string>.
            // If you want later: we can add a converter for a comma-separated "Tags" column.
        }
    }
}


//using CsvHelper.Configuration;

//namespace xbytechat.api.Features.CRM.Dtos
//{
//    public class ContactDtoCsvMap : ClassMap<ContactDto>
//    {
//        public ContactDtoCsvMap()
//        {
//            Map(m => m.Name).Name("name", "Name", "full name");
//            Map(m => m.PhoneNumber).Name("phone", "Phone", "mobile", "mobile number");
//            Map(m => m.Email).Name("email", "Email").Optional();
//            Map(m => m.Notes).Name("notes", "Notes").Optional();
//        }
//    }
//}
