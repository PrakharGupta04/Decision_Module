USE UWDecisionDb;
GO

INSERT INTO Customer (CustomerId, CustomerName, BenefitGroupId) VALUES
 ('900000001', N'DEMO Customer A', 'Life'),
 ('900000002', N'DEMO Customer B', 'Life'),
 ('900000003', N'DEMO Customer C', 'Life'),
 ('900000004', N'DEMO Customer D', 'Life'),
 ('900000005', N'DEMO Customer E', 'Life'),
 ('900000006', N'DEMO Customer F', 'Life'),
 ('900000007', N'DEMO Customer G', 'Life');

INSERT INTO Policy
 (PolicyNumber, CustomerId, MarketName, BillingFrequency, BenefitCode, BenefitGroup,
  Bterm, Bppt, BenefitInstalmentPremium, BenefitSumAssured) VALUES
 ('9000000001','900000001', N'DEMO Term Plan',      1,'DEMO-B01','Life',20,10,  5000,  100000),
 ('9000000002','900000001', N'DEMO Savings Plan',   4,'DEMO-B02','Life',15,15, 40000,  600000),
 ('9000000003','900000002', N'DEMO Term Plan',      4,'DEMO-B03','Life',30,30,  3000,10000000),
 ('9000000004','900000003', N'DEMO Term Plan',      1,'DEMO-B04','Life',25,25,  6000, 5000000),
 ('9000000005','900000003', N'DEMO Pension Plan',   1,'DEMO-B05','Life',20,10,100000, 1000000),
 ('9000000006','900000004', N'DEMO Wealth ULIP',    2,'DEMO-B06','Life',15,15, 10000, 1000000),
 ('9000000007','900000005', N'DEMO Term Plan',     12,'DEMO-B07','Life',20,20,  1500, 5000000),
 ('9000000008','900000005', N'DEMO Pension Plan',   1,'DEMO-B08','Life',25,10,200000, 2000000),
 ('9000000009','900000006', N'DEMO Endowment Plan', 2,'DEMO-B09','Life',20,15, 25000,  500000),
 ('9000000010','900000007', N'DEMO Term Plan',      1,'DEMO-B10','Life',30,20,  7000, 7500000);

INSERT INTO Rider
 (RiderId, PolicyNumber, BenefitCode, RiderType, RiderSumAssured, RiderPremium, RiderTerm, RiderPpt) VALUES
 ('DEMO-RID-1','9000000001','DEMO-R01','Critical_Illness',   25000,  600,20,10),
 ('DEMO-RID-2','9000000001','DEMO-R02','Accidental_Death',   10000,  200,15,15),
 ('DEMO-RID-3','9000000001','DEMO-R03','Waiver_of_Premium',   NULL,  150,20,10),
 ('DEMO-RID-4','9000000003','DEMO-R02','Accidental_Death',  5000000, 1500,30,30),
 ('DEMO-RID-5','9000000004','DEMO-R01','Critical_Illness',  1000000,  900,25,25),
 ('DEMO-RID-6','9000000009','DEMO-R01','Critical_Illness',   300000,  700,20,15),
 ('DEMO-RID-7','9000000010','DEMO-R01','Critical_Illness',  1500000, 1200,30,20),
 ('DEMO-RID-8','9000000010','DEMO-R02','Accidental_Death',  2500000, 1000,30,20);
