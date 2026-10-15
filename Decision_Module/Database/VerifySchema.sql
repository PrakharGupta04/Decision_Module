
USE UWDecisionDb;

SELECT 'Tables' AS Item, COUNT(*) AS Found, 6 AS Expected
FROM sys.tables

UNION ALL

SELECT 'Foreign keys', COUNT(*), 8
FROM sys.foreign_keys

UNION ALL

SELECT 'Check constraints', COUNT(*), 2
FROM sys.check_constraints

UNION ALL

SELECT 'Filtered unique indexes', COUNT(*), 3
FROM sys.indexes
WHERE name LIKE 'UX[_]%';
