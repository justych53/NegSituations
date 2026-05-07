using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FailureRecord> FailureRecords => Set<FailureRecord>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<FailureParticipant> FailureParticipants => Set<FailureParticipant>();
    public DbSet<Factor> Factors => Set<Factor>();
    public DbSet<ComparisonMatrix> ComparisonMatrices => Set<ComparisonMatrix>();
    public DbSet<ParticipantMatrix> ParticipantMatrices => Set<ParticipantMatrix>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FailureParticipant>()
            .HasKey(fp => new { fp.FailureRecordId, fp.ParticipantId });

        modelBuilder.Entity<FailureParticipant>()
            .HasOne(fp => fp.FailureRecord)
            .WithMany(fr => fr.FailureParticipants)
            .HasForeignKey(fp => fp.FailureRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FailureParticipant>()
            .HasOne(fp => fp.Participant)
            .WithMany(p => p.FailureParticipants)
            .HasForeignKey(fp => fp.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ComparisonMatrix>()
            .HasOne(cm => cm.FactorA)
            .WithMany()
            .HasForeignKey(cm => cm.FactorAId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComparisonMatrix>()
            .HasOne(cm => cm.FactorB)
            .WithMany()
            .HasForeignKey(cm => cm.FactorBId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ParticipantMatrix>()
            .HasOne(pm => pm.ParticipantA)
            .WithMany()
            .HasForeignKey(pm => pm.ParticipantAId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ParticipantMatrix>()
            .HasOne(pm => pm.ParticipantB)
            .WithMany()
            .HasForeignKey(pm => pm.ParticipantBId)
            .OnDelete(DeleteBehavior.Cascade);
            }
}