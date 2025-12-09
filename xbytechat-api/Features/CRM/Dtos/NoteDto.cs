namespace xbytechat.api.Features.CRM.Dtos
{
    public class NoteDto
    {
        public Guid Id { get; set; }
        public Guid? ContactId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Source { get; set; }
        public string CreatedBy { get; set; }
        public bool IsPinned { get; set; }
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
    }
}
