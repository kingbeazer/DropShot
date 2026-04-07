using Microsoft.AspNetCore.Identity;

namespace DropShot.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string DisplayName { get; set; } = "";

        public bool IsSubscribed { get; set; }
        public string? SubscriptionTier { get; set; }
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public string? PaypalSubscriptionId { get; set; }
        public string? PaypalPayerId { get; set; }
        public string? ProfileImagePath { get; set; }
    }
}
