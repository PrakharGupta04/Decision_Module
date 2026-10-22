
USE UWDecisionDb;
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

-- Global notes are separate from individual cover notes.
IF COL_LENGTH('dbo.UWGlobalDecision', 'Notes') IS NULL
BEGIN
    ALTER TABLE dbo.UWGlobalDecision
    ADD Notes NVARCHAR(500) NULL;
END;

-- Multiple main reasons for each individual cover decision.
IF OBJECT_ID('dbo.UWDecisionReason', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UWDecisionReason
    (
        UwId INT NOT NULL,
        ReasonId INT NOT NULL,

        CONSTRAINT PK_UWDecisionReason
            PRIMARY KEY (UwId, ReasonId),

        CONSTRAINT FK_UWDecisionReason_UWDecision
            FOREIGN KEY (UwId)
            REFERENCES dbo.UWDecision(UwId),

        CONSTRAINT FK_UWDecisionReason_DecisionReason
            FOREIGN KEY (ReasonId)
            REFERENCES dbo.DecisionReason(ReasonId)
    );
END;

-- Multiple main reasons for each global decision.
IF OBJECT_ID('dbo.UWGlobalReason', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UWGlobalReason
    (
        GlobalId INT NOT NULL,
        ReasonId INT NOT NULL,

        CONSTRAINT PK_UWGlobalReason
            PRIMARY KEY (GlobalId, ReasonId),

        CONSTRAINT FK_UWGlobalReason_UWGlobalDecision
            FOREIGN KEY (GlobalId)
            REFERENCES dbo.UWGlobalDecision(GlobalId),

        CONSTRAINT FK_UWGlobalReason_DecisionReason
            FOREIGN KEY (ReasonId)
            REFERENCES dbo.DecisionReason(ReasonId)
    );
END;

-- Preserve each existing individual main reason.
INSERT INTO dbo.UWDecisionReason (UwId, ReasonId)
SELECT u.UwId, u.ReasonId
FROM dbo.UWDecision AS u
WHERE u.ReasonId IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.UWDecisionReason AS r
      WHERE r.UwId = u.UwId
        AND r.ReasonId = u.ReasonId
  );

-- Preserve each existing global main reason.
INSERT INTO dbo.UWGlobalReason (GlobalId, ReasonId)
SELECT g.GlobalId, g.ReasonId
FROM dbo.UWGlobalDecision AS g
WHERE g.ReasonId IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.UWGlobalReason AS r
      WHERE r.GlobalId = g.GlobalId
        AND r.ReasonId = g.ReasonId
  );

COMMIT TRANSACTION;
GO

-- Verify the migration.
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'UWGlobalDecision'
  AND COLUMN_NAME = 'Notes';

SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('UWDecisionReason', 'UWGlobalReason');

SELECT COUNT(*) AS IndividualReasonsCopied
FROM dbo.UWDecisionReason;

SELECT COUNT(*) AS GlobalReasonsCopied
FROM dbo.UWGlobalReason;
GO
