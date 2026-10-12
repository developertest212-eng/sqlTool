# SQL Command Center

An enterprise-grade, high-performance **VB.NET Windows desktop monitoring and management dashboard** for **Microsoft SQL Server** and **MySQL** database platforms built with **.NET 8 Windows Forms**.

![Platform](https://img.shields.io/badge/.NET-8.0-blue.svg)
![Language](https://img.shields.io/badge/Language-VB.NET-purple.svg)
![UI](https://img.shields.io/badge/UI-Windows%20Forms-green.svg)
![Security](https://img.shields.io/badge/Security-Windows%20DPAPI-orange.svg)

---

## Key Capabilities

### 1. Multi-Server & Fleet Monitoring
* Real-time monitoring across heterogeneous database environments (Microsoft SQL Server and MySQL).
* Fleet overview cards highlighting online status, latency, CPU %, active connections, and database count per server.
* Seamless switching between multi-server fleet overview and single-server deep-dive dashboards.
* Server grouping, search, and favorite flags.

### 2. High-Resolution Performance Telemetry
* Real-time anti-aliased GDI+ line and area charts for:
  * **CPU Utilization (%)** (via SQL Server DMVs & scheduler ring buffers, MySQL process indicators)
  * **Memory Usage & Buffer Cache Hit Ratio** (process memory, total system memory, InnoDB buffer pool stats)
  * **Active Connections & Concurrency**
  * **Throughput & QPS (Queries per second)**
* Configurable historical polling intervals: 10s, 30s, 1m, 5m, 15m, and manual on-demand refresh.

### 3. Active Queries & Session Diagnostics
* Inspect running queries in real time with session ID, login user, host, database, elapsed duration, CPU time (ms), logical reads, writes, wait type, and blocking session ID.
* Full statement text preview and one-click copy to clipboard.
* **Safe Session Termination**: Interactive Kill Session / Terminate Connection with mandatory confirmation guard dialog.

### 4. Lock Contention & Visual Blocking Tree
* Dedicated Lock & Blocking inspector.
* Hierarchical blocking tree illustrating multi-hop blocking chains (e.g. `Lead Blocker SPID 52 ➔ Blocked SPID 74 ➔ Blocked SPID 81`).
* Blocker SQL vs Blocked SQL statement comparison.
* One-click "Kill Lead Blocker" to instantly clear database contention.

### 5. Database Catalog & Schema Explorer
* Complete database inventory: State, Recovery Model, Collation, Compatibility Level, Created Date, Data Size (MB), Log Size (MB), and Total Size.
* Interactive tree explorer: Tables, Views, Stored Procedures, and Functions.
* Table column definitions inspector: Data types, max length, nullable flags, primary keys, and default values.
* Script generation: Instant "Generate SELECT *" statements.

### 6. Storage & Capacity Management
* File-level storage inspector (Data MDF/NDF vs Transaction Log LDF).
* Physical paths, file sizes, used space, and autogrowth settings.
* Color-coded threshold alerts: **Warning at >75% used**, **Critical at >90% used**.

### 7. Backup Freshness & Recovery Validation
* Tracks Full, Differential, and Transaction Log backups from `msdb.dbo.backupset`.
* Highlights stale backups older than configurable thresholds.
* For MySQL, tracks active Binary Log sizes and replication status.

### 8. Enterprise SQL Console
* Multi-statement query execution engine with F5 shortcut support.
* Tabbed output: Formatted Results Grid and Messages/Output log.
* Execution time tracking (ms) and row count feedback.
* **Destructive SQL Guard**: Automatically intercepts potentially hazardous commands (`DROP`, `DELETE`, `TRUNCATE`, `ALTER`, `UPDATE`) and requires explicit operator approval before execution.

### 9. Alert Engine & Incident Tracking
* Automated background rule evaluation against CPU, Memory, Storage, Blocking Contention, and Connection thresholds.
* Severity classifications: `CRITICAL`, `WARNING`, `INFO`.
* Alert lifecycle management: Active, Acknowledged, and Resolved states.

### 10. Reports & Export
* Comprehensive reporting templates for Server Health, Database Space, Workload Audit, and Security Alerts.
* Export to **CSV** and **styled HTML** (browser printable to PDF).

### 11. Security & Windows DPAPI
* Zero plain-text credential storage: Sensitive passwords in `servers.json` are encrypted using the **Windows Data Protection API (DPAPI)** with `CurrentUser` scope.
* Automatic credential sanitization in application logs.
* Support for SQL Server Authentication and Windows Integrated Authentication.

---

## Architecture & Project Structure

```
SQLCommandCenter/
├── SQLCommandCenter.sln
├── SQLCommandCenter.vbproj
│
├── Models/                     # Strongly typed data models and enums
│   ├── Enums.vb
│   ├── ServerProfile.vb        # Connection profile with DPAPI encryption
│   ├── ServerStatus.vb         # Live status, latency, version, health
│   ├── PerformanceMetrics.vb   # CPU, memory, buffer pool, QPS, TPS
│   ├── DatabaseInfo.vb         # Catalogs, tables, views, procedures, columns
│   ├── QueryInfo.vb            # Active query execution stats
│   ├── BlockingInfo.vb         # Lock contention & blocking hierarchy
│   ├── StorageInfo.vb          # File-level data and log allocation
│   ├── BackupInfo.vb           # Backup history and freshness
│   └── AlertInfo.vb            # System health and threshold alerts
│
├── Data/                       # Database provider abstraction
│   ├── IDatabaseProvider.vb    # Unified async provider contract
│   ├── SqlServerProvider.vb    # High-performance DMV queries via Microsoft.Data.SqlClient
│   ├── MySqlProvider.vb        # Metrics via MySqlConnector & information_schema
│   └── DatabaseProviderFactory.vb
│
├── Services/                   # Application services
│   ├── ServerService.vb        # Profile persistence and active server state
│   ├── MonitoringService.vb    # Async periodic background polling with backoff
│   ├── AlertService.vb         # Automated threshold rule evaluation
│   ├── QueryService.vb         # Query execution and safety checks
│   └── ReportService.vb        # CSV and HTML report export
│
├── Security/
│   └── CredentialManager.vb    # Windows DPAPI encryption/decryption module
│
├── Configuration/
│   ├── AppSettings.vb          # Application configuration settings
│   └── ConfigurationManager.vb # Thread-safe JSON configuration loader
│
├── Logging/
│   └── AppLogger.vb            # Thread-safe rolling logger with credential masking
│
└── UI/
    ├── Theme/
    │   └── ThemeManager.vb     # Dark and Light enterprise color palettes
    ├── Controls/               # Custom-drawn GDI+ anti-aliased controls
    │   ├── MetricCard.vb
    │   ├── StatusBadge.vb
    │   ├── ServerCard.vb
    │   ├── ChartCanvas.vb      # Real-time multi-point performance line charts
    │   ├── ModernDataGrid.vb   # Borderless styled DataGridView
    │   └── SidebarButton.vb    # Sidebar navigation button
    ├── Forms/
    │   ├── MainForm.vb         # Main window shell & view host
    │   ├── ServerEditDialog.vb # Add/Edit server with live Test Connection
    │   └── ConfirmDestructiveDialog.vb # Safety modal for destructive commands
    └── Views/                  # Modular UserControls for each section
        ├── DashboardView.vb
        ├── ServersView.vb
        ├── DatabasesView.vb
        ├── PerformanceView.vb
        ├── QueriesView.vb
        ├── ConnectionsView.vb
        ├── BlockingView.vb
        ├── StorageView.vb
        ├── BackupView.vb
        ├── AlertsView.vb
        ├── ReportsView.vb
        ├── SqlConsoleView.vb
        └── SettingsView.vb
```

---

## Prerequisites

* **Operating System**: Windows 10 / 11 or Windows Server 2016+
* **Framework**: .NET 8.0 SDK / Runtime (`net8.0-windows`)
* **IDE**: Visual Studio 2022 / Visual Studio 2026 or .NET CLI

---

## Building and Running

### Build via .NET CLI:

```powershell
cd SQLCommandCenter
dotnet restore
dotnet build -c Release
```

### Run the Application:

```powershell
dotnet run -c Release
```

---

## Configuration & Log Locations

* **Configuration**: `%LOCALAPPDATA%\SQLCommandCenter\config\appsettings.json`
* **Saved Servers**: `%LOCALAPPDATA%\SQLCommandCenter\config\servers.json` (passwords DPAPI encrypted)
* **Application Logs**: `%LOCALAPPDATA%\SQLCommandCenter\logs\sqlcommandcenter.log`
