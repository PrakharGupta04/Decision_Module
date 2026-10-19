USE UWDecisionDb;
GO

-- 1. a typed reason on every cover row
IF COL_LENGTH('UWDecision', 'ReasonText') IS NULL
    ALTER TABLE UWDecision ADD ReasonText NVARCHAR(500) NULL;
GO

-- 2. keep reasons already saved: copy the master text (skip reasons that only repeat the decision name)
UPDATE u
   SET u.ReasonText = r.ReasonText
  FROM UWDecision u
  JOIN DecisionReason r ON r.ReasonId = u.ReasonId
  JOIN DecisionOption d ON d.DecisionId = u.DecisionId
 WHERE u.ReasonText IS NULL AND r.ReasonText <> d.DecisionName;
GO

-- 3. the customer's global decision, stored once per cover type
IF OBJECT_ID('UWGlobalDecision', 'U') IS NULL
    CREATE TABLE UWGlobalDecision (
        CustomerId VARCHAR(20)   NOT NULL CONSTRAINT FK_UWGlobal_Customer REFERENCES Customer(CustomerId),
        CoverType  VARCHAR(5)    NOT NULL CONSTRAINT CK_UWGlobal_Type CHECK (CoverType IN ('BASE', 'RIDER')),
        DecisionId INT           NOT NULL CONSTRAINT FK_UWGlobal_Decision REFERENCES DecisionOption(DecisionId),
        ReasonText NVARCHAR(500) NULL,
        CONSTRAINT PK_UWGlobal PRIMARY KEY (CustomerId, CoverType)
    );
GO

-- 4. fill it from the global decisions already on the cover rows
INSERT INTO UWGlobalDecision (CustomerId, CoverType, DecisionId, ReasonText)
SELECT u.CustomerId, u.CoverType, u.DecisionId, u.ReasonText
  FROM UWDecision u
 WHERE u.DecisionSource = 'GLOBAL'
   AND u.UwId = (SELECT MIN(x.UwId) FROM UWDecision x
                  WHERE x.CustomerId = u.CustomerId AND x.CoverType = u.CoverType AND x.DecisionSource = 'GLOBAL')
   AND NOT EXISTS (SELECT 1 FROM UWGlobalDecision g
                    WHERE g.CustomerId = u.CustomerId AND g.CoverType = u.CoverType);
GO

-- 5. a reason is required for Decline and Reject
INSERT INTO DecisionField (DecisionId, FieldName)
SELECT d.DecisionId, 'ReasonRequired'
  FROM DecisionOption d
 WHERE d.DecisionName IN (N'Decline', N'Reject')
   AND NOT EXISTS (SELECT 1 FROM DecisionField f WHERE f.DecisionId = d.DecisionId AND f.FieldName = 'ReasonRequired');
GO