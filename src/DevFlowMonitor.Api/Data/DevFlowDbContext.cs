using DevFlowMonitor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevFlowMonitor.Api.Data;

internal sealed class DevFlowDbContext(DbContextOptions<DevFlowDbContext> options) : DbContext(options)
{
    public DbSet<GitHubAccount> GitHubAccounts => Set<GitHubAccount>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Step> Steps => Set<Step>();
    public DbSet<TriggerEvent> Events => Set<TriggerEvent>();
    public DbSet<GitCommit> Commits => Set<GitCommit>();
    public DbSet<GitHubUser> Users => Set<GitHubUser>();
    public DbSet<Metric> Metrics => Set<Metric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("devflow");
        ConfigureAccounts(modelBuilder);
        ConfigureRepositories(modelBuilder);
        ConfigureWorkflows(modelBuilder);
        ConfigureRuns(modelBuilder);
        ConfigureAttempts(modelBuilder);
        ConfigureJobs(modelBuilder);
        ConfigureSteps(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureCommits(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureMetrics(modelBuilder);
    }

    private static void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GitHubAccount>();
        entity.ToTable("github_accounts");
        entity.HasKey(account => account.Id);
        entity.Property(account => account.Owner).HasMaxLength(100).IsRequired();
        entity.HasIndex(account => account.Owner).IsUnique();
    }

