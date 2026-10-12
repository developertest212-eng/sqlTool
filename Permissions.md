# Database Permissions & Security Guide for SQL Command Center

SQL Command Center adheres to the principle of least privilege. To ensure smooth operation and real-time monitoring across SQL Server and MySQL instances, the monitoring accounts should be granted the minimum required privileges outlined below.

---

## 1. Microsoft SQL Server Permissions

### Minimum Monitoring Permissions (Read-Only)
For monitoring performance counters, CPU ring buffers, memory DMV tables, database storage, and active sessions:

```sql
-- Create monitoring login and user
USE [master];
CREATE LOGIN [sqlcc_monitor] WITH PASSWORD = 'StrongMonitoringPassword123!';
CREATE USER [sqlcc_monitor] FOR LOGIN [sqlcc_monitor];

-- Grant server-level state viewing (Required for sys.dm_os_*, sys.dm_exec_*)
-- For SQL Server 2022 / Azure SQL:
GRANT VIEW SERVER PERFORMANCE STATE TO [sqlcc_monitor];
GRANT VIEW SERVER SECURITY STATE TO [sqlcc_monitor];

-- For SQL Server 2012 through 2019:
GRANT VIEW SERVER STATE TO [sqlcc_monitor];
GRANT VIEW ANY DATABASE TO [sqlcc_monitor];

-- Grant access to MSDB for backup history retrieval
USE [msdb];
CREATE USER [sqlcc_monitor] FOR LOGIN [sqlcc_monitor];
GRANT SELECT ON dbo.backupset TO [sqlcc_monitor];
GRANT SELECT ON dbo.backupmediafamily TO [sqlcc_monitor];
```

### Optional Administration Permissions (For Session Termination)
If the monitoring user is also permitted to terminate stuck queries or kill blocking sessions from the UI:

```sql
-- Required only if using "Kill Session" button
GRANT ALTER ANY CONNECTION TO [sqlcc_monitor];
```

---

## 2. MySQL Permissions

### Minimum Monitoring Permissions (Read-Only)
For reading status variables, process lists, InnoDB buffer pool metrics, and schema table sizes:

```sql
-- Create monitoring user
CREATE USER 'sqlcc_monitor'@'%' IDENTIFIED BY 'StrongMonitoringPassword123!';

-- PROCESS privilege is required to inspect running queries across all users in PROCESSLIST
GRANT PROCESS ON *.* TO 'sqlcc_monitor'@'%';

-- SELECT on information_schema and performance_schema tables
GRANT SELECT ON information_schema.* TO 'sqlcc_monitor'@'%';
GRANT SELECT ON performance_schema.* TO 'sqlcc_monitor'@'%';

-- Required if checking replication / replica status
GRANT REPLICATION CLIENT ON *.* TO 'sqlcc_monitor'@'%';

FLUSH PRIVILEGES;
```

### Optional Administration Permissions (For Session Termination)
If the user is permitted to kill client connections or queries from the UI:

```sql
-- MySQL 8.0+:
GRANT CONNECTION_ADMIN ON *.* TO 'sqlcc_monitor'@'%';

-- MySQL 5.7:
GRANT SUPER ON *.* TO 'sqlcc_monitor'@'%';

FLUSH PRIVILEGES;
```

---

## 3. Credential Security & Windows DPAPI

* All passwords saved in connection profiles (`servers.json`) are automatically encrypted using **Windows DPAPI** (`ProtectedData.Protect` with `DataProtectionScope.CurrentUser`).
* No plaintext passwords are ever serialized to configuration files or emitted into application logs.
* Passwords are only decrypted transiently in memory when creating live database connection objects.
* All connection strings written to application logs are automatically scrubbed via regex sanitization.
