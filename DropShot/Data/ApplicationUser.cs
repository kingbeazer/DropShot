using Microsoft.AspNetCore.Identity;

namespace DropShot.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public bool IsSubscribed { get; set; }
        public string? SubscriptionTier { get; set; }
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public string? PaypalSubscriptionId { get; set; }
        public string? PaypalPayerId { get; set; }
    }
}
