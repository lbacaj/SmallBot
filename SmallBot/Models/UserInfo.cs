using System.ComponentModel.DataAnnotations;

namespace SmallBot.Models
{
    public class UserInfo
    {
        public string Id { get; set; }
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }


        public string TwitterUrl { get; set; }

        public string LinkedInUrl { get; set; }

        public string Location { get; set; }

        public int? TotalNumberOfProjects { get; set; }

        public DateTime? JoinDate { get; set; }


    }
}
