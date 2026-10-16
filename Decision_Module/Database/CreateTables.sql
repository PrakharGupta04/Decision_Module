USE UWDecisionDb;
GO

-- ===== SOURCE TABLES (loaded once from the sheets) =====
CREATE TABLE Customer (
    CustomerId     VARCHAR(20)   NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    CustomerName   NVARCHAR(150) NOT NULL,
    BenefitGroupId VARCHAR(20)   NULL,
    ReinsurerId    VARCHAR(20)   NULL
);

-- one row per policy, holding its base cover
CREATE TABLE Policy (
    PolicyNumber             VARCHAR(20)   NOT NULL CONSTRAINT PK_Policy PRIMARY KEY,
    CustomerId               VARCHAR(20)   NOT NULL CONSTRAINT FK_Policy_Customer REFERENCES Customer(CustomerId),
    MarketName               NVARCHAR(100) NOT NULL,
    BillingFrequency         INT           NULL,
    BenefitCode              VARCHAR(20)   NOT NULL,
    BenefitGroup             VARCHAR(20)   NULL,
    Bterm                    INT           NULL,   -- base term
    Bppt                     INT           NULL,   -- base premium paying term
    BenefitInstalmentPremium DECIMAL(18,2) NULL,
    BenefitSumAssured        DECIMAL(18,2) NULL
);

-- zero or more riders per policy
CREATE TABLE Rider (
    RiderId         VARCHAR(20)   NOT NULL CONSTRAINT PK_Rider PRIMARY KEY,
    PolicyNumber    VARCHAR(20)   NOT NULL CONSTRAINT FK_Rider_Policy REFERENCES Policy(PolicyNumber),
    BenefitCode     VARCHAR(20)   NOT NULL,
    RiderType       VARCHAR(50)   NOT NULL,   -- e.g. Critical_Illness
    RiderSumAssured DECIMAL(18,2) NULL,
    RiderPremium    DECIMAL(18,2) NULL,
    RiderTerm       INT           NULL,
    RiderPpt        INT           NULL
);

-- ===== MASTER TABLES (they fill the dropdowns) =====
CREATE TABLE DecisionOption (
    DecisionId   INT IDENTITY(1,1) CONSTRAINT PK_DecisionOption PRIMARY KEY,
    DecisionName NVARCHAR(50) NOT NULL CONSTRAINT UQ_DecisionOption_Name UNIQUE
);

CREATE TABLE DecisionReason (
    ReasonId   INT IDENTITY(1,1) CONSTRAINT PK_DecisionReason PRIMARY KEY,
    DecisionId INT NOT NULL CONSTRAINT FK_DecisionReason_Decision REFERENCES DecisionOption(DecisionId),
    ReasonText NVARCHAR(250) NOT NULL,
    CONSTRAINT UQ_DecisionReason_Text UNIQUE (DecisionId, ReasonText),
    CONSTRAINT UQ_DecisionReason_Pair UNIQUE (ReasonId, DecisionId)   -- target for the composite key below
);

-- ===== THE FINAL TABLE: one row per cover, policy details and decision together =====
CREATE TABLE UWDecision (
    UwId         INT IDENTITY(1,1) CONSTRAINT PK_UWDecision PRIMARY KEY,
    CustomerId   VARCHAR(20) NOT NULL CONSTRAINT FK_UW_Customer REFERENCES Customer(CustomerId),
    CoverType    VARCHAR(5)  NOT NULL,      -- 'BASE' or 'RIDER'
    PolicyNumber VARCHAR(20) NOT NULL,
    RiderId      VARCHAR(20) NULL,          -- empty for base rows
    RiderType    VARCHAR(50) NULL,          -- empty for base rows

    -- policy and cover details (copied from the source tables)
    MarketName               NVARCHAR(100) NOT NULL,
    BillingFrequency         INT           NULL,
    BenefitCode              VARCHAR(20)   NOT NULL,
    BenefitGroup             VARCHAR(20)   NULL,
    Bterm                    INT           NULL,
    Bppt                     INT           NULL,
    BenefitInstalmentPremium DECIMAL(18,2) NULL,
    BenefitSumAssured        DECIMAL(18,2) NULL,

    -- the decision (all empty until an underwriter decides)
    DecisionId       INT          NULL CONSTRAINT FK_UW_Decision REFERENCES DecisionOption(DecisionId),
    ReasonId         INT          NULL,
    DecisionSource   VARCHAR(8)   NULL CONSTRAINT CK_UW_Source CHECK (DecisionSource IN ('GLOBAL','OVERRIDE')),
    PostponePeriod   INT          NULL,
    EMR              DECIMAL(9,2) NULL,
    OccupationExtra  DECIMAL(9,2) NULL,
    ResidentialExtra DECIMAL(9,2) NULL,
    FlatExtra        DECIMAL(9,2) NULL,
    AgeExtra         DECIMAL(9,2) NULL,
    Notes            NVARCHAR(500) NULL,

    -- the reason must belong to the chosen decision
    CONSTRAINT FK_UW_Reason FOREIGN KEY (ReasonId, DecisionId)
        REFERENCES DecisionReason(ReasonId, DecisionId),
    -- a base row has no rider id, a rider row must have one
    CONSTRAINT CK_UW_Cover CHECK (
        (CoverType = 'BASE'  AND RiderId IS NULL) OR
        (CoverType = 'RIDER' AND RiderId IS NOT NULL)),
    -- a decision and its source always come together
    CONSTRAINT CK_UW_Decision CHECK (
        (DecisionId IS NULL AND DecisionSource IS NULL AND ReasonId IS NULL) OR
        (DecisionId IS NOT NULL AND DecisionSource IS NOT NULL))
);

-- fast lookup of one customer's covers, and fast global updates
CREATE INDEX IX_UW_Customer ON UWDecision(CustomerId, CoverType);
-- exactly one row per base cover and one per rider
CREATE UNIQUE INDEX UX_UW_Base  ON UWDecision(PolicyNumber) WHERE CoverType = 'BASE';
CREATE UNIQUE INDEX UX_UW_Rider ON UWDecision(RiderId)      WHERE CoverType = 'RIDER';
GO