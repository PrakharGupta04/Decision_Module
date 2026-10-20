USE UWDecisionDb;

DROP TABLE IF EXISTS #Reason;
DROP TABLE IF EXISTS #Sub;

-- decision, reason, cover kinds the reason applies to
CREATE TABLE #Reason (Decision NVARCHAR(50), Reason NVARCHAR(250), Kinds VARCHAR(200));
INSERT INTO #Reason VALUES
 (N'Accept with Extra', N'Medical loading',                    'BASE,Critical_Illness'),
 (N'Accept with Extra', N'Occupational hazard',                'BASE,Accidental_Death'),
 (N'Accept with Extra', N'Hazardous pursuit',                  'BASE,Accidental_Death'),
 (N'Accept with Extra', N'Residence or travel risk',           'BASE,Accidental_Death'),
 (N'Accept with Extra', N'Age rating',                         'BASE'),
 (N'Postpone',          N'Medical investigation pending',      'BASE,Critical_Illness,Waiver_of_Premium'),
 (N'Postpone',          N'Financial documents pending',        'BASE'),
 (N'Postpone',          N'Occupation details awaited',         'BASE,Accidental_Death'),
 (N'Requirements',      N'Additional medical evidence required', 'BASE,Critical_Illness,Waiver_of_Premium'),
 (N'Requirements',      N'Financial documents required',       'BASE'),
 (N'Requirements',      N'Occupation details required',        'BASE,Accidental_Death'),
 (N'Counter Offer',     N'Reduced sum assured',                'BASE,Critical_Illness,Accidental_Death'),
 (N'Counter Offer',     N'Shorter cover term',                 'BASE,Critical_Illness,Accidental_Death,Waiver_of_Premium'),
 (N'Decline',           N'Medical risk not acceptable',        'BASE,Critical_Illness,Waiver_of_Premium'),
 (N'Decline',           N'Noncompliance of medical information/ reports for medical risk assessment', 'BASE,Critical_Illness,Waiver_of_Premium'),
 (N'Decline',           N'Noncompliance of Financial Information required for applied cover', 'BASE'),
 (N'Decline',           N'Hazardous occupation or pursuit not acceptable', 'Accidental_Death'),
 (N'Reject',            N'Misrepresentation or non-disclosure', 'BASE,Critical_Illness,Accidental_Death,Waiver_of_Premium'),
 (N'Reject',            N'Outside territorial limits',         'BASE,Accidental_Death');

-- reason, sub-reason
CREATE TABLE #Sub (Reason NVARCHAR(250), Sub NVARCHAR(200));
INSERT INTO #Sub VALUES
 (N'Medical loading', N'Elevated blood sugar levels'),
 (N'Medical loading', N'Raised blood pressure'),
 (N'Medical loading', N'Overweight'),
 (N'Medical loading', N'Raised liver function tests'),
 (N'Medical loading', N'Low haemoglobin'),
 (N'Occupational hazard', N'Aviation or aircrew'),
 (N'Occupational hazard', N'Mining or underground work'),
 (N'Occupational hazard', N'Work at heights'),
 (N'Occupational hazard', N'Hazardous materials or explosives'),
 (N'Hazardous pursuit', N'Scuba diving'),
 (N'Hazardous pursuit', N'Motor racing'),
 (N'Hazardous pursuit', N'Mountaineering'),
 (N'Hazardous pursuit', N'Skydiving or paragliding'),
 (N'Hazardous pursuit', N'Private flying'),
 (N'Residence or travel risk', N'Resident outside India'),
 (N'Residence or travel risk', N'Frequent travel to high-risk areas'),
 (N'Medical investigation pending', N'Test results awaited'),
 (N'Medical investigation pending', N'Specialist report awaited'),
 (N'Medical investigation pending', N'Treatment or recovery in progress'),
 (N'Financial documents pending', N'Income proof awaited'),
 (N'Financial documents pending', N'Bank statements awaited'),
 (N'Additional medical evidence required', N'ECG'),
 (N'Additional medical evidence required', N'Blood profile'),
 (N'Additional medical evidence required', N'Medical examiner''s report'),
 (N'Additional medical evidence required', N'Attending physician''s statement'),
 (N'Financial documents required', N'Latest income proof'),
 (N'Financial documents required', N'Bank statements'),
 (N'Occupation details required', N'Employer certificate'),
 (N'Occupation details required', N'Job description'),
 (N'Medical risk not acceptable', N'Life Assured appears to be of gross underweight'),
 (N'Medical risk not acceptable', N'Life Assured appears to be morbid overweight'),
 (N'Medical risk not acceptable', N'Elevated blood sugar levels'),
 (N'Medical risk not acceptable', N'Low Haemoglobin'),
 (N'Medical risk not acceptable', N'Raised Liver function Tests'),
 (N'Hazardous occupation or pursuit not acceptable', N'Occupation hazard too high'),
 (N'Hazardous occupation or pursuit not acceptable', N'Hazardous sport not covered'),
 (N'Misrepresentation or non-disclosure', N'Undisclosed medical condition'),
 (N'Misrepresentation or non-disclosure', N'Incorrect age or income declared');

INSERT INTO DecisionReason (DecisionId, ReasonText)
SELECT d.DecisionId, r.Reason
  FROM #Reason r
  JOIN DecisionOption d ON d.DecisionName = r.Decision
 WHERE NOT EXISTS (SELECT 1 FROM DecisionReason x WHERE x.DecisionId = d.DecisionId AND x.ReasonText = r.Reason);

INSERT INTO ReasonCover (ReasonId, CoverKind)
SELECT dr.ReasonId, LTRIM(RTRIM(k.value))
  FROM #Reason r
  JOIN DecisionReason dr ON dr.ReasonText = r.Reason
 CROSS APPLY STRING_SPLIT(r.Kinds, ',') k
 WHERE NOT EXISTS (SELECT 1 FROM ReasonCover x
                    WHERE x.ReasonId = dr.ReasonId AND x.CoverKind = LTRIM(RTRIM(k.value)));

INSERT INTO SubReason (ReasonId, SubReasonText)
SELECT dr.ReasonId, s.Sub
  FROM #Sub s
  JOIN DecisionReason dr ON dr.ReasonText = s.Reason
 WHERE NOT EXISTS (SELECT 1 FROM SubReason x WHERE x.ReasonId = dr.ReasonId AND x.SubReasonText = s.Sub);

DROP TABLE #Reason;
DROP TABLE #Sub;