namespace xbytechat.api.Features.CRM.Interfaces
{
    public interface IContactTagService
    {
        Task<bool> RemoveTagFromContactAsync(Guid businessId, Guid contactId, Guid tagId);
    }
}