    private static void ConfigureRepositories(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Repository>();
        entity.ToTable("repositories");
        entity.HasKey(repository => repository.Id);
        entity.Property(repository => repository.Owner).HasMaxLength(100).IsRequired();
        entity.Property(repository => repository.Name).HasMaxLength(100).IsRequired();
        entity.Property(repository => repository.FullName).HasMaxLength(201).IsRequired();
        entity.Property(repository => repository.DefaultBranch).HasMaxLength(255).IsRequired();
        entity.HasIndex(repository => new { repository.AccountId, repository.ExternalId }).IsUnique();
        entity.HasIndex(repository => new { repository.AccountId, repository.FullName }).IsUnique();
        entity.HasOne(repository => repository.Account)
            .WithMany(account => account.Repositories)
            .HasForeignKey(repository => repository.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureWorkflows(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Workflow>();
        entity.ToTable("workflows");
        entity.HasKey(workflow => workflow.Id);
        entity.Property(workflow => workflow.Name).HasMaxLength(255).IsRequired();
        entity.Property(workflow => workflow.Path).HasMaxLength(500);
        entity.Property(workflow => workflow.State).HasMaxLength(30).IsRequired();
        entity.HasIndex(workflow => new { workflow.RepositoryId, workflow.ExternalId }).IsUnique();
        entity.HasOne(workflow => workflow.Repository)
            .WithMany(repository => repository.Workflows)
            .HasForeignKey(workflow => workflow.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureRuns(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkflowRun>();
        entity.ToTable("workflow_runs");
        entity.HasKey(run => run.Id);
        entity.Property(run => run.DisplayTitle).HasMaxLength(500).IsRequired();
        entity.Property(run => run.Branch).HasMaxLength(255).IsRequired();
        entity.Property(run => run.Status).HasMaxLength(30).IsRequired();
        entity.Property(run => run.Conclusion).HasMaxLength(30);
        entity.Property(run => run.HtmlUrl).HasMaxLength(1000);
        entity.HasIndex(run => new { run.WorkflowId, run.ExternalId }).IsUnique();
        entity.HasIndex(run => new { run.WorkflowId, run.StartedAt });
        entity.HasOne(run => run.Workflow)
            .WithMany(workflow => workflow.Runs)
            .HasForeignKey(run => run.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(run => run.Event)
            .WithMany(triggerEvent => triggerEvent.Runs)
            .HasForeignKey(run => run.EventId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(run => run.Commit)
            .WithMany(commit => commit.Runs)
            .HasForeignKey(run => run.CommitId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(run => run.Actor)
            .WithMany(user => user.Runs)
            .HasForeignKey(run => run.ActorId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAttempts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Attempt>();
        entity.ToTable("attempts");
        entity.HasKey(attempt => attempt.Id);
        entity.Property(attempt => attempt.Status).HasMaxLength(30).IsRequired();
        entity.Property(attempt => attempt.Conclusion).HasMaxLength(30);
        entity.HasIndex(attempt => new { attempt.WorkflowRunId, attempt.Number }).IsUnique();
        entity.HasOne(attempt => attempt.WorkflowRun)
            .WithMany(run => run.Attempts)
            .HasForeignKey(attempt => attempt.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureJobs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Job>();
        entity.ToTable("jobs");
        entity.HasKey(job => job.Id);
        entity.Property(job => job.Name).HasMaxLength(255).IsRequired();
        entity.Property(job => job.Status).HasMaxLength(30).IsRequired();
        entity.Property(job => job.Conclusion).HasMaxLength(30);
        entity.Property(job => job.RunnerName).HasMaxLength(255);
        entity.HasIndex(job => new { job.AttemptId, job.ExternalId }).IsUnique();
        entity.HasIndex(job => new { job.AttemptId, job.Name });
        entity.HasOne(job => job.Attempt)
            .WithMany(attempt => attempt.Jobs)
            .HasForeignKey(job => job.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureSteps(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Step>();
        entity.ToTable("steps");
        entity.HasKey(step => step.Id);
        entity.Property(step => step.Name).HasMaxLength(255).IsRequired();
        entity.Property(step => step.Status).HasMaxLength(30).IsRequired();
        entity.Property(step => step.Conclusion).HasMaxLength(30);
        entity.HasIndex(step => new { step.JobId, step.Number }).IsUnique();
        entity.HasOne(step => step.Job)
            .WithMany(job => job.Steps)
            .HasForeignKey(step => step.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TriggerEvent>();
        entity.ToTable("events");
        entity.HasKey(triggerEvent => triggerEvent.Id);
        entity.Property(triggerEvent => triggerEvent.Name).HasMaxLength(100).IsRequired();
        entity.HasIndex(triggerEvent => new { triggerEvent.RepositoryId, triggerEvent.Name }).IsUnique();
        entity.HasOne(triggerEvent => triggerEvent.Repository)
            .WithMany(repository => repository.Events)
            .HasForeignKey(triggerEvent => triggerEvent.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureCommits(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GitCommit>();
        entity.ToTable("commits");
        entity.HasKey(commit => commit.Id);
        entity.Property(commit => commit.Sha).HasMaxLength(64).IsRequired();
        entity.Property(commit => commit.Message).HasMaxLength(2000);
        entity.Property(commit => commit.AuthorName).HasMaxLength(255);
        entity.Property(commit => commit.AuthorEmail).HasMaxLength(320);
        entity.HasIndex(commit => new { commit.RepositoryId, commit.Sha }).IsUnique();
        entity.HasOne(commit => commit.Repository)
            .WithMany(repository => repository.Commits)
            .HasForeignKey(commit => commit.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GitHubUser>();
        entity.ToTable("users");
        entity.HasKey(user => user.Id);
        entity.Property(user => user.Login).HasMaxLength(100).IsRequired();
        entity.Property(user => user.DisplayName).HasMaxLength(255);
        entity.Property(user => user.AvatarUrl).HasMaxLength(1000);
        entity.Property(user => user.HtmlUrl).HasMaxLength(1000);
        entity.HasIndex(user => user.ExternalId).IsUnique();
        entity.HasIndex(user => user.Login);
    }

    private static void ConfigureMetrics(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Metric>();
        entity.ToTable("metrics");
        entity.HasKey(metric => metric.Id);
        entity.Property(metric => metric.ScopeKey).HasMaxLength(80).IsRequired();
        entity.Property(metric => metric.Kind).HasMaxLength(80).IsRequired();
        entity.Property(metric => metric.Dimension).HasMaxLength(320).IsRequired();
        entity.Property(metric => metric.Value).HasPrecision(18, 4);
        entity.HasIndex(metric => new
        {
            metric.AccountId,
            metric.ScopeKey,
            metric.PeriodStart,
            metric.PeriodEnd,
            metric.Kind,
            metric.Dimension
        }).IsUnique();
        entity.HasOne(metric => metric.Account)
            .WithMany(account => account.Metrics)
            .HasForeignKey(metric => metric.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(metric => metric.Repository)
            .WithMany(repository => repository.Metrics)
            .HasForeignKey(metric => metric.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(metric => metric.Workflow)
            .WithMany(workflow => workflow.Metrics)
            .HasForeignKey(metric => metric.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
