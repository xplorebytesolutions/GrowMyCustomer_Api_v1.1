using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CRM.Services
{
    public sealed class ContactProfileService : IContactProfileService
    {
        private readonly AppDbContext _db;

        public ContactProfileService(AppDbContext db) => _db = db;

        public async Task UpsertProfileNameAsync(
            Guid businessId,
            string phoneE164,
            string? profileName,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(phoneE164) || string.IsNullOrWhiteSpace(profileName))
                return;

            static string Digits(string s) => new string(s.Where(char.IsDigit).ToArray());
            var phoneDigits = Digits(phoneE164);
            var newName = profileName.Trim();
            var now = DateTime.UtcNow;

            // Try digits first; fall back to raw (handles legacy rows)
            var contact = await _db.Contacts.FirstOrDefaultAsync(
                c => c.BusinessId == businessId &&
                     (c.PhoneNumber == phoneDigits || c.PhoneNumber == phoneE164),
                ct);

            if (contact == null)
            {
                // Concurrency-safe create
                try
                {
                    _db.Contacts.Add(new Contact
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        PhoneNumber = phoneDigits,          // canonical = digits-only
                        Name = newName,                     // display fallback
                        ProfileName = newName,              // WA profile name
                        ProfileNameUpdatedAt = now,
                        CreatedAt = now,
                        LastContactedAt = now
                    });
                    await _db.SaveChangesAsync(ct);
                    return;
                }
                catch (DbUpdateException)
                {
                    // Someone else created it — refetch and continue as update
                    contact = await _db.Contacts.FirstOrDefaultAsync(
                        c => c.BusinessId == businessId && c.PhoneNumber == phoneDigits, ct);
                    if (contact == null) return;
                }
            }

            var anyChange = false;

            if (!string.Equals(contact.ProfileName, newName, StringComparison.Ordinal))
            {
                contact.ProfileName = newName;
                contact.ProfileNameUpdatedAt = now;
                anyChange = true;
            }

            // Backfill Name if empty/placeholder/phone
            if (string.IsNullOrWhiteSpace(contact.Name) ||
                contact.Name == "WhatsApp User" ||
                contact.Name == contact.PhoneNumber)
            {
                if (!string.Equals(contact.Name, newName, StringComparison.Ordinal))
                {
                    contact.Name = newName;
                    anyChange = true;
                }
            }

            if (anyChange)
            {
                contact.ProfileNameUpdatedAt = now;
                await _db.SaveChangesAsync(ct);
            }
        }


    }
}
