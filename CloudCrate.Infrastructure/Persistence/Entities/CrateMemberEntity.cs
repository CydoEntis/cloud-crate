using CloudCrate.Domain.Enums;
using CloudCrate.Infrastructure.Identity;

namespace CloudCrate.Infrastructure.Persistence.Entities;
    public class CrateMemberEntity
    {
        public Guid Id { get; set; }
        public Guid CrateId { get; set; }
        public CrateEntity Crate { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!; 

        public CrateRole Role { get; set; }

        public DateTime JoinedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
