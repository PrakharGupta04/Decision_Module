USE UWDecisionDb;
GO

INSERT INTO DecisionOption (DecisionName) VALUES
 (N'Accept at OR'), (N'Accept with Extra'), (N'Postpone'), (N'Decline'),
 (N'Requirements'), (N'Counter Offer'), (N'Reject');

INSERT INTO DecisionReason (DecisionId, ReasonText)
SELECT d.DecisionId, r.ReasonText
FROM (VALUES
 (N'Decline',           N'Noncompliance of Financial Information required for applied cover'),
 (N'Decline',           N'Noncompliance of medical information/ reports for medical risk assessment'),
 (N'Decline',           N'Medical risk not acceptable due to Life Assured appears to be of gross underweight'),
 (N'Decline',           N'Medical risk not acceptable due to Life Assured appears to be morbid overweight'),
 (N'Decline',           N'Medical risk not acceptable due to elevated blood sugar levels'),
 (N'Decline',           N'Medical risk not acceptable due to Low Haemoglobin'),
 (N'Accept at OR',      N'Accept at OR'),
 (N'Accept with Extra', N'Accept with Extra'),
 (N'Requirements',      N'Requirements'),
 (N'Decline',           N'Medical risk not acceptable due to Raised Liver function Tests')
) AS r(DecisionName, ReasonText)
JOIN DecisionOption d ON d.DecisionName = r.DecisionName;
GO