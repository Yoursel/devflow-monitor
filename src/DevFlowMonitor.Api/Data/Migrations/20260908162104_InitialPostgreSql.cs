using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevFlowMonitor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "devflow");

            migrationBuilder.CreateTable(
                name: "github_accounts",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSynchronizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    Login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HtmlUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    DefaultBranch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repositories_github_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "devflow",
                        principalTable: "github_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commits",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AuthorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AuthorEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    AuthoredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commits_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "devflow",
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_events_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "devflow",
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflows_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "devflow",
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metrics",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metrics_github_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "devflow",
                        principalTable: "github_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_metrics_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "devflow",
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_metrics_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "devflow",
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_runs",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunNumber = table.Column<long>(type: "bigint", nullable: false),
                    DisplayTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Conclusion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HtmlUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_runs_commits_CommitId",
                        column: x => x.CommitId,
                        principalSchema: "devflow",
                        principalTable: "commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_workflow_runs_events_EventId",
                        column: x => x.EventId,
                        principalSchema: "devflow",
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_workflow_runs_users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "devflow",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_workflow_runs_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "devflow",
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Conclusion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attempts_workflow_runs_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalSchema: "devflow",
                        principalTable: "workflow_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Conclusion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RunnerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jobs_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "devflow",
                        principalTable: "attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "steps",
                schema: "devflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Conclusion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_steps_jobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "devflow",
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attempts_WorkflowRunId_Number",
                schema: "devflow",
                table: "attempts",
                columns: new[] { "WorkflowRunId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commits_RepositoryId_Sha",
                schema: "devflow",
                table: "commits",
                columns: new[] { "RepositoryId", "Sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_RepositoryId_Name",
                schema: "devflow",
                table: "events",
                columns: new[] { "RepositoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_github_accounts_Owner",
                schema: "devflow",
                table: "github_accounts",
                column: "Owner",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jobs_AttemptId_ExternalId",
                schema: "devflow",
                table: "jobs",
                columns: new[] { "AttemptId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jobs_AttemptId_Name",
                schema: "devflow",
                table: "jobs",
                columns: new[] { "AttemptId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_metrics_AccountId_ScopeKey_PeriodStart_PeriodEnd_Kind_Dimen~",
                schema: "devflow",
                table: "metrics",
                columns: new[] { "AccountId", "ScopeKey", "PeriodStart", "PeriodEnd", "Kind", "Dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metrics_RepositoryId",
                schema: "devflow",
                table: "metrics",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_metrics_WorkflowId",
                schema: "devflow",
                table: "metrics",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_repositories_AccountId_ExternalId",
                schema: "devflow",
                table: "repositories",
                columns: new[] { "AccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repositories_AccountId_FullName",
                schema: "devflow",
                table: "repositories",
                columns: new[] { "AccountId", "FullName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_steps_JobId_Number",
                schema: "devflow",
                table: "steps",
                columns: new[] { "JobId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_ExternalId",
                schema: "devflow",
                table: "users",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Login",
                schema: "devflow",
                table: "users",
                column: "Login");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_ActorId",
                schema: "devflow",
                table: "workflow_runs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_CommitId",
                schema: "devflow",
                table: "workflow_runs",
                column: "CommitId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_EventId",
                schema: "devflow",
                table: "workflow_runs",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_WorkflowId_ExternalId",
                schema: "devflow",
                table: "workflow_runs",
                columns: new[] { "WorkflowId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_WorkflowId_StartedAt",
                schema: "devflow",
                table: "workflow_runs",
                columns: new[] { "WorkflowId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_RepositoryId_ExternalId",
                schema: "devflow",
                table: "workflows",
                columns: new[] { "RepositoryId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metrics",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "steps",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "attempts",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "workflow_runs",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "commits",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "events",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "users",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "workflows",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "repositories",
                schema: "devflow");

            migrationBuilder.DropTable(
                name: "github_accounts",
                schema: "devflow");
        }
    }
}
