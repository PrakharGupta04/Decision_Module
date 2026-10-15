USE UWDecisionDb;
GO

INSERT INTO Customer (CustomerId, CustomerName, BenefitGroupId) VALUES
 ('101891625', N'Rajinder Tripathi',     'Life'),
 ('115204268', N'SHIVAM SHET',           'Life'),
 ('123230715', N'NIKUNJ GOYAL',          'Life'),
 ('123259449', N'RAVIVARMAN SELVAKUMAR', 'Life'),
 ('123965992', N'ALKA SINGH',            'Life'),
 ('79040595',  N'TANVI ANDRADE',         'Life'),
 ('85001',     N'Raghav Mishra',         'Life'),
 ('97515806',  N'ADITI SHEKHAWAT',       'Life'),
 ('97725761',  N'NITIN BANSOD',          'Life'),
 ('98013635',  N'MINAL BHOIR',           'Life'),
 ('98275003',  N'SUSHANT NAVALE',        'Life');

INSERT INTO Policy
 (PolicyNumber, CustomerId, MarketName, BillingFrequency, BenefitCode, BenefitGroup,
  Bterm, Bppt, BenefitInstalmentPremium, BenefitSumAssured) VALUES
 ('10000000070','79040595', N'Bajaj Life Future Wealth Gain',      1,'L167A01','Life',10, 5,200000,2000000),
 ('10000089560','101891625',N'Bajaj Future Gain',                  1,'L149A01','Life',10,10, 30000, 300000),
 ('10000180755','123230715',N'Bajaj Life Supreme',                 1,'L210G01','Life',95,10,155000,1550000),
 ('10000182584','123259449',N'Bajaj Life Saral Jeevan Bima',       1,'L188A01','Life',38,38,     0, 500000),
 ('10000193843','115204268',N'Bajaj Life Guaranteed Pension Goal', 4,'L186B01','Life',76,12,  4638,      1),
 ('10000241127','123965992',N'Bajaj Life eTouch Term',             1,'L191A01','Life',54, 6, 27869,5000000);
GO