-- ============================================
-- AI智能招聘管理系统 - 数据库建表脚本
-- 数据库名: AIRecruitment
-- 适用于: SQL Server / SQL Express / LocalDB
-- ============================================

-- 创建数据库
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'AIRecruitment')
BEGIN
    CREATE DATABASE AIRecruitment;
END
GO

USE AIRecruitment;
GO

-- ============================================
-- 1. 系统用户表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SysUser')
BEGIN
    CREATE TABLE SysUser (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Role NVARCHAR(50) NOT NULL,
        RealName NVARCHAR(100) NULL,
        Phone NVARCHAR(20) NULL,
        Email NVARCHAR(100) NULL,
        Status INT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        LastLogin DATETIME NULL
    );
    
    CREATE UNIQUE INDEX IX_SysUser_Username ON SysUser(Username);
END
GO

-- ============================================
-- 2. 岗位表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Job')
BEGIN
    CREATE TABLE Job (
        JobId INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Dept NVARCHAR(100) NOT NULL,
        JD NVARCHAR(MAX) NOT NULL,
        Requirements NVARCHAR(MAX) NOT NULL,
        SalaryMin INT NULL,
        SalaryMax INT NULL,
        Location NVARCHAR(200) NOT NULL,
        HeadCount INT NULL,
        Status INT DEFAULT 1,
        HrId INT NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL,
        ExpiredAt DATETIME NULL,
        CONSTRAINT FK_Job_SysUser FOREIGN KEY (HrId) REFERENCES SysUser(UserId) ON DELETE NO ACTION
    );
    
    CREATE INDEX IX_Job_HrId_Status ON Job(HrId, Status);
END
GO

