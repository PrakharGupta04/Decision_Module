USE UWDecisionDb;

DROP TABLE IF EXISTS #Global;
DROP TABLE IF EXISTS #Cover;

-- start the demo customers from a clean state
DELETE FROM UWDecisionSubReason WHERE UwId IN (SELECT UwId FROM UWDecision WHERE CustomerId LIKE '9000000%');
DELETE FROM UWGlobalSubReason   WHERE GlobalId IN (SELECT GlobalId FROM UWGlobalDecision WHERE CustomerId LIKE '9000000%');
DELETE FROM UWGlobalDecision    WHERE CustomerId LIKE '9000000%';
UPDATE UWDecision
   SET DecisionId = NULL, ReasonId = NULL, DecisionSource = NULL, PostponePeriod = NULL,
       EMR = NULL, OccupationExtra = NULL, ResidentialExtra = NULL, FlatExtra = NULL, AgeExtra = NULL, Notes = NULL
 WHERE CustomerId LIKE '9000000%';

-- global decisions: customer, cover kind, on/off, decision, reason, sub-reasons (separated by |)
CREATE TABLE #Global (CustomerId VARCHAR(20), Kind VARCHAR(50), IsOn BIT,
                      Decision NVARCHAR(50), Reason NVARCHAR(250), Subs NVARCHAR(500));
INSERT INTO #Global VALUES
 ('900000001', 'BASE',              1, N'Accept at OR',      NULL, NULL),
 ('900000001', 'Critical_Illness',  1, N'Accept with Extra', N'Medical loading', N'Elevated blood sugar levels'),
 ('900000001', 'Accidental_Death',  1, N'Accept at OR',      NULL, NULL),
 ('900000001', 'Waiver_of_Premium', 1, N'Accept at OR',      NULL, NULL),
 ('900000002', 'BASE',              1, N'Accept with Extra', N'Occupational hazard', N'Aviation or aircrew'),
 ('900000002', 'Accidental_Death',  1, N'Accept with Extra', N'Occupational hazard', N'Aviation or aircrew'),
 ('900000003', 'BASE',              1, N'Decline', N'Medical risk not acceptable', N'Elevated blood sugar levels|Raised Liver function Tests'),
 ('900000003', 'Critical_Illness',  1, N'Decline', N'Medical risk not acceptable', N'Elevated blood sugar levels|Raised Liver function Tests'),
 ('900000004', 'BASE',              1, N'Requirements', N'Additional medical evidence required', N'ECG|Blood profile'),
 ('900000005', 'BASE',              1, N'Accept with Extra', N'Residence or travel risk', N'Resident outside India'),
 ('900000006', 'BASE',              1, N'Accept with Extra', N'Age rating', NULL),
 ('900000006', 'Critical_Illness',  0, NULL, NULL, NULL),
 ('900000007', 'BASE',              1, N'Accept at OR', NULL, NULL),
 ('900000007', 'Critical_Illness',  1, N'Accept at OR', NULL, NULL),
 ('900000007', 'Accidental_Death',  1, N'Accept at OR', NULL, NULL);

-- covers: policy number (base cover) or rider id, source, decision details for overrides, then the extras
CREATE TABLE #Cover (CoverId VARCHAR(20), Source VARCHAR(8), Decision NVARCHAR(50), Reason NVARCHAR(250),
                     Subs NVARCHAR(500), PostponePeriod INT, EMR DECIMAL(9,2), OccupationExtra DECIMAL(9,2),
                     ResidentialExtra DECIMAL(9,2), FlatExtra DECIMAL(9,2), AgeExtra DECIMAL(9,2), Notes NVARCHAR(500));
