USE UWDecisionDb;
GO

-- Safety copy of the cover rows (drop it yourself when you are happy with the new design)
IF OBJECT_ID('UWDecision_BeforeRedesign') IS NULL
    SELECT * INTO UWDecision_BeforeRedesign FROM UWDecision;
GO

-- Lookup tables
IF OBJECT_ID('RiderType') IS NULL
    CREATE TABLE RiderType (
        RiderType     VARCHAR(50)   NOT NULL CONSTRAINT PK_RiderType PRIMARY KEY,
        RiderTypeName NVARCHAR(100) NOT NULL
    );

IF OBJECT_ID('FrequencyCode') IS NULL
    CREATE TABLE FrequencyCode (
        Code  VARCHAR(5)   NOT NULL CONSTRAINT PK_FrequencyCode PRIMARY KEY,
        Label NVARCHAR(50) NOT NULL
    );
GO

INSERT INTO RiderType (RiderType, RiderTypeName)
SELECT v.Code, v.Name
  FROM (VALUES ('Critical_Illness',  N'Critical Illness'),
               ('Accidental_Death',  N'Accidental Death Benefit'),
               ('Waiver_of_Premium', N'Waiver of Premium')) AS v (Code, Name)
 WHERE NOT EXISTS (SELECT 1 FROM RiderType t WHERE t.RiderType = v.Code);

INSERT INTO FrequencyCode (Code, Label)
SELECT v.Code, v.Label
  FROM (VALUES ('A', N'Annual'), ('Y', N'Annual'),
               ('S', N'Semi-annual'), ('H', N'Semi-annual'),
               ('Q', N'Quarterly'), ('M', N'Monthly'), ('W', N'Weekly'),
               ('I', N'Single pay'), ('SNG', N'Single pay')) AS v (Code, Label)
 WHERE NOT EXISTS (SELECT 1 FROM FrequencyCode f WHERE f.Code = v.Code);
GO

-- New policy-level columns
IF COL_LENGTH('Policy', 'ReinsurerId') IS NULL
    ALTER TABLE Policy ADD ReinsurerId VARCHAR(20) NULL, BaseCoverId VARCHAR(20) NULL,
                           BaseCoverType NVARCHAR(50) NULL, PaymentFrequency VARCHAR(5) NULL;

IF COL_LENGTH('UWDecision', 'ReinsurerId') IS NULL
    ALTER TABLE UWDecision ADD ReinsurerId VARCHAR(20) NULL, BaseCoverId VARCHAR(20) NULL,
                               BaseCoverType NVARCHAR(50) NULL, PaymentFrequency VARCHAR(5) NULL;
GO

IF OBJECT_ID('FK_Policy_Frequency', 'F') IS NULL
    ALTER TABLE Policy ADD CONSTRAINT FK_Policy_Frequency
        FOREIGN KEY (PaymentFrequency) REFERENCES FrequencyCode(Code);
IF OBJECT_ID('FK_UW_Frequency', 'F') IS NULL
    ALTER TABLE UWDecision ADD CONSTRAINT FK_UW_Frequency
        FOREIGN KEY (PaymentFrequency) REFERENCES FrequencyCode(Code);
IF OBJECT_ID('FK_Rider_RiderType', 'F') IS NULL
    ALTER TABLE Rider ADD CONSTRAINT FK_Rider_RiderType
        FOREIGN KEY (RiderType) REFERENCES RiderType(RiderType);
IF OBJECT_ID('FK_UW_RiderType', 'F') IS NULL
    ALTER TABLE UWDecision ADD CONSTRAINT FK_UW_RiderType
        FOREIGN KEY (RiderType) REFERENCES RiderType(RiderType);
GO

-- Reasons, sub-reasons and global decisions.
-- The first time only: old decisions, typed reasons and the old reason list are discarded.
IF OBJECT_ID('ReasonCover') IS NULL
BEGIN
    UPDATE UWDecision
       SET DecisionId = NULL, ReasonId = NULL, DecisionSource = NULL, PostponePeriod = NULL,
           EMR = NULL, OccupationExtra = NULL, ResidentialExtra = NULL, FlatExtra = NULL,
           AgeExtra = NULL, Notes = NULL;

    DELETE FROM DecisionReason;
    DROP TABLE IF EXISTS UWGlobalDecision;

    -- which cover kinds ('BASE' or a rider type) a reason applies to
    CREATE TABLE ReasonCover (
        ReasonId  INT         NOT NULL CONSTRAINT FK_ReasonCover_Reason REFERENCES DecisionReason(ReasonId),
        CoverKind VARCHAR(50) NOT NULL,
        CONSTRAINT PK_ReasonCover PRIMARY KEY (ReasonId, CoverKind)
    );

    CREATE TABLE SubReason (
        SubReasonId   INT IDENTITY(1,1) CONSTRAINT PK_SubReason PRIMARY KEY,
        ReasonId      INT           NOT NULL CONSTRAINT FK_SubReason_Reason REFERENCES DecisionReason(ReasonId),
        SubReasonText NVARCHAR(200) NOT NULL,
        CONSTRAINT UQ_SubReason UNIQUE (ReasonId, SubReasonText)
    );

    -- the sub-reasons chosen for a cover
    CREATE TABLE UWDecisionSubReason (
        UwId        INT NOT NULL CONSTRAINT FK_UWSub_Cover REFERENCES UWDecision(UwId),
        SubReasonId INT NOT NULL CONSTRAINT FK_UWSub_SubReason REFERENCES SubReason(SubReasonId),
        CONSTRAINT PK_UWDecisionSubReason PRIMARY KEY (UwId, SubReasonId)
    );

    -- one global decision per customer and cover kind; IsOn = 0 means no global decision
    CREATE TABLE UWGlobalDecision (
        GlobalId   INT IDENTITY(1,1) CONSTRAINT PK_UWGlobal PRIMARY KEY,
        CustomerId VARCHAR(20) NOT NULL CONSTRAINT FK_UWGlobal_Customer REFERENCES Customer(CustomerId),
        CoverKind  VARCHAR(50) NOT NULL,
        IsOn       BIT         NOT NULL CONSTRAINT DF_UWGlobal_IsOn DEFAULT 0,
        DecisionId INT         NULL CONSTRAINT FK_UWGlobal_Decision REFERENCES DecisionOption(DecisionId),
        ReasonId   INT         NULL,
        CONSTRAINT UQ_UWGlobal UNIQUE (CustomerId, CoverKind),
        CONSTRAINT FK_UWGlobal_Reason FOREIGN KEY (ReasonId, DecisionId)
            REFERENCES DecisionReason(ReasonId, DecisionId)
    );

    CREATE TABLE UWGlobalSubReason (
        GlobalId    INT NOT NULL CONSTRAINT FK_UWGlobalSub_Global REFERENCES UWGlobalDecision(GlobalId),
        SubReasonId INT NOT NULL CONSTRAINT FK_UWGlobalSub_SubReason REFERENCES SubReason(SubReasonId),
        CONSTRAINT PK_UWGlobalSubReason PRIMARY KEY (GlobalId, SubReasonId)
    );
END
GO

-- Clean-up of the old structure
IF OBJECT_ID('CK_UW_Decision') IS NOT NULL
    ALTER TABLE UWDecision DROP CONSTRAINT CK_UW_Decision;

IF COL_LENGTH('UWDecision', 'ReasonText') IS NOT NULL
    ALTER TABLE UWDecision DROP COLUMN ReasonText;

DELETE FROM DecisionField WHERE FieldName = 'ReasonRequired';
GO