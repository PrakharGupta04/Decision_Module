USE UWDecisionDb;
GO

-- which detail fields appear for which decision
CREATE TABLE DecisionField (
    DecisionId INT         NOT NULL CONSTRAINT FK_DecisionField_Decision REFERENCES DecisionOption(DecisionId),
    FieldName  VARCHAR(30) NOT NULL,   -- 'Extras' or 'PostponePeriod'
    CONSTRAINT PK_DecisionField PRIMARY KEY (DecisionId, FieldName)
);

-- which extras apply to which kind of cover ('BASE' or a rider type)
CREATE TABLE CoverExtra (
    CoverKind VARCHAR(50) NOT NULL,
    FieldName VARCHAR(30) NOT NULL,
    CONSTRAINT PK_CoverExtra PRIMARY KEY (CoverKind, FieldName)
);
GO

INSERT INTO DecisionField (DecisionId, FieldName)
SELECT d.DecisionId, v.FieldName
FROM (VALUES (N'Accept with Extra', 'Extras'),
             (N'Postpone',          'PostponePeriod')) AS v(DecisionName, FieldName)
JOIN DecisionOption d ON d.DecisionName = v.DecisionName;

INSERT INTO CoverExtra (CoverKind, FieldName) VALUES
 ('BASE', 'EMR'), ('BASE', 'OccupationExtra'), ('BASE', 'ResidentialExtra'), ('BASE', 'FlatExtra'), ('BASE', 'AgeExtra'),
 ('Critical_Illness', 'EMR'),
 ('Accidental_Death', 'OccupationExtra'), ('Accidental_Death', 'ResidentialExtra'), ('Accidental_Death', 'FlatExtra');
-- Waiver_of_Premium has no rows, so no extras appear for it
GO