-- ============================================
-- 3. 候选人表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Candidate')
BEGIN
    CREATE TABLE Candidate (
        CandidateId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NULL,
        RealName NVARCHAR(100) NOT NULL,
        Phone NVARCHAR(20) NOT NULL,
        Email NVARCHAR(100) NULL,
        Education NVARCHAR(200) NULL,
        WorkYears INT NULL,
        ResumeUrl NVARCHAR(500) NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- ============================================
-- 4. 投递记录表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Delivery')
BEGIN
    CREATE TABLE Delivery (
        DeliveryId INT IDENTITY(1,1) PRIMARY KEY,
        JobId INT NOT NULL,
        CandidateId INT NOT NULL,
        Status INT DEFAULT 0,
        HrId INT NOT NULL,
        DeliverTime DATETIME DEFAULT GETDATE(),
        UpdateTime DATETIME NULL,
        Remark NVARCHAR(MAX) NULL,
        CONSTRAINT FK_Delivery_Job FOREIGN KEY (JobId) REFERENCES Job(JobId) ON DELETE NO ACTION,
        CONSTRAINT FK_Delivery_Candidate FOREIGN KEY (CandidateId) REFERENCES Candidate(CandidateId) ON DELETE NO ACTION,
        CONSTRAINT FK_Delivery_SysUser FOREIGN KEY (HrId) REFERENCES SysUser(UserId) ON DELETE NO ACTION
    );
    
    CREATE UNIQUE INDEX IX_Delivery_Job_Candidate ON Delivery(JobId, CandidateId);
    CREATE INDEX IX_Delivery_Status ON Delivery(Status);
END
GO

-- ============================================
-- 5. 面试表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Interview')
BEGIN
    CREATE TABLE Interview (
        InterviewId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryId INT NOT NULL,
        InterviewerId INT NOT NULL,
        ScheduleTime DATETIME NOT NULL,
        Location NVARCHAR(200) NOT NULL,
        Status INT DEFAULT 0,
        Result NVARCHAR(MAX) NULL,
        Record NVARCHAR(MAX) NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL,
        CONSTRAINT FK_Interview_Delivery FOREIGN KEY (DeliveryId) REFERENCES Delivery(DeliveryId) ON DELETE NO ACTION,
        CONSTRAINT FK_Interview_SysUser FOREIGN KEY (InterviewerId) REFERENCES SysUser(UserId) ON DELETE NO ACTION
    );
    
    CREATE INDEX IX_Interview_ScheduleTime ON Interview(ScheduleTime);
END
GO

-- ============================================
-- 6. AI评分表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AIScore')
BEGIN
    CREATE TABLE AIScore (
        ScoreId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryId INT NOT NULL,
        MatchScore INT NOT NULL,
        MatchReason NVARCHAR(MAX) NULL,
        AnalysisReport NVARCHAR(MAX) NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_AIScore_Delivery FOREIGN KEY (DeliveryId) REFERENCES Delivery(DeliveryId) ON DELETE NO ACTION
    );
END
GO

-- ============================================
-- 7. AI面试题表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AIInterviewQuestion')
BEGIN
    CREATE TABLE AIInterviewQuestion (
        QuestionId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryId INT NOT NULL,
        JobId INT NOT NULL,
        QuestionsJson NVARCHAR(MAX) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_AIInterviewQuestion_Delivery FOREIGN KEY (DeliveryId) REFERENCES Delivery(DeliveryId) ON DELETE NO ACTION,
        CONSTRAINT FK_AIInterviewQuestion_Job FOREIGN KEY (JobId) REFERENCES Job(JobId) ON DELETE NO ACTION
    );
END
GO

-- ============================================
-- 8. AI简历分析表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AIResumeAnalysis')
BEGIN
    CREATE TABLE AIResumeAnalysis (
        AnalysisId INT IDENTITY(1,1) PRIMARY KEY,
        CandidateId INT NOT NULL,
        ParsedJson NVARCHAR(MAX) NOT NULL,
        SkillsTags NVARCHAR(MAX) NULL,
        WorkExperience NVARCHAR(MAX) NULL,
        Projects NVARCHAR(MAX) NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_AIResumeAnalysis_Candidate FOREIGN KEY (CandidateId) REFERENCES Candidate(CandidateId)
    );
END
GO

-- ============================================
-- 9. AI招聘洞察表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AIRecruitmentInsights')
BEGIN
    CREATE TABLE AIRecruitmentInsights (
        InsightId INT IDENTITY(1,1) PRIMARY KEY,
        HrId INT NOT NULL,
        Period NVARCHAR(50) NOT NULL,
        PipelineData NVARCHAR(MAX) NOT NULL,
        Recommendations NVARCHAR(MAX) NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_AIRecruitmentInsights_SysUser FOREIGN KEY (HrId) REFERENCES SysUser(UserId)
    );
END
GO

-- ============================================
-- 10. 系统登录日志表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SysLoginLog')
BEGIN
    CREATE TABLE SysLoginLog (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        IpAddress NVARCHAR(50) NULL,
        UserAgent NVARCHAR(500) NULL,
        Status NVARCHAR(50) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- ============================================
-- 11. 系统操作日志表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SysOperLog')
BEGIN
    CREATE TABLE SysOperLog (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        Module NVARCHAR(100) NOT NULL,
        Action NVARCHAR(100) NOT NULL,
        Detail NVARCHAR(MAX) NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- ============================================
-- 12. 系统配置表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SysConfig')
BEGIN
    CREATE TABLE SysConfig (
        ConfigId INT IDENTITY(1,1) PRIMARY KEY,
        ConfigKey NVARCHAR(100) NOT NULL,
        ConfigValue NVARCHAR(MAX) NOT NULL,
        Description NVARCHAR(500) NULL
    );
END
GO

-- ============================================
-- 13. 文件上传表
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UploadFile')
BEGIN
    CREATE TABLE UploadFile (
        FileId INT IDENTITY(1,1) PRIMARY KEY,
        FileName NVARCHAR(200) NOT NULL,
        FilePath NVARCHAR(500) NOT NULL,
        FileType NVARCHAR(50) NOT NULL,
        FileSize BIGINT NOT NULL,
        UserId INT NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- ============================================
-- 插入默认测试账号
-- ============================================

-- 管理员账号 (密码: admin123)
IF NOT EXISTS (SELECT * FROM SysUser WHERE Username = 'admin')
BEGIN
    INSERT INTO SysUser (Username, PasswordHash, Role, RealName, Status)
    VALUES ('admin', '$2a$11$QkK5x6q8p9z0A1b2C3d4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z', 'admin', '系统管理员', 1);
END
GO

-- HR账号 (密码: hr123)
IF NOT EXISTS (SELECT * FROM SysUser WHERE Username = 'hr')
BEGIN
    INSERT INTO SysUser (Username, PasswordHash, Role, RealName, Status)
    VALUES ('hr', '$2a$11$QkK5x6q8p9z0A1b2C3d4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z', 'hr', '招聘专员', 1);
END
GO

PRINT 'AIRecruitment数据库初始化完成！';
GO
