USE UWDecisionDb;
GO

UPDATE u
   SET DecisionId       = d.DecisionId,
       ReasonId         = r.ReasonId,
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
     -- CoverId (policy number for base, rider id for riders), Decision, Reason, Source,
     -- PostponePeriod, EMR, OccupationExtra, ResidentialExtra, FlatExtra, AgeExtra, Notes
     -- Customer A
     ('9000000001',N'Accept at OR',     N'Accept at OR',     'GLOBAL',  NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('DEMO-RID-1',N'Accept with Extra',N'Accept with Extra','GLOBAL',  NULL,  50,NULL,NULL,NULL,NULL,NULL),
     ('DEMO-RID-2',N'Accept at OR',     N'Accept at OR',     'OVERRIDE',NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('DEMO-RID-3',N'Decline',          N'Medical risk not acceptable due to elevated blood sugar levels','OVERRIDE',NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('9000000002',N'Postpone',         NULL,                'OVERRIDE',   6,NULL,NULL,NULL,NULL,NULL,N'DEMO: awaiting income proof'),
     -- Customer B (hazardous occupation)
     ('9000000003',N'Accept with Extra',N'Accept with Extra','GLOBAL',  NULL,NULL,   2,NULL,NULL,NULL,NULL),
     ('DEMO-RID-4',N'Accept with Extra',N'Accept with Extra','GLOBAL',  NULL,NULL,   4,NULL,NULL,NULL,NULL),
     -- Customer C (medical decline)
     ('9000000004',N'Decline',          N'Noncompliance of medical information/ reports for medical risk assessment','GLOBAL',NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('DEMO-RID-5',N'Decline',          N'Noncompliance of medical information/ reports for medical risk assessment','GLOBAL',NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('9000000005',N'Counter Offer',    NULL,                'OVERRIDE',NULL,NULL,NULL,NULL,NULL,NULL,N'DEMO: reduced sum assured offered'),
     -- Customer D (documents pending)
     ('9000000006',N'Requirements',     N'Requirements',     'GLOBAL',  NULL,NULL,NULL,NULL,NULL,NULL,N'DEMO: latest ECG and income proof needed'),
     -- Customer E (lives abroad)
     ('9000000007',N'Accept with Extra',N'Accept with Extra','GLOBAL',  NULL,NULL,NULL,   3,NULL,NULL,NULL),
     ('9000000008',N'Reject',           NULL,                'OVERRIDE',NULL,NULL,NULL,NULL,NULL,NULL,N'DEMO: country outside accepted list'),
     -- Customer F (rated age; its rider has no decision yet)
     ('9000000009',N'Accept with Extra',N'Accept with Extra','GLOBAL',  NULL,NULL,NULL,NULL,NULL,   5,NULL),
     -- Customer G (every rider overridden)
     ('9000000010',N'Accept at OR',     N'Accept at OR',     'GLOBAL',  NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('DEMO-RID-7',N'Decline',          N'Medical risk not acceptable due to Raised Liver function Tests','OVERRIDE',NULL,NULL,NULL,NULL,NULL,NULL,NULL),
     ('DEMO-RID-8',N'Accept with Extra',N'Accept with Extra','OVERRIDE',NULL,NULL,NULL,NULL, 1.5,NULL,NULL)
  ) AS v (CoverId, Decision, Reason, Source, PostponePeriod, EMR,
          OccupationExtra, ResidentialExtra, FlatExtra, AgeExtra, Notes)
    ON v.CoverId = CASE WHEN u.CoverType = 'BASE' THEN u.PolicyNumber ELSE u.RiderId END
  JOIN DecisionOption d ON d.DecisionName = v.Decision
  LEFT JOIN DecisionReason r ON r.DecisionId = d.DecisionId AND r.ReasonText = v.Reason;
GO