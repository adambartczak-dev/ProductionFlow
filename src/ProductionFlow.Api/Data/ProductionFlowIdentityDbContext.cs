using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Identity;

namespace ProductionFlow.Api.Data;

public sealed class ProductionFlowIdentityDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ProductionFlowIdentityDbContext(
        DbContextOptions<ProductionFlowIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(user => user.AssignedWorkCenterId)
                .IsRequired(false);

            entity.HasIndex(user => user.AssignedWorkCenterId);
        });
    }
}