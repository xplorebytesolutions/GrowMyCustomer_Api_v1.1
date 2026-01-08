using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.DTOs;

namespace xbytechat.api.Features.TemplateModule.Services;

public static class TemplateLibrarySeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var library = scope.ServiceProvider.GetRequiredService<ITemplateLibraryService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TemplateLibraryService>>();

        try
        {
            var request = new LibraryImportRequest
            {
                Items = new List<LibraryImportItem>
                {
                    // ==========================================
                    // SALON
                    // ==========================================
                    CreateItem("SALON", "Welcome_Gift", "MARKETING", "Welcome to [Salon Name]! 🌸 Get 15% OFF your first haircut when you book today.", true),
                    CreateItem("SALON", "Booking_Reminder", "UTILITY", "Hi {{1}}, just a reminder for your appointment tomorrow at {{2}}. See you soon! ✨"),
                    CreateItem("SALON", "Feedback_Request", "MARKETING", "How was your experience today? {{1}} Tap below to leave a review and get 5 points! ⭐"),
                    CreateItem("SALON", "Seasonal_Offer", "MARKETING", "Festive Glow! 🏮 Book a Facial & Mani-Pedi combo for just $99. Valid till Sunday.", true),
                    CreateItem("SALON", "New_Service_Alert", "MARKETING", "Introducing: Keratin Treatment! 💆‍♀️ Get smoother hair in 2 hours. Tap to view gallery."),
                    CreateItem("SALON", "Reactivation", "MARKETING", "We miss you! 💖 It's been a while. Click below to book your next session and get a free head massage."),

                    // ==========================================
                    // GYM
                    // ==========================================
                    CreateItem("GYM", "Member_Welcome", "MARKETING", "Welcome to the Tribe! 🏋️‍♂️ Hi {{1}}, your membership is active. Ready to crush your goals?", true),
                    CreateItem("GYM", "PT_Session_Confirm", "UTILITY", "Confirmation: Your Personal Training session with {{1}} is scheduled for {{2}}. Don't forget your towel!"),
                    CreateItem("GYM", "Renewal_Notice", "UTILITY", "Action Required: Your membership expires in 3 days. Renew now via the link below: {{1}}"),
                    CreateItem("GYM", "New_Class_Schedule", "MARKETING", "New Yoga & Zumba slots added! 🧘‍♀️ Check the updated schedule here: {{1}}", true),
                    CreateItem("GYM", "Guest_Pass", "MARKETING", "Fitness is better with friends! 👬 Show this message for a 1-day free Guest Pass for your buddy."),
                    CreateItem("GYM", "Holiday_Hours", "UTILITY", "Holiday Update: We are open 24/7 during the festive season. Stay fit! 💪"),

                    // ==========================================
                    // DOCTOR
                    // ==========================================
                    CreateItem("DOCTOR", "Appt_Confirmation", "UTILITY", "Your appointment with Dr. {{1}} is confirmed for {{2}}. Please arrive 15 mins early.", true),
                    CreateItem("DOCTOR", "Followup_Reminder", "UTILITY", "Hi {{1}}, it's time for your follow-up check. Tap to schedule a slot that works for you."),
                    CreateItem("DOCTOR", "Prescription_Ready", "UTILITY", "Your prescription is ready for pickup at {{1}}. Ref: {{2}}", true),
                    CreateItem("DOCTOR", "Health_Tip", "MARKETING", "Health Tip: Drinking 8 glasses of water a day boosts immunity. Stay hydrated! 💧"),
                    CreateItem("DOCTOR", "Clinic_Relocation", "UTILITY", "Important: We've moved! Our new clinic is at {{1}}. Open from Monday."),
                    CreateItem("DOCTOR", "Lab_Results", "UTILITY", "Your lab results are available on our portal. Access here: {{1}}. Login with DOB."),

                    // ==========================================
                    // RETAILER
                    // ==========================================
                    CreateItem("RETAILER", "Order_Confirmed", "UTILITY", "Success! Your order #{{1}} has been placed. We'll notify you when it ships. 📦", true),
                    CreateItem("RETAILER", "Shipping_Update", "UTILITY", "Great news! Your order {{1}} is out for delivery. Track it here: {{2}}"),
                    CreateItem("RETAILER", "Abandoned_Cart", "MARKETING", "Still thinking about it? 🤔 Use code SAVE10 to get 10% OFF the items in your cart!", true),
                    CreateItem("RETAILER", "Exclusive_Sale", "MARKETING", "VIP Early Access! 🛍️ Our Summer Sale starts now. Tap to shop before everyone else."),
                    CreateItem("RETAILER", "Delivery_Complete", "UTILITY", "Delivered! 🏠 Your package was dropped off at {{1}}. Enjoy your purchase!"),
                    CreateItem("RETAILER", "Product_Review", "MARKETING", "Your opinion matters! 🌟 How is your new {{1}}? Rate it and get a $5 voucher."),

                    // ==========================================
                    // MEDICAL
                    // ==========================================
                    CreateItem("MEDICAL", "Test_Appointment", "UTILITY", "Your {{1}} test is scheduled for tomorrow at {{2}}. Fasting required for 8 hours.", true),
                    CreateItem("MEDICAL", "Billing_Statement", "UTILITY", "Your monthly statement for {{1}} is ready. Pay securely here: {{2}}"),
                    CreateItem("MEDICAL", "Insurance_Update", "UTILITY", "Action Required: Please update your insurance details at our desk or via the link: {{1}}"),
                    CreateItem("MEDICAL", "Vaccination_Drive", "MARKETING", "Flu Season! 💉 Protect yourself and your family. Free vaccinations this Saturday.", true),
                    CreateItem("MEDICAL", "Patient_Portal", "UTILITY", "Account Created: Welcome to the Patient Portal. Set your password here: {{1}}"),
                    CreateItem("MEDICAL", "Emergency_Contact", "UTILITY", "Safety First: Update your emergency contact info to stay protected during your visit."),

                    // ==========================================
                    // HOSPITAL
                    // ==========================================
                    CreateItem("HOSPITAL", "Discharge_Notice", "UTILITY", "Discharge Process: Hi {{1}}, you are cleared for discharge at {{2}}. Safe travels home! 🏥", true),
                    CreateItem("HOSPITAL", "Visiting_Hours", "UTILITY", "Visitor Info: Visiting hours are 10 AM - 8 PM. Max 2 visitors per room at a time."),
                    CreateItem("HOSPITAL", "Patient_Education", "UTILITY", "Post-Op Guide: View your recovery instructions here: {{1}}. Call {{2}} for assistance."),
                    CreateItem("HOSPITAL", "Blood_Donation", "MARKETING", "Urgent: Blood donors needed for {{1}}. Save a life today at our blood bank.", true),
                    CreateItem("HOSPITAL", "Parking_Info", "UTILITY", "Free parking is available in the North Wing for patient families. Validation required."),
                    CreateItem("HOSPITAL", "Health_Webinar", "MARKETING", "Join our webinar on 'Heart Health' this Friday at 6 PM. Register here: {{1}}"),

                    // ==========================================
                    // REAL_ESTATE
                    // ==========================================
                    CreateItem("REAL_ESTATE", "Viewing_Confirmed", "UTILITY", "Viewing Scheduled: We'll see you at {{1}} tomorrow at {{2}}. See you there! 🔑", true),
                    CreateItem("REAL_ESTATE", "New_Listing", "MARKETING", "New Listing! 🏡 Stunning 3 BHK at {{1}}. Tap to view 3D tour and pricing.", true),
                    CreateItem("REAL_ESTATE", "Price_Drop", "MARKETING", "Price Slashed! 📉 The property at {{1}} is now available for just {{2}}. Don't miss out!"),
                    CreateItem("REAL_ESTATE", "Doc_Signature", "UTILITY", "Action Required: Please sign the property documents for {{1}} here: {{2}}"),
                    CreateItem("REAL_ESTATE", "Closing_Update", "UTILITY", "Exciting News! 🎊 The closing for {{1}} is scheduled for {{2}}. We're almost there!"),
                    CreateItem("REAL_ESTATE", "Client_Anniversary", "MARKETING", "Happy 1 Year in your new home! 🏠 Time flies. Hope you're making great memories!"),
                }
            };

            var result = await library.ImportAsync(request, false);
            if (result.Success)
            {
                logger.LogInformation("✅ Library Seeded: {Count} items added/updated.", result.TotalItems);
            }
            else
            {
                logger.LogWarning("⚠️ Library Seeding had errors. Check logs.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to seed template library.");
        }
    }

    private static LibraryImportItem CreateItem(string industry, string key, string category, string body, bool featured = false)
    {
        return new LibraryImportItem
        {
            Industry = industry,
            Key = key,
            Category = category,
            IsFeatured = featured,
            Variants = new List<LibraryImportVariant>
            {
                new LibraryImportVariant
                {
                    Language = "en_US",
                    HeaderType = "NONE",
                    BodyText = body,
                    Buttons = new List<LibraryImportButton>
                    {
                        new LibraryImportButton { Type = "QUICK_REPLY", Text = "Interested" },
                        new LibraryImportButton { Type = "QUICK_REPLY", Text = "Not now" }
                    }
                }
            }
        };
    }
}
