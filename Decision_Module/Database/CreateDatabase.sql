IF DB_ID(N'UWDecisionDb') IS NULL
BEGIN
    CREATE DATABASE UWDecisionDb;
    PRINT 'UWDecisionDb created successfully.';
END
ELSE
BEGIN
    PRINT 'UWDecisionDb already exists.';
END;
GO