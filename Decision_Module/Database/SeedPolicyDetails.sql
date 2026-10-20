USE UWDecisionDb;
GO

-- Payment frequency code. Assumption: the sheet's number is payments per year (1 = A, 2 = S, 4 = Q, 12 = M).
UPDATE Policy
   SET PaymentFrequency = CASE BillingFrequency WHEN 1 THEN 'A' WHEN 2 THEN 'S' WHEN 4 THEN 'Q' WHEN 12 THEN 'M' END
 WHERE PaymentFrequency IS NULL;

-- Sample base cover id and type
UPDATE Policy SET BaseCoverId = 'BC-' + PolicyNumber WHERE BaseCoverId IS NULL;

UPDATE Policy
   SET BaseCoverType = CASE WHEN MarketName LIKE N'%Term%' OR MarketName LIKE N'%Saral Jeevan%' THEN N'Term Life'
                            WHEN MarketName LIKE N'%Pension%'   THEN N'Pension'
                            WHEN MarketName LIKE N'%ULIP%'      THEN N'ULIP'
                            WHEN MarketName LIKE N'%Endowment%' THEN N'Endowment'
                            ELSE N'Savings' END
 WHERE BaseCoverType IS NULL;

-- Sample reinsurer per policy: some customers have one reinsurer on all policies, some have different ones
UPDATE p
   SET p.ReinsurerId = v.ReinsurerId
  FROM Policy p
  JOIN (VALUES ('10000000070', 'RE-01'), ('10000089560', 'RE-02'), ('10000180755', 'RE-01'),
               ('10000182584', 'RE-02'), ('10000193843', 'RE-03'), ('10000241127', 'RE-01'),
               ('9000000001', 'RE-01'), ('9000000002', 'RE-02'), ('9000000003', 'RE-01'),
               ('9000000004', 'RE-02'), ('9000000005', 'RE-02'), ('9000000006', 'RE-03'),
               ('9000000007', 'RE-01'), ('9000000008', 'RE-03'), ('9000000009', 'RE-01'),
               ('9000000010', 'RE-02')) AS v (PolicyNumber, ReinsurerId)
    ON v.PolicyNumber = p.PolicyNumber;

-- Customer level reinsurer: the one on the customer's first policy
UPDATE c
   SET c.ReinsurerId = (SELECT TOP 1 p.ReinsurerId FROM Policy p
                         WHERE p.CustomerId = c.CustomerId ORDER BY p.PolicyNumber)
  FROM Customer c
 WHERE EXISTS (SELECT 1 FROM Policy p WHERE p.CustomerId = c.CustomerId);

-- Copy to the cover rows
UPDATE u
   SET u.ReinsurerId      = p.ReinsurerId,
       u.PaymentFrequency = p.PaymentFrequency,
       u.BaseCoverId      = CASE WHEN u.CoverType = 'BASE' THEN p.BaseCoverId END,
       u.BaseCoverType    = CASE WHEN u.CoverType = 'BASE' THEN p.BaseCoverType END
  FROM UWDecision u
  JOIN Policy p ON p.PolicyNumber = u.PolicyNumber;
GO