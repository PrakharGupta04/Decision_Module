USE UWDecisionDb;
GO

-- one BASE row per policy
INSERT INTO UWDecision
    (CustomerId, CoverType, PolicyNumber, MarketName, BillingFrequency, PaymentFrequency, ReinsurerId,
     BaseCoverId, BaseCoverType, BenefitCode, BenefitGroup, Bterm, Bppt, BenefitInstalmentPremium, BenefitSumAssured)
SELECT p.CustomerId, 'BASE', p.PolicyNumber, p.MarketName, p.BillingFrequency, p.PaymentFrequency, p.ReinsurerId,
       p.BaseCoverId, p.BaseCoverType, p.BenefitCode, p.BenefitGroup, p.Bterm, p.Bppt,
       p.BenefitInstalmentPremium, p.BenefitSumAssured
  FROM Policy p
 WHERE NOT EXISTS (SELECT 1 FROM UWDecision u WHERE u.CoverType = 'BASE' AND u.PolicyNumber = p.PolicyNumber);

-- one RIDER row per rider; the policy's details are repeated on it
INSERT INTO UWDecision
    (CustomerId, CoverType, PolicyNumber, RiderId, RiderType, MarketName, BillingFrequency, PaymentFrequency,
     ReinsurerId, BenefitCode, BenefitGroup, Bterm, Bppt, BenefitInstalmentPremium, BenefitSumAssured)
SELECT p.CustomerId, 'RIDER', r.PolicyNumber, r.RiderId, r.RiderType, p.MarketName, p.BillingFrequency, p.PaymentFrequency,
       p.ReinsurerId, r.BenefitCode, p.BenefitGroup, r.RiderTerm, r.RiderPpt, r.RiderPremium, r.RiderSumAssured
  FROM Rider r
  JOIN Policy p ON p.PolicyNumber = r.PolicyNumber
 WHERE NOT EXISTS (SELECT 1 FROM UWDecision u WHERE u.CoverType = 'RIDER' AND u.RiderId = r.RiderId);
GO