USE UWDecisionDb;
GO

CREATE TABLE Customer (
    CustomerId     VARCHAR(20)   NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    CustomerName   NVARCHAR(150) NOT NULL,
    BenefitGroupId VARCHAR(20)   NULL,
    ReinsurerId    VARCHAR(20)   NULL
);

CREATE TABLE Policy (
    PolicyNumber VARCHAR(20)   NOT NULL CONSTRAINT PK_Policy PRIMARY KEY,
    CustomerId   VARCHAR(20)   NOT NULL CONSTRAINT FK_Policy_Customer REFERENCES Customer(CustomerId),
    MarketName   NVARCHAR(100) NOT NULL,
    BillingFrequency         INT           NULL,
    BenefitCode              VARCHAR(20)   NOT NULL,
    BenefitGroup             VARCHAR(20)   NULL,
    Bterm                    INT           NULL,   
    Bppt                     INT           NULL,   
    BenefitInstalmentPremium DECIMAL(18,2) NULL,
    BenefitSumAssured        DECIMAL(18,2) NULL
);
CREATE INDEX IX_Policy_CustomerId ON Policy(CustomerId);

CREATE TABLE Rider (
    RiderId      VARCHAR(20) NOT NULL CONSTRAINT PK_Rider PRIMARY KEY,
    PolicyNumber VARCHAR(20) NOT NULL CONSTRAINT FK_Rider_Policy REFERENCES Policy(PolicyNumber),
    RiderType    VARCHAR(50) NOT NULL,
    RiderSumAssured DECIMAL(18,2) NULL,
    RiderPremium    DECIMAL(18,2) NULL,
    RiderTerm       INT           NULL,
    RiderPpt        INT           NULL
);
CREATE INDEX IX_Rider_PolicyNumber ON Rider(PolicyNumber);

CREATE TABLE DecisionOption (
    DecisionId   INT IDENTITY(1,1) CONSTRAINT PK_DecisionOption PRIMARY KEY,
    DecisionName NVARCHAR(50) NOT NULL CONSTRAINT UQ_DecisionOption_Name UNIQUE
);

CREATE TABLE DecisionReason (
    ReasonId   INT IDENTITY(1,1) CONSTRAINT PK_DecisionReason PRIMARY KEY,
    DecisionId INT NOT NULL CONSTRAINT FK_DecisionReason_Decision REFERENCES DecisionOption(DecisionId),
    ReasonText NVARCHAR(250) NOT NULL,
    CONSTRAINT UQ_DecisionReason_Text UNIQUE (DecisionId, ReasonText),
    CONSTRAINT UQ_DecisionReason_Pair UNIQUE (ReasonId, DecisionId)  
);

CREATE TABLE UnderwritingDecision (
    UwDecisionId INT IDENTITY(1,1) CONSTRAINT PK_UwDecision PRIMARY KEY,
    CustomerId   VARCHAR(20) NOT NULL CONSTRAINT FK_UwDecision_Customer REFERENCES dbo.Customer(CustomerId),
    CoverType    VARCHAR(5)  NOT NULL CONSTRAINT CK_UwDecision_CoverType CHECK (CoverType IN ('BASE','RIDER')),
    PolicyNumber VARCHAR(20) NULL CONSTRAINT FK_UwDecision_Policy REFERENCES Policy(PolicyNumber),
    RiderId      VARCHAR(20) NULL CONSTRAINT FK_UwDecision_Rider  REFERENCES Rider(RiderId),
    DecisionId   INT         NOT NULL CONSTRAINT FK_UwDecision_Decision REFERENCES DecisionOption(DecisionId),
    ReasonId     INT         NULL,
    PostponePeriod   INT          NULL,
    EMR              DECIMAL(9,2) NULL,
    OccupationExtra  DECIMAL(9,2) NULL,
    ResidentialExtra DECIMAL(9,2) NULL,
    FlatExtra        DECIMAL(9,2) NULL,
    AgeExtra         DECIMAL(9,2) NULL,
    Notes            NVARCHAR(500) NULL,
    -- the reason must belong to the chosen decision (skipped when ReasonId is NULL)
    CONSTRAINT FK_UwDecision_Reason FOREIGN KEY (ReasonId, DecisionId)
        REFERENCES DecisionReason(ReasonId, DecisionId),

    CONSTRAINT CK_UwDecision_Target CHECK (
        (CoverType = 'BASE'  AND RiderId IS NULL) OR
        (CoverType = 'RIDER' AND PolicyNumber IS NULL))
);

CREATE INDEX IX_UwDecision_CustomerId ON UnderwritingDecision(CustomerId);

CREATE UNIQUE INDEX UX_UwDecision_Global ON UnderwritingDecision(CustomerId, CoverType)
    WHERE PolicyNumber IS NULL AND RiderId IS NULL;

CREATE UNIQUE INDEX UX_UwDecision_BaseOverride  ON UnderwritingDecision(PolicyNumber) WHERE PolicyNumber IS NOT NULL;
CREATE UNIQUE INDEX UX_UwDecision_RiderOverride ON UnderwritingDecision(RiderId)      WHERE RiderId IS NOT NULL;
GO