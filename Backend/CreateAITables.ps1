$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName "System.Data"

$connString = "Server=(localdb)\MSSQLLocalDB;Database=AIRecruitment;Trusted_Connection=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()

Write-Host "Connected to database: $($conn.Database)"

$sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AIInterviewSessions')
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
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    ALTER TABLE AIInterviewSessions ADD CONSTRAINT FK_AIIntSess_Deliveries FOREIGN KEY (DeliveryId) REFERENCES Deliveries(DeliveryId);
    ALTER TABLE AIInterviewSessions ADD CONSTRAINT FK_AIIntSess_Candidates FOREIGN KEY (CandidateId) REFERENCES Candidates(CandidateId);
    ALTER TABLE AIInterviewSessions ADD CONSTRAINT FK_AIIntSess_Jobs FOREIGN KEY (JobId) REFERENCES Jobs(JobId);
    CREATE INDEX IX_AIIntSess_DeliveryId ON AIInterviewSessions(DeliveryId);
    CREATE INDEX IX_AIIntSess_CandidateId ON AIInterviewSessions(CandidateId);
    CREATE INDEX IX_AIIntSess_JobId ON AIInterviewSessions(JobId);
    PRINT 'AIInterviewSessions table created'
END
ELSE
BEGIN
    PRINT 'AIInterviewSessions table already exists'
END
"@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.ExecuteNonQuery() | Out-Null

$sql2 = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AIInterviewMessages')
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
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    ALTER TABLE AIInterviewMessages ADD CONSTRAINT FK_AIIntMsg_Sessions FOREIGN KEY (SessionId) REFERENCES AIInterviewSessions(SessionId) ON DELETE CASCADE;
    CREATE INDEX IX_AIIntMsg_SessionId ON AIInterviewMessages(SessionId);
    CREATE INDEX IX_AIIntMsg_OrderIndex ON AIInterviewMessages(SessionId, OrderIndex);
    PRINT 'AIInterviewMessages table created'
END
ELSE
BEGIN
    PRINT 'AIInterviewMessages table already exists'
END
"@

$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = $sql2
$cmd2.ExecuteNonQuery() | Out-Null

$verifyCmd = $conn.CreateCommand()
$verifyCmd.CommandText = "SELECT name FROM sys.tables WHERE name LIKE 'AIInterview%'"
$reader = $verifyCmd.ExecuteReader()
Write-Host ""
Write-Host "Verification - AI Interview tables:"
while ($reader.Read()) { Write-Host "  - $($reader[0])" }
$reader.Close()

$conn.Close()
Write-Host ""
Write-Host "Done!"
