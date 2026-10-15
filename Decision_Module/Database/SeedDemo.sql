USE UWDecisionDb;
GO

INSERT INTO Customer (CustomerId, CustomerName, BenefitGroupId)
VALUES ('900000001', N'DEMO Customer A', 'Life');

INSERT INTO Policy
 (PolicyNumber, CustomerId, MarketName, BillingFrequency, BenefitCode, BenefitGroup,
  Bterm, Bppt, BenefitInstalmentPremium, BenefitSumAssured) VALUES
 ('9000000001','900000001',N'DEMO Term Plan',   1,'DEMO-B01','Life',20,10, 5000,100000),
 ('9000000002','900000001',N'DEMO Savings Plan',4,'DEMO-B02','Life',15,15,40000,600000);

INSERT INTO Rider (RiderId, PolicyNumber, RiderType, RiderSumAssured, RiderPremium, RiderTerm, RiderPpt) VALUES
 ('DEMO-RID-1','9000000001','Critical_Illness',25000,600,20,10),
 ('DEMO-RID-2','9000000001','Accidental_Death',10000,200,20,10);

INSERT INTO UnderwritingDecision
 (CustomerId, CoverType, PolicyNumber, RiderId, DecisionId, ReasonId, PostponePeriod, EMR, Notes)
VALUES
 -- global BASE decision (no policy, no rider)
 ('900000001','BASE',NULL,NULL,
   (SELECT DecisionId FROM DecisionOption WHERE DecisionName = N'Accept at OR'),
   (SELECT ReasonId   FROM DecisionReason WHERE ReasonText   = N'Accept at OR'),
   NULL, NULL, N'DEMO: global base'),
 -- global RIDER decision
 ('900000001','RIDER',NULL,NULL,
   (SELECT DecisionId FROM DecisionOption WHERE DecisionName = N'Accept with Extra'),
   (SELECT ReasonId   FROM DecisionReason WHERE ReasonText   = N'Accept with Extra'),
   NULL, 50.00, N'DEMO: global rider'),
 -- override on policy 9000000002's base cover
 ('900000001','BASE','9000000002',NULL,
   (SELECT DecisionId FROM DecisionOption WHERE DecisionName = N'Postpone'),
   NULL, 6, NULL, N'DEMO: base override'),
 -- override on one rider
 ('900000001','RIDER',NULL,'DEMO-RID-2',
   (SELECT DecisionId FROM DecisionOption WHERE DecisionName = N'Decline'),
   (SELECT ReasonId   FROM DecisionReason WHERE ReasonText   = N'Medical risk not acceptable due to Low Haemoglobin'),
   NULL, NULL, N'DEMO: rider override');
GO