-- AI面试功能 - 创建数据表
-- 在 (localdb)\MSSQLLocalDB 的 AIRecruitment 数据库中执行

USE AIRecruitment;
GO

-- 1. 创建 AI面试会话表
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AIInterviewSessions' AND xtype='U')
BEGIN
    CREATE TABLE AIInterviewSessions (
        SessionId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryId INT NULL,
        CandidateId INT NULL,
        JobId INT NULL,
        Status INT NOT NULL DEFAULT 0,
        StartTime DATETIME2 NULL,
        EndTime DATETIME2 NULL,
        TotalScore INT NULL,
        ScoresJson NVARCHAR(MAX) NULL,
        TranscriptJson NVARCHAR(MAX) NULL,
        TotalDuration INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_AIInterviewSessions_Deliveries FOREIGN KEY (DeliveryId) REFERENCES Deliveries(DeliveryId) ON DELETE NO ACTION,
        CONSTRAINT FK_AIInterviewSessions_Candidates FOREIGN KEY (CandidateId) REFERENCES Candidates(CandidateId) ON DELETE NO ACTION,
        CONSTRAINT FK_AIInterviewSessions_Jobs FOREIGN KEY (JobId) REFERENCES Jobs(JobId) ON DELETE NO ACTION
    );
    CREATE INDEX IX_AIInterviewSessions_DeliveryId ON AIInterviewSessions(DeliveryId);
    CREATE INDEX IX_AIInterviewSessions_CandidateId ON AIInterviewSessions(CandidateId);
    CREATE INDEX IX_AIInterviewSessions_JobId ON AIInterviewSessions(JobId);
    PRINT 'AIInterviewSessions 表创建成功';
END
ELSE
    PRINT 'AIInterviewSessions 表已存在';

-- 2. 创建 AI面试消息表
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AIInterviewMessages' AND xtype='U')
BEGIN
    CREATE TABLE AIInterviewMessages (
        MessageId INT IDENTITY(1,1) PRIMARY KEY,
        SessionId INT NOT NULL,
        Role NVARCHAR(20) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        MessageType NVARCHAR(50) NULL,
        Score INT NULL,
        Evaluation NVARCHAR(MAX) NULL,
        OrderIndex INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_AIInterviewMessages_Sessions FOREIGN KEY (SessionId) REFERENCES AIInterviewSessions(SessionId) ON DELETE CASCADE
    );
    CREATE INDEX IX_AIInterviewMessages_SessionId ON AIInterviewMessages(SessionId);
    CREATE INDEX IX_AIInterviewMessages_OrderIndex ON AIInterviewMessages(SessionId, OrderIndex);
    PRINT 'AIInterviewMessages 表创建成功';
END
ELSE
    PRINT 'AIInterviewMessages 表已存在';

PRINT 'AI面试数据表创建完成！';
GO
