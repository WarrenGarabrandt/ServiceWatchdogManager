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

        };

        public static List<Tuple<string, string, string>> DatabaseDefaults = new List<Tuple<string, string, string>>()
        {
            // current database version
            new Tuple<string, string, string>("System", "Version", COMPATIBLE_DATABASE_VERSION),
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
                
    }
}
