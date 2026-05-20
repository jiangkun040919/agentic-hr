using Microsoft.Data.SqlClient;

namespace AIRecruitment.Api.Data;

/// <summary>
/// 启动时增量迁移：EF Core 不管理的索引、约束补充、旧数据回填。
/// 全部幂等（IF NOT EXISTS），失败不阻断启动。
/// </summary>
public static class DbInitializer
{
    public static void EnsureTables(IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=AIRecruitment;Trusted_Connection=True;TrustServerCertificate=True";

        try
        {
            using var conn = new SqlConnection(connStr);
            conn.Open();

            EnsureGraphSnapshotTable(conn);
            EnsureDeliveryForeignKeys(conn);
            BackfillDeliveryContactSnapshots(conn);

            Console.WriteLine("[Startup] 增量迁移检查完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] 增量迁移失败（不影响启动）: {ex.Message}");
        }
    }

    private static void EnsureGraphSnapshotTable(SqlConnection conn)
    {
        using var cmd = new SqlCommand(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='GraphSnapshot')
            BEGIN
                CREATE TABLE GraphSnapshot (
                    SnapshotId INT IDENTITY(1,1) PRIMARY KEY,
                    JobName NVARCHAR(200) NOT NULL,
                    SkillsJson NVARCHAR(MAX) NOT NULL DEFAULT '',
                    Period NVARCHAR(20) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
                );
                CREATE INDEX IX_GraphSnapshot_Period ON GraphSnapshot(Period);
                CREATE INDEX IX_GraphSnapshot_JobName ON GraphSnapshot(JobName);
            END
        ", conn);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureDeliveryForeignKeys(SqlConnection conn)
    {
        using var cmd = new SqlCommand(@"
            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name='FK_AIIntSess_Deliveries')
                ALTER TABLE AIInterviewSessions ADD CONSTRAINT FK_AIIntSess_Deliveries FOREIGN KEY (DeliveryId) REFERENCES Delivery(DeliveryId);
            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name='FK_AIIntSess_Candidates')
                ALTER TABLE AIInterviewSessions ADD CONSTRAINT FK_AIIntSess_Candidates FOREIGN KEY (CandidateId) REFERENCES Candidate(CandidateId);
            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name='FK_AIIntSess_Jobs')
                ALTER TABLE AIInterviewSessions ADD CONSTRAINT FK_AIIntSess_Jobs FOREIGN KEY (JobId) REFERENCES Job(JobId);
            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name='FK_AIIntMsg_Sessions')
                ALTER TABLE AIInterviewMessages ADD CONSTRAINT FK_AIIntMsg_Sessions FOREIGN KEY (SessionId) REFERENCES AIInterviewSessions(SessionId) ON DELETE CASCADE;
            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name='FK_Notification_SysUser')
                ALTER TABLE Notification ADD CONSTRAINT FK_Notification_SysUser FOREIGN KEY (UserId) REFERENCES SysUser(UserId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_AIIntSess_DeliveryId')
                CREATE INDEX IX_AIIntSess_DeliveryId ON AIInterviewSessions(DeliveryId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_AIIntSess_CandidateId')
                CREATE INDEX IX_AIIntSess_CandidateId ON AIInterviewSessions(CandidateId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_AIIntSess_JobId')
                CREATE INDEX IX_AIIntSess_JobId ON AIInterviewSessions(JobId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_AIIntMsg_SessionId')
                CREATE INDEX IX_AIIntMsg_SessionId ON AIInterviewMessages(SessionId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Notification_UserId')
                CREATE INDEX IX_Notification_UserId ON Notification(UserId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Notification_UserId_IsRead')
                CREATE INDEX IX_Notification_UserId_IsRead ON Notification(UserId, IsRead);
        ", conn);
        cmd.ExecuteNonQuery();
    }

    private static void BackfillDeliveryContactSnapshots(SqlConnection conn)
    {
        using var cmd = new SqlCommand(@"
            UPDATE d SET d.ContactName = ISNULL(c.RealName,''), d.ContactPhone = ISNULL(c.Phone,''),
                   d.ContactEmail = c.Email, d.ContactEducation = c.Education,
                   d.ContactWorkYears = c.WorkYears, d.ContactResumeUrl = c.ResumeUrl
            FROM Delivery d INNER JOIN Candidate c ON d.CandidateId = c.CandidateId
            WHERE (d.ContactPhone = '' OR d.ContactPhone IS NULL)
              AND EXISTS (SELECT * FROM sys.columns WHERE object_id=OBJECT_ID('Candidate') AND name='RealName');
        ", conn);
        cmd.ExecuteNonQuery();
    }
}
