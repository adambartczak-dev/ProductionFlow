using Microsoft.AspNetCore.Identity;

namespace ProductionFlow.Api.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
    }

    public string DisplayName { get; set; } = string.Empty;

    public Guid? AssignedWorkCenterId { get; set; }
}