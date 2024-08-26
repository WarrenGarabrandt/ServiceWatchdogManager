using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWManager.Database
{
    public static class SQLiteStrings
    {
        private const string COMPATIBLE_DATABASE_VERSION = "1.0";
        public static string[] Format_Database = new string[]
        {
            // Contains configuration and version data.
            @"CREATE TABLE System (Category TEXT, Setting TEXT, Value TEXT);",

            // create unique constraint so that REPLACE INTO will function properly.
            @"CREATE UNIQUE INDEX idx_System_CategorySetting ON System(Category, Setting);",

            // User table
            @"CREATE TABLE User (UserID INTEGER PRIMARY KEY, DisplayName TEXT, Email TEXT, Salt TEXT NOT NULL, PassHash TEXT NOT NULL, Enabled INTEGER NOT NULL, Admin INTEGER NOT NULL, Maildrop INTEGER NOT NULL, MailGatewayID INTEGER);",

            // Manager Service
            // Manager Service distributes configs, receives logs, sends alerts, sends commands to watchdog services.
            @"CREATE TABLE ManagerService (ManagerServiceID INTEGER PRIMARY KEY, DisplayName TEXT, IPAddress TEXT NOT NULL, Port INTEGER NOT NULL, Enabled INTEGER NOT NULL);",

            // Watchdog Service
            // Client Watchdog service monitors services, collects log files, writes config files, reports service status to Manager Service.
            @"CREATE TABLE WatchdogService (WatchdogServiceID INTEGER PRIMARY KEY, DisplayName TEXT, ServiceToken TEXT NOT NULL, Enabled INTEGER NOT NULL);",

            // Template group
            // Defines categories of services that can be configured on Watchdogs
            @"CREATE TABLE TemplateGroup (TemplateGroupID INTEGER PRIMARY KEY, DisplayName TEXT NOT NULL);",

            // Service template
            // Defines a windows service and specifies how it will be controlled or monitored.
            // ControlMode : 0 = Monitor only
            // Controlmode : 1 = Manual Start and Stop commands only.
            // ControlMode : 2 = Force Autorestart if it stops after RestartDelaySec seconds.
            @"CREATE TABLE TemplateService (TemplateServiceID INTEGER PRIMARY KEY, TemplateGroupID INTEGER, DisplayName TEXT NOT NULL, SVCName TEXT NOT NULL, ControlMode INTEGER NOT NULL, RestartDelaySec INTEGER NOT NULL);",

            // File template
            // Defines a file (presumably a config file for a service, but can be for nearly anything) that can be updated, retrieved, etc.
            @"CREATE TABLE TemplateFile (TemplateFileID INTEGER PRIMARY KEY, TemplateGroupID INTEGER, DisplayName TEXT NOT NULL, Path TEXT NOT NULL);",

        };

        public static List<Tuple<string, string, string>> DatabaseDefaultsSystem = new List<Tuple<string, string, string>>()
        {
            // current database version
            new Tuple<string, string, string>("System", "Version", COMPATIBLE_DATABASE_VERSION),
        };

        // temp ID, DisplayName
        public static List<Tuple<string, string>> DatabaseDefaultsTemplateGroup = new List<Tuple<string, string>>()
        {
            new Tuple<string, string>("ef9c3e55f441", "JumpCloud"),
            new Tuple<string, string>("f0b6b78a3ed9", "Security Software")
        };

        // Group temp ID, DisplayName, SVCName, ControlMode, RestartDelaySec
        public static List<Tuple<string, string, string, int, int>> DatabaseDefaultsTemplateService = new List<Tuple<string, string, string, int, int>>()
        {
            new Tuple<string, string, string, int, int>("ef9c3e55f441", "JumpCloud AD Integration Import Agent", "JCADImportAgent", 2, 60),
            new Tuple<string, string, string, int, int>("ef9c3e55f441", "JumpCloud AD Integration Sync Agent", "JCADSyncAgent_DC", 2, 60),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "Quest KACE Agent WatchDog", "AMPWatchDog", 0, -1),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "Quest KACE One Agent", "konea", 0, -1),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "Quest KACE Offline Scheduler", "OfflineScheduler", 0, -1),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "CrowdStrike Falcon Sensor Service", "CSFalconService", 0, -1),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "JumpCloud Agent", "jumpcloud-agent", 2, 120),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "BeyondTrust Remote Support Jump Client", "bomgar-ps-*", 0, -1),
            new Tuple<string, string, string, int, int>("f0b6b78a3ed9", "ThreatLocker Service", "ThreatLockerService", 0, -1)
        };

        // Group temp ID, DisplayName, Path
        public static List<Tuple<string, string, string>> DatabaseDefaultsTemplateFile = new List<Tuple<string, string, string>>()
        {
            new Tuple<string, string, string>("ef9c3e55f441", "JumpCloud AD Integration Import Agent Log File", @"C:\Windows\Temp\JumpCloud_AD_Integration.log"),
            new Tuple<string, string, string>("ef9c3e55f441", "JumpCloud AD Integration Import Agent Configuration File", @"C:\Program Files\JumpCloud AD Bridge\adint.config.json"),
            new Tuple<string, string, string>("ef9c3e55f441", "JumpCloud AD Integration Sync Agent Log File", @"C:\Program Files\JumpCloud\AD Sync\adsync.log"),
            new Tuple<string, string, string>("f0b6b78a3ed9", "Quest KACE One Agent Log File", @"C:\ProgramData\Quest\KACE\konea.log")
        };

        public static string Table_LastRowID = @"SELECT last_insert_rowid();";

        public static string System_GetAll = @"SELECT Category, Setting, Value FROM System ORDER BY Category ASC, Setting ASC;";
        public static string System_Select = @"SELECT Value FROM System WHERE Category = $Category AND Setting = $Setting;";
        public static string System_Insert = @"REPLACE INTO System(Category, Setting, Value) VALUES ($Category, $Setting, $Value);";

        public static string User_GetAll = @"SELECT UserID, DisplayName, Email, Salt, PassHash, Enabled, Admin, Maildrop, MailGatewayID FROM User;";
        public static string User_GetByEmail = @"SELECT UserID, DisplayName, Email, Salt, PassHash, Enabled, Admin, Maildrop, MailGatewayID FROM User WHERE Email = $Email COLLATE NOCASE;";
        public static string User_GetByID = @"SELECT UserID, DisplayName, Email, Salt, PassHash, Enabled, Admin, Maildrop, MailGatewayID FROM User WHERE UserID = $UserID;";
        public static string User_Insert = @"INSERT INTO User(DisplayName, Email, Salt, PassHash, Enabled, Admin, Maildrop, MailGatewayID) VALUES ($DisplayName, $Email, $Salt, $PassHash, $Enabled, $Admin, $Maildrop, $MailGatewayID);";
        public static string User_Update = @"UPDATE User SET DisplayName = $DisplayName, Email = $Email, Salt = $Salt, PassHash = $PassHash, Enabled = $Enabled, Admin = $Admin, Maildrop = $Maildrop, MailGatewayID = $MailGatewayID WHERE UserID = $UserID;";
        public static string User_ClearGatewayByID = @"UPDATE User SET MailGatewayID = NULL WHERE MailGatewayID = $MailGatewayID;";
        public static string User_DeleteByID = @"DELETE FROM User WHERE UserID = $UserID;";

        public static string ManagerService_GetAll = @"SELECT ManagerServiceID, DisplayName, IPAddress, Port, Enabled FROM ManagerService;";
        public static string ManagerService_GetByID = @"SELECT ManagerServiceID, DisplayName, IPAddress, Port, Enabled FROM ManagerService WHERE ManagerServiceID = $ManagerServiceID;";
        public static string ManagerService_Insert = @"INSERT INTO ManagerService(DisplayName, IPAddress, Port, Enabled) VALUES ($DisplayName, $IPAddress, $Port, $Enabled);";
        public static string ManagerService_Update = @"UPDATE ManagerService SET DisplayName = $DisplayName, IPAddress = $IPAddress, Port = $Port, Enabled = $Enabled WHERE ManagerServiceID = $ManagerServiceID;";
        public static string ManagerService_DeleteByID = @"DELETE FROM ManagerService WHERE ManagerServiceID = $ManagerServiceID;";

        public static string WatchdogService_GetAll = @"SELECT WatchdogServiceID, DisplayName, ServiceToken, Enabled FROM WatchdogService;";
        public static string WatchdogService_GetByID = @"SELECT WatchdogServiceID, DisplayName, ServiceToken, Enabled FROM WatchdogService WHERE WatchdogServiceID = $WatchdogServiceID;";
        public static string WatchdogService_Insert = @"INSERT INTO WatchdogService(DisplayName, ServiceToken, Enabled) VALUES ($DisplayName, $ServiceToken, $Enabled);";
        public static string WatchdogService_Update = @"UPDATE WatchdogService SET DisplayName = $DisplayName, ServiceToken = $ServiceToken, Enabled = $Enabled WHERE WatchdogServiceID = $WatchdogServiceID;";
        public static string WatchdogService_DeleteByID = @"DELETE FROM WatchdogService WHERE WatchdogServiceID = $WatchdogServiceID;";

        public static string TemplateGroup_GetAll = @"SELECT TemplateGroupID, DisplayName FROM TemplateGroup;";
        public static string TemplateGroup_GetByID = @"SELECT TemplateGroupID, DisplayName FROM TemplateGroup WHERE TemplateGroupID = $TemplateGroupID;";
        public static string TemplateGroup_Insert = @"INSERT INTO TemplateGroup(DisplayName) VALUES ($DisplayName);";
        public static string TemplateGroup_Update = @"UPDATE TemplateGroup SET DisplayName = $DisplayName WHERE TemplateGroupID = $TemplateGroupID;";
        public static string TemplateGroup_DeleteByID = @"DELETE FROM TemplateGroup WHERE TemplateGroupID = $TemplateGroupID;";

        public static string TemplateService_GetAll = @"SELECT TemplateServiceID, TemplateGroupID, DisplayName, SVCName, ControlMode, RestartDelaySec FROM TemplateService;";
        public static string TemplateService_GetByID = @"SELECT TemplateServiceID, TemplateGroupID, DisplayName, SVCName, ControlMode, RestartDelaySec FROM TemplateService WHERE TemplateServiceID = $TemplateServiceID;";
        public static string TemplateService_GetAllByTemplateGroupID = @"SELECT TemplateServiceID, TemplateGroupID, DisplayName, SVCName, ControlMode, RestartDelaySec FROM TemplateService WHERE TemplateGroupID = $TemplateGroupID;";
        public static string TemplateService_Insert = @"INSERT INTO TemplateService(TemplateGroupID, DisplayName, SVCName, ControlMode, RestartDelaySec) VALUES ($TemplateGroupID, $DisplayName, $SVCName, $ControlMode, $RestartDelaySec);";
        public static string TemplateService_Update = @"UPDATE TemplateService SET TemplateGroupID = $TemplateGroupID, DisplayName = $DisplayName, SVCName = $SVCName, ControlMode = $ControlMode, RestartDelaySec = $RestartDelaySec WHERE TemplateServiceID = $TemplateServiceID;";
        public static string TemplateService_DeleteByID = @"DELETE FROM TemplateService WHERE TemplateServiceID = $TemplateServiceID;";

        public static string TemplateFile_GetAll = @"SELECT TemplateFileID, TemplateGroupID, DisplayName, Path FROM TemplateFile;";
        public static string TemplateFile_GetByID = @"SELECT TemplateFileID, TemplateGroupID, DisplayName, Path FROM TemplateFile WHERE TemplateFileID = $TemplateFileID;";
        public static string TemplateFile_GetAllByTemplateGroupID = @"SELECT TemplateFileID, TemplateGroupID, DisplayName, Path FROM TemplateFile WHERE TemplateGroupID = $TemplateGroupID;";
        public static string TemplateFile_Insert = @"INSERT INTO TemplateFile(TemplateGroupID, DisplayName, Path) VALUES ($TemplateGroupID, $DisplayName, $Path);";
        public static string TemplateFile_Update = @"UPDATE TemplateFile SET TemplateGroupID = $TemplateGroupID, DisplayName = $DisplayName, Path = $Path WHERE TemplateFileID = $TemplateFileID;";
        public static string TemplateFile_DeleteByID = @"DELETE FROM TemplateFile WHERE TemplateFileID = $TemplateFileID;";
    }
}