INSERT INTO #Cover VALUES
 -- customer A: base and three riders on one policy, a second policy with its own decision
 ('9000000001', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-RID-1', 'GLOBAL',   NULL, NULL, NULL, NULL, 150,  NULL, NULL, NULL, NULL, NULL),
 ('DEMO-RID-2', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-RID-3', 'OVERRIDE', N'Decline', N'Medical risk not acceptable', N'Elevated blood sugar levels', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('9000000002', 'OVERRIDE', N'Postpone', N'Financial documents pending', N'Income proof awaited', 6, NULL, NULL, NULL, NULL, NULL, N'Review when income proof arrives'),
 -- customer B: hazardous occupation
 ('9000000003', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, 2,    NULL, NULL, NULL, NULL),
 ('DEMO-RID-4', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, 4,    NULL, NULL, NULL, NULL),
 -- customer C: medical decline, counter offer on the pension policy
 ('9000000004', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-RID-5', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('9000000005', 'OVERRIDE', N'Counter Offer', N'Reduced sum assured', NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'Customer to confirm the reduced sum assured'),
 -- customer D: evidence requested
 ('9000000006', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'Evidence requested by letter'),
 -- customer E: lives abroad
 ('9000000007', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, 3,    NULL, NULL, NULL),
 ('9000000008', 'OVERRIDE', N'Reject', N'Outside territorial limits', NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'Demo: closed case'),
 -- customer F: rated age; the critical illness rider has no global decision and no decision yet
 ('9000000009', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5,    NULL),
 ('DEMO-RID-6', 'OVERRIDE', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 -- customer G: every rider overridden while the global decisions stay on
 ('9000000010', 'GLOBAL',   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-RID-7', 'OVERRIDE', N'Decline', N'Medical risk not acceptable', N'Raised Liver function Tests', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
 ('DEMO-RID-8', 'OVERRIDE', N'Accept with Extra', N'Hazardous pursuit', N'Scuba diving', NULL, NULL, NULL, NULL, 1.5, NULL, NULL);

-- global decisions
INSERT INTO UWGlobalDecision (CustomerId, CoverKind, IsOn, DecisionId, ReasonId)
SELECT g.CustomerId, g.Kind, g.IsOn, d.DecisionId, r.ReasonId
  FROM #Global g
  LEFT JOIN DecisionOption d ON d.DecisionName = g.Decision
  LEFT JOIN DecisionReason r ON r.DecisionId = d.DecisionId AND r.ReasonText = g.Reason;

INSERT INTO UWGlobalSubReason (GlobalId, SubReasonId)
SELECT ug.GlobalId, sr.SubReasonId
  FROM #Global g
  JOIN UWGlobalDecision ug ON ug.CustomerId = g.CustomerId AND ug.CoverKind = g.Kind
  JOIN DecisionReason r ON r.ReasonText = g.Reason
 CROSS APPLY STRING_SPLIT(g.Subs, '|') s
  JOIN SubReason sr ON sr.ReasonId = r.ReasonId AND sr.SubReasonText = s.value;

-- covers: a Global cover takes its values from the global decision, an Override cover from its own line
UPDATE u
   SET DecisionSource   = c.Source,
       DecisionId       = CASE WHEN c.Source = 'GLOBAL' THEN g.DecisionId ELSE d.DecisionId END,
       ReasonId         = CASE WHEN c.Source = 'GLOBAL' THEN g.ReasonId   ELSE r.ReasonId   END,
       PostponePeriod   = c.PostponePeriod,
       EMR              = c.EMR,
       OccupationExtra  = c.OccupationExtra,
       ResidentialExtra = c.ResidentialExtra,
       FlatExtra        = c.FlatExtra,
       AgeExtra         = c.AgeExtra,
       Notes            = c.Notes
  FROM UWDecision u
  JOIN #Cover c ON c.CoverId = CASE WHEN u.CoverType = 'BASE' THEN u.PolicyNumber ELSE u.RiderId END
  LEFT JOIN UWGlobalDecision g ON g.CustomerId = u.CustomerId
                              AND g.CoverKind = CASE WHEN u.CoverType = 'BASE' THEN 'BASE' ELSE u.RiderType END
  LEFT JOIN DecisionOption d ON d.DecisionName = c.Decision
  LEFT JOIN DecisionReason r ON r.DecisionId = d.DecisionId AND r.ReasonText = c.Reason;

INSERT INTO UWDecisionSubReason (UwId, SubReasonId)
SELECT u.UwId, gs.SubReasonId
  FROM UWDecision u
  JOIN UWGlobalDecision g ON g.CustomerId = u.CustomerId
                         AND g.CoverKind = CASE WHEN u.CoverType = 'BASE' THEN 'BASE' ELSE u.RiderType END
  JOIN UWGlobalSubReason gs ON gs.GlobalId = g.GlobalId
 WHERE u.DecisionSource = 'GLOBAL' AND u.CustomerId LIKE '9000000%';

INSERT INTO UWDecisionSubReason (UwId, SubReasonId)
SELECT u.UwId, sr.SubReasonId
  FROM #Cover c
  JOIN UWDecision u ON c.CoverId = CASE WHEN u.CoverType = 'BASE' THEN u.PolicyNumber ELSE u.RiderId END
  JOIN DecisionReason r ON r.ReasonText = c.Reason
 CROSS APPLY STRING_SPLIT(c.Subs, '|') s
  JOIN SubReason sr ON sr.ReasonId = r.ReasonId AND sr.SubReasonText = s.value
 WHERE c.Source = 'OVERRIDE';

DROP TABLE #Global;
DROP TABLE #Cover;