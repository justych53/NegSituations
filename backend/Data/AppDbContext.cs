using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FailureRecord> FailureRecords => Set<FailureRecord>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Factor> Factors => Set<Factor>();
    public DbSet<ComparisonMatrix> ComparisonMatrices => Set<ComparisonMatrix>();
    public DbSet<ParticipantMatrix> ParticipantMatrices => Set<ParticipantMatrix>();
    public DbSet<FailureFactor> FailureFactors => Set<FailureFactor>();
    public DbSet<QuestionnaireAnswer> QuestionnaireAnswers => Set<QuestionnaireAnswer>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Участник привязан к отказу (1:N)
        modelBuilder.Entity<Participant>()
            .HasOne(p => p.FailureRecord)
            .WithMany(fr => fr.Participants)
            .HasForeignKey(p => p.FailureRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Матрица сравнения факторов
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

        // Матрица вины участников
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
        modelBuilder.Entity<FailureFactor>()
            .HasKey(ff => new { ff.FailureRecordId, ff.FactorId });

        modelBuilder.Entity<FailureFactor>()
            .HasOne(ff => ff.FailureRecord)
            .WithMany(fr => fr.FailureFactors)
            .HasForeignKey(ff => ff.FailureRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FailureFactor>()
            .HasOne(ff => ff.Factor)
            .WithMany()
            .HasForeignKey(ff => ff.FactorId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ParticipantMatrix>()
            .HasOne(pm => pm.Factor)
            .WithMany()
            .HasForeignKey(pm => pm.FactorId)
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
        modelBuilder.Entity<QuestionnaireAnswer>()
            .HasOne(qa => qa.Participant)
            .WithMany()
            .HasForeignKey(qa => qa.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FailureRecord>()
            .HasOne(fr => fr.CreatedBy)
            .WithMany(u => u.FailureRecords)
            .HasForeignKey(fr => fr.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}