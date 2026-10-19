USE UWDecisionDb;
GO

-- reinsurer ids (demo values; a few are left empty)
UPDATE Customer SET ReinsurerId = v.ReinsurerId
  FROM Customer c
  JOIN (VALUES ('900000001','RE-DEMO-01'), ('900000002','RE-DEMO-01'), ('900000003','RE-DEMO-02'),
               ('900000005','RE-DEMO-02'), ('900000006','RE-DEMO-01'), ('900000007','RE-DEMO-03'))
       AS v (CustomerId, ReinsurerId) ON v.CustomerId = c.CustomerId;

-- start the demo customers from a clean state
UPDATE UWDecision
   SET DecisionId = NULL, ReasonId = NULL, ReasonText = NULL, DecisionSource = NULL, PostponePeriod = NULL,
       EMR = NULL, OccupationExtra = NULL, ResidentialExtra = NULL, FlatExtra = NULL, AgeExtra = NULL, Notes = NULL
 WHERE CustomerId LIKE '9000000%';

DELETE FROM UWGlobalDecision WHERE CustomerId LIKE '9000000%';

-- global decisions: one for base covers and one for riders, per customer
INSERT INTO UWGlobalDecision (CustomerId, CoverType, DecisionId, ReasonText)
SELECT v.CustomerId, v.CoverType, d.DecisionId, v.ReasonText
  FROM (VALUES
     ('900000001','BASE',  N'Accept at OR',      NULL),
     ('900000001','RIDER', N'Accept with Extra', N'Elevated blood sugar: loaded for critical illness risk'),
     ('900000002','BASE',  N'Accept with Extra', N'Pilot: aviation occupation loading'),
     ('900000002','RIDER', N'Accept with Extra', N'Pilot: aviation occupation loading'),
     ('900000003','BASE',  N'Decline',           N'Medical reports required for risk assessment were not submitted'),
     ('900000003','RIDER', N'Decline',           N'Medical reports required for risk assessment were not submitted'),
     ('900000004','BASE',  N'Requirements',      N'Latest ECG and income proof required before a decision'),
     ('900000005','BASE',  N'Accept with Extra', N'Resident outside India: residential loading applied'),
     ('900000006','BASE',  N'Accept with Extra', N'Impaired life: rated up in age'),
     ('900000007','BASE',  N'Accept at OR',      NULL),
     ('900000007','RIDER', N'Accept at OR',      NULL)
  ) AS v (CustomerId, CoverType, Decision, ReasonText)
  JOIN DecisionOption d ON d.DecisionName = v.Decision;

-- decisions on the individual covers (CoverId = policy number for a base cover, rider id for a rider)
UPDATE u
   SET DecisionId       = d.DecisionId,
       ReasonText       = v.Reason,
       DecisionSource   = v.Source,
       PostponePeriod   = v.PostponePeriod,
       EMR              = v.EMR,
       OccupationExtra  = v.OccupationExtra,
       ResidentialExtra = v.ResidentialExtra,
       FlatExtra        = v.FlatExtra,
       AgeExtra         = v.AgeExtra,
       Notes            = v.Notes
  FROM UWDecision u
  JOIN (VALUES
     -- CoverId, Decision, Reason, Source, PostponePeriod, EMR, OccupationExtra, ResidentialExtra, FlatExtra, AgeExtra, Notes
     -- A: base + three riders on one policy, and a second policy with its own decision
     ('9000000001', N'Accept at OR',      NULL, 'GLOBAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('DEMO-RID-1', N'Accept with Extra', N'Elevated blood sugar: loaded for critical illness risk', 'GLOBAL', NULL, 50, NULL, NULL, NULL, NULL, NULL),
     ('DEMO-RID-2', N'Accept at OR',      N'No hazardous occupation or sport declared; accident cover is standard', 'OVERRIDE', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('DEMO-RID-3', N'Decline',           N'Medical risk not acceptable due to elevated blood sugar levels', 'OVERRIDE', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('9000000002', N'Postpone',          N'Income proof not yet received', 'OVERRIDE', 6, NULL, NULL, NULL, NULL, NULL, N'Review when documents arrive'),
     -- B: hazardous occupation
     ('9000000003', N'Accept with Extra', N'Pilot: aviation occupation loading', 'GLOBAL', NULL, NULL, 2, NULL, NULL, NULL, NULL),
     ('DEMO-RID-4', N'Accept with Extra', N'Pilot: aviation occupation loading', 'GLOBAL', NULL, NULL, 4, NULL, NULL, NULL, NULL),
     -- C: medical decline, and a counter offer on the pension policy
     ('9000000004', N'Decline',           N'Medical reports required for risk assessment were not submitted', 'GLOBAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('DEMO-RID-5', N'Decline',           N'Medical reports required for risk assessment were not submitted', 'GLOBAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('9000000005', N'Counter Offer',     N'Sum assured reduced to 5,00,000 based on income documents', 'OVERRIDE', NULL, NULL, NULL, NULL, NULL, NULL, N'Customer to confirm revised terms'),
     -- D: documents pending
     ('9000000006', N'Requirements',      N'Latest ECG and income proof required before a decision', 'GLOBAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     -- E: lives abroad
     ('9000000007', N'Accept with Extra', N'Resident outside India: residential loading applied', 'GLOBAL', NULL, NULL, NULL, 3, NULL, NULL, NULL),
     ('9000000008', N'Reject',            N'Country of residence is outside the accepted territory list', 'OVERRIDE', NULL, NULL, NULL, NULL, NULL, NULL, N'Demo: closed case'),
     -- F: rated age; its rider has no decision yet
     ('9000000009', N'Accept with Extra', N'Impaired life: rated up in age', 'GLOBAL', NULL, NULL, NULL, NULL, NULL, 5, NULL),
     -- G: every rider has its own decision, but the customer still has a global rider decision (Accept at OR)
     ('9000000010', N'Accept at OR',      NULL, 'GLOBAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('DEMO-RID-7', N'Decline',           N'Raised liver function tests: medical risk not acceptable', 'OVERRIDE', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
     ('DEMO-RID-8', N'Accept with Extra', N'Part-time scuba diving: hazardous sport loading', 'OVERRIDE', NULL, NULL, NULL, NULL, 1.5, NULL, NULL)
  ) AS v (CoverId, Decision, Reason, Source, PostponePeriod, EMR, OccupationExtra, ResidentialExtra, FlatExtra, AgeExtra, Notes)
    ON v.CoverId = CASE WHEN u.CoverType = 'BASE' THEN u.PolicyNumber ELSE u.RiderId END
  JOIN DecisionOption d ON d.DecisionName = v.Decision;
GO