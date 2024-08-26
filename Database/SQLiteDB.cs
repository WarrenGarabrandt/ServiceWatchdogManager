using SWManager.Model;
using SWManager.Model.DB;
using SWManager.Model.Query;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SWManager.Database
{
    public static class SQLiteDB
    {
        private const string COMPATIBLE_DATABASE_VERSION = "1.0";
        private static BackgroundWorker Worker = null;
        public static bool ConnectionInitialized = false;

        private static BlockingCollection<DatabaseQuery> QueryQueue = new BlockingCollection<DatabaseQuery>();

        private static void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BlockingCollection<DatabaseQuery> queue = e.Argument as BlockingCollection<DatabaseQuery>;
            if (queue == null)
            {
                e.Result = new Exception("No Work Queue Provided.");
                return;
            }
            SQLiteConnection conn = null;
            try
            {
                while (!Worker.CancellationPending)
                {
                    DatabaseQuery query;
                    if (queue.TryTake(out query, 2000))
                    {
                        try
                        {
                            if (query is DatabaseInit)
                            {
                                _initDatabase(ref conn, query as DatabaseInit);
                            }
                            else
                            {
                                if (!_verifyConnection(ref conn))
                                {
                                    query.Abort();
                                }
                                switch (query)
                                {
                                    case qryGetAllConfigValues q:
                                        _system_GetAll(conn, q);
                                        break;
                                    case qryGetConfigValue q:
                                        _system_GetValue(conn, q);
                                        break;
                                    case qrySetConfigValue q:
                                        _system_AddUpdateValue(conn, q);
                                        break;
                                    default:
                                        throw new Exception(string.Format("Unsupported object type: {0}", query.GetType().ToString()));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Error processing query: {0}", ex.Message));
                            query.Abort();
                        }
                    }
                    else
                    {
                        if (conn != null)
                        {
                            try
                            {
                                conn.Dispose();
                            }
                            catch { }
                            conn = null;
                        }
                    }
                }
            }
            finally
            {
                if (conn != null)
                {
                    try
                    {
                        conn.Dispose();
                        conn = null;
                    }
                    catch { }
                }
            }
        }

        private static void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
            {
                System.Diagnostics.Debug.WriteLine(((Exception)e.Result).Message);
            }
            DatabaseQuery q;
            while (QueryQueue.TryTake(out q, 100))
            {
                q.Abort();
            }
        }

        private static string DBPathOverride = null;

        private static string ProgramName = null;

        private static string DatabasePath
        {
            get
            {
                string progdata;
                if (string.IsNullOrEmpty(DBPathOverride))
                {
                    progdata = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    progdata = System.IO.Path.Combine(progdata, ProgramName);
                }
                else
                {
                    progdata = System.IO.Path.GetDirectoryName(DBPathOverride);
                }
                return progdata;
            }
        }

        private static string DatabaseFile
        {
            get
            {
                string filePath;
                if (string.IsNullOrEmpty(DBPathOverride))
                {
                    filePath = System.IO.Path.Combine(DatabasePath, "config.db");
                }
                else
                {
                    filePath = DBPathOverride;
                }
                return filePath;
            }
        }

        private static string _cached_DatabaseConnectionString = null;

        private static string DatabaseConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_cached_DatabaseConnectionString))
                {
                    _cached_DatabaseConnectionString = string.Format(string.Format("Data Source={0}", DatabaseFile));
                }
                return _cached_DatabaseConnectionString;
            }
        }

        #region Public Methods
        /// <summary>
        /// Generates and sets the salt and password hash on a user for a given password.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="newPassword"></param>
        public static void GeneratePasswordHash(tblUser user, string newPassword)
        {
            user.Salt = GenerateNonce(16);
            string Password = string.Format("{0}:{1}", user.Salt, newPassword);
            byte[] passbytes = UTF8Encoding.UTF8.GetBytes(Password);
            using (SHA256 sha = SHA256.Create())
            {
                passbytes = sha.ComputeHash(passbytes);
            }
            user.PassHash = Convert.ToBase64String(passbytes);
        }

        /// <summary>
        /// Compute a password hash for a provided password and verify that it matches the expected value
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static bool ValidatePasswordHash(tblUser user, string password)
        {
            string Password = string.Format("{0}:{1}", user.Salt, password);
            byte[] passbytes = UTF8Encoding.UTF8.GetBytes(Password);
            using (SHA256 sha = SHA256.Create())
            {
                passbytes = sha.ComputeHash(passbytes);
            }
            if (user.PassHash == Convert.ToBase64String(passbytes))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a random string of letters and numbers.
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        public static string GenerateNonce(int len)
        {
            Random rnd = new Random();
            string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                sb.Append(chars[rnd.Next(chars.Length)]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Sets up the connection to the database. Will create a new database if one doesn't exist already.
        /// </summary>
        /// <returns></returns>
        public static WorkerReport InitDatabase(string programName, string dbPath = null)
        {
            if (Worker != null && !Worker.IsBusy)
            {
                Worker = null;
            }
            if (Worker == null)
            {
                Worker = new BackgroundWorker();
                Worker.WorkerReportsProgress = false;
                Worker.WorkerSupportsCancellation = true;
                Worker.DoWork += Worker_DoWork;
                Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                Worker.RunWorkerAsync(QueryQueue);
            }
            DatabaseInit q = new DatabaseInit(programName, dbPath);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Gets all system configuration values.
        /// </summary>
        /// <returns>All config values in the System table</returns>
        public static List<tblSystem> System_GetAll()
        {
            qryGetAllConfigValues q = new qryGetAllConfigValues();
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Returns a setting value from the System table for a given Category and Setting.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="setting"></param>
        /// <returns></returns>
        public static string System_GetValue(string category, string setting)
        {
            qryGetConfigValue q = new qryGetConfigValue(category, setting);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        public static string System_GetDefaultValue(string category, string setting)
        {
            string value = string.Empty;
            foreach (var item in SQLiteStrings.DatabaseDefaultsSystem)
            {
                if (item.Item1 == category && item.Item2 == setting)
                {
                    return item.Item3;
                }
            }

            return value;
        }

        /// <summary>
        /// Creates or updates a setting to a specified value.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="setting"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool System_AddUpdateValue(string category, string setting, string value)
        {
            qrySetConfigValue q = new qrySetConfigValue(category, setting, value);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Gets a list of all user accounts
        /// </summary>
        /// <returns></returns>
        public static List<tblUser> User_GetAll()
        {
            qryGetAllUsers q = new qryGetAllUsers();
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Gets a user by UserID
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public static tblUser User_GetByID(long userID)
        {
            qryGetUserByID q = new qryGetUserByID(userID);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Gets a user by email address
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public static tblUser User_GetByEmail(string email)
        {
            qryGetUserByEmail q = new qryGetUserByEmail(email);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Gets a user by email address and password.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static tblUser User_GetByEmailPassword(string email, string password)
        {
            qryGetUserByEmailPassword q = new qryGetUserByEmailPassword(email, password);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Adds or updates a user in the database.
        /// </summary>
        /// <param name="user"></param>
        public static bool User_AddUpdate(tblUser user)
        {
            qrySetUser q = new qrySetUser(user);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        /// <summary>
        /// Deletes a user from the database specified by UserID
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public static bool User_DeleteByID(long userID)
        {
            qryDeleteUserByID q = new qryDeleteUserByID(userID);
            QueryQueue.Add(q);
            return q.GetResult();
        }

        

        public static string GetFQDN()
        {
            string domainName = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
            string hostName = System.Net.Dns.GetHostName();

            domainName = "." + domainName;
            if (!hostName.EndsWith(domainName))  // if hostname does not already include domain name
            {
                hostName += domainName;   // add the domain name part
            }

            return hostName;                    // return the fully qualified name
        }


        #endregion

        #region Private Methods
        private static bool _verifyConnection(ref SQLiteConnection conn)
        {
            if (!ConnectionInitialized)
            {
                return false;
            }
            if (conn != null)
            {
                if (conn.State == System.Data.ConnectionState.Broken || conn.State == System.Data.ConnectionState.Closed)
                {
                    try
                    {
                        conn.Dispose();
                        conn = null;
                    }
                    catch { }
                }
            }
            if (conn == null)
            {
                try
                {
                    conn = new SQLiteConnection(DatabaseConnectionString);
                    conn.Open();
                }
                catch (Exception ex)
                {
                    ConnectionInitialized = false;
                    System.Diagnostics.Debug.WriteLine(string.Format("Unable to connect to the database. {0}", ex.Message));
                    return false;
                }
            }
            return true;
        }

        private static void _initDatabase(ref SQLiteConnection conn, DatabaseInit query)
        {
            if (conn != null)
            {
                try
                {
                    conn.Dispose();
                }
                catch { }
                conn = null;
            }
            ConnectionInitialized = false;
            ProgramName = query.ProgramName;
            DBPathOverride = query.DBPath;
            _cached_DatabaseConnectionString = null;
            try
            {
                if (!System.IO.File.Exists(DatabaseFile))
                {
                    try
                    {
                        _formatNewDatabase();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format("Error formattting database: {0}", ex.Message));
                    }
                }
                conn = new SQLiteConnection(DatabaseConnectionString);
                conn.Open();
                List<KeyValuePair<string, string>> parms = new List<KeyValuePair<string, string>>();
                parms.Add(new KeyValuePair<string, string>("$Category", "System"));
                parms.Add(new KeyValuePair<string, string>("$Setting", "Version"));
                string value = _runValueQuery(conn, SQLiteStrings.System_Select, parms);
                if (value != COMPATIBLE_DATABASE_VERSION)
                {
                    throw new Exception("Incompatible database version.");
                }
                ConnectionInitialized = true;
                query.SetResult(null);
            }
            catch (Exception ex)
            {
                query.SetResult(new WorkerReport()
                {
                    LogError = string.Format("Unable to start the database. {0}", ex.Message),
                });
                return;
            }
        }

        private static void _formatNewDatabase()
        {
            if (!System.IO.Directory.Exists(DatabasePath))
            {
                System.IO.Directory.CreateDirectory(DatabasePath);
            }
            SQLiteConnection.CreateFile(DatabaseFile);
            var parms = new List<KeyValuePair<string, string>>();
            using (var s = new SQLiteConnection(DatabaseConnectionString))
            {
                s.Open();
                // create all tables
                foreach (string cmdstr in SQLiteStrings.Format_Database)
                {
                    _runNonQuery(s, cmdstr, parms);
                }

                // create default System table values
                foreach (var setting in SQLiteStrings.DatabaseDefaultsSystem)
                {
                    parms.Add(new KeyValuePair<string, string>("$Category", setting.Item1));
                    parms.Add(new KeyValuePair<string, string>("$Setting", setting.Item2));
                    parms.Add(new KeyValuePair<string, string>("$Value", setting.Item3));
                    _runNonQuery(s, SQLiteStrings.System_Insert, parms);
                }

                // create default Template group table values
                Dictionary<string, long> InsertPKs = new Dictionary<string, long>();
                foreach (var setting in SQLiteStrings.DatabaseDefaultsTemplateGroup)
                {
                    // create entry, add primary key to dictionary InsertPKs so we can reference that ID later
                }
                //$TemplateGroupID, $DisplayName, $SVCName, $ControlMode, $RestartDelaySec

                foreach (var setting in SQLiteStrings.DatabaseDefaultsTemplateService)
                {
                    // create entry, looking up TemplateGroupID from InsertPKs
                }

                foreach (var setting in SQLiteStrings.DatabaseDefaultsTemplateFile)
                {
                    // create entry, looking up TemplateGroupID from InsertPKs
                }

                // Create the admin user
                tblUser newAdminUser = new tblUser("Administrator", "admin@local", "", "", true, true, false, null);
                GeneratePasswordHash(newAdminUser, "password");
                qrySetUser q = new qrySetUser(newAdminUser);
                _user_AddUpdate(s, q);

                // Create a default IP Endpoint
                //tblIPEndpoint newEndpoint = new tblIPEndpoint("0.0.0.0", 25, tblIPEndpoint.IPEndpointProtocols.ESMTP, tblIPEndpoint.IPEndpointTLSModes.Disabled, "smtprelay.local", "", false);
                //qrySetIPEndpoint newepq = new qrySetIPEndpoint(newEndpoint);
                //_ipendpoint_AddUpdate(s, newepq);
            }
        }

        /// <summary>
        /// Gets a value from a table. if not found, returns null;
        /// </summary>
        /// <returns></returns>
        private static string _runValueQuery(SQLiteConnection conn, string query, List<KeyValuePair<string, string>> parms)
        {
            string result = null;
            using (var command = conn.CreateCommand())
            {
                command.CommandText = query;
                foreach (var kv in parms)
                {
                    command.Parameters.AddWithValue(kv.Key, kv.Value);
                }
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = reader.GetString(0);
                    }
                }
            }
            return result;
        }

        private static void _runNonQuery(SQLiteConnection conn, string query, List<KeyValuePair<string, string>> parms)
        {
            using (var command = conn.CreateCommand())
            {
                command.CommandText = query;
                foreach (var kv in parms)
                {
                    command.Parameters.AddWithValue(kv.Key, kv.Value);
                }
                command.ExecuteNonQuery();
            }
        }

        private static void _system_GetAll(SQLiteConnection conn, qryGetAllConfigValues query)
        {
            List<tblSystem> results = new List<tblSystem>();
            {
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = SQLiteStrings.System_GetAll;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new tblSystem(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                        }
                    }
                }
            }
            query.SetResult(results);
        }

        private static void _system_GetValue(SQLiteConnection conn, qryGetConfigValue query)
        {
            List<KeyValuePair<string, string>> parms = new List<KeyValuePair<string, string>>();
            parms.Add(new KeyValuePair<string, string>("$Category", query.Category));
            parms.Add(new KeyValuePair<string, string>("$Setting", query.Setting));
            query.SetResult(_runValueQuery(conn, SQLiteStrings.System_Select, parms));
        }

        private static void _system_AddUpdateValue(SQLiteConnection conn, qrySetConfigValue query)
        {
            var parms = new List<KeyValuePair<string, string>>();
            parms.Add(new KeyValuePair<string, string>("$Category", query.Category));
            parms.Add(new KeyValuePair<string, string>("$Setting", query.Setting));
            parms.Add(new KeyValuePair<string, string>("$Value", query.Value));
            _runNonQuery(conn, SQLiteStrings.System_Insert, parms);
            query.SetResult(true);
        }

        private static void _user_GetAll(SQLiteConnection conn, qryGetAllUsers query)
        {
            List<tblUser> results = new List<tblUser>();
            using (var command = conn.CreateCommand())
            {
                command.CommandText = SQLiteStrings.User_GetAll;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new tblUser(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8)));
                    }
                }
            }
            query.SetResult(results);
        }

        private static void _user_GetByID(SQLiteConnection conn, qryGetUserByID query)
        {
            tblUser dbUser = null;
            using (var command = conn.CreateCommand())
            {
                command.CommandText = SQLiteStrings.User_GetByID;
                command.Parameters.AddWithValue("$UserID", query.UserID);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        dbUser = new tblUser(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8));
                    }
                }
            }
            query.SetResult(dbUser);
        }

        private static tblUser _user_GetByEmail(SQLiteConnection conn, string email)
        {
            tblUser result = null;
            using (var command = conn.CreateCommand())
            {
                command.CommandText = SQLiteStrings.User_GetByEmail;
                command.Parameters.AddWithValue("$Email", email);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        result = new tblUser(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8));
                    }
                }
            }
            return result;
        }

        private static void _user_GetByEmail(SQLiteConnection conn, qryGetUserByEmail query)
        {
            query.SetResult(_user_GetByEmail(conn, query.Email));
        }

        private static void _user_GetByEmailPassword(SQLiteConnection conn, qryGetUserByEmailPassword query)
        {
            tblUser user = _user_GetByEmail(conn, query.Email);
            if (user == null)
            {
                query.SetResult(null);
                return;
            }
            if (user.Enabled && ValidatePasswordHash(user, query.Password))
            {
                query.SetResult(user);
            }
            else
            {
                query.SetResult(null);
            }
        }

        private static void _user_AddUpdate(SQLiteConnection conn, qrySetUser query)
        {
            try
            {
                // if the UserID is populated, then we are going to try to update first. 
                // the update might fail, in which case we insert below
                if (query.User.UserID.HasValue)
                {
                    // update
                    tblUser dbUser = null;
                    using (var command = conn.CreateCommand())
                    {
                        command.CommandText = SQLiteStrings.User_GetByID;
                        command.Parameters.AddWithValue("$UserID", query.User.UserID);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dbUser = new tblUser(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8));
                            }
                        }
                    }
                    if (dbUser == null)
                    {
                        // user doesn't exit afterall
                        query.User.UserID = null;
                    }
                    else
                    {
                        using (var command = conn.CreateCommand())
                        {
                            command.CommandText = SQLiteStrings.User_Update;
                            //@"UPDATE User SET DisplayName = $DisplayName, Email = $Email, Salt = $Salt, PassHash = $PassHash, Enabled = $Enabled, Admin = $Admin WHERE UserID = $UserID;"
                            command.Parameters.AddWithValue("$DisplayName", query.User.DisplayName);
                            command.Parameters.AddWithValue("$Email", query.User.Email);
                            command.Parameters.AddWithValue("$Salt", query.User.Salt);
                            command.Parameters.AddWithValue("$PassHash", query.User.PassHash);
                            command.Parameters.AddWithValue("$Enabled", query.User.EnabledInt);
                            command.Parameters.AddWithValue("$Admin", query.User.AdminInt);
                            command.Parameters.AddWithValue("$Maildrop", query.User.MaildropInt);
                            if (query.User.MailGateway.HasValue)
                            {
                                command.Parameters.AddWithValue("$MailGatewayID", query.User.MailGateway);
                            }
                            else
                            {
                                command.Parameters.AddWithValue("$MailGatewayID", DBNull.Value);
                            }
                            command.Parameters.AddWithValue("$UserID", query.User.UserID);
                            command.ExecuteNonQuery();
                        }
                    }
                }

                // if there is no UserID, then we insert a new record and select the ID back.
                if (!query.User.UserID.HasValue)
                {
                    // insert new record and read back the ID
                    using (var command = conn.CreateCommand())
                    {
                        command.CommandText = SQLiteStrings.User_Insert;
                        //@"INSERT INTO User(DisplayName, Email, Salt, PassHash, Enabled, Admin) VALUES ($DisplayName, $Email, $Salt, $PassHash, $Enabled, $Admin);"
                        command.Parameters.AddWithValue("$DisplayName", query.User.DisplayName);
                        command.Parameters.AddWithValue("$Email", query.User.Email);
                        command.Parameters.AddWithValue("$Salt", query.User.Salt);
                        command.Parameters.AddWithValue("$PassHash", query.User.PassHash);
                        command.Parameters.AddWithValue("$Enabled", query.User.EnabledInt);
                        command.Parameters.AddWithValue("$Admin", query.User.AdminInt);
                        command.Parameters.AddWithValue("$Maildrop", query.User.MaildropInt);
                        if (query.User.MailGateway.HasValue)
                        {
                            command.Parameters.AddWithValue("$MailGatewayID", query.User.MailGateway);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("$MailGatewayID", DBNull.Value);
                        }
                        command.ExecuteNonQuery();
                    }
                    using (var command = conn.CreateCommand())
                    {
                        command.CommandText = SQLiteStrings.Table_LastRowID;
                        query.User.UserID = (long)command.ExecuteScalar();
                    }
                }
                query.SetResult(true);
            }
            catch (Exception ex)
            {
                query.SetResult(false);
                throw ex;
            }
        }

        private static void _user_DeleteByID(SQLiteConnection conn, qryDeleteUserByID query)
        {
            using (var command = conn.CreateCommand())
            {
                command.CommandText = SQLiteStrings.User_DeleteByID;
                command.Parameters.AddWithValue("$UserID", query.UserID);
                command.ExecuteNonQuery();
            }
            query.SetResult(true);
        }

        private static void _managerService_GetAll(SQLiteConnection conn)
        {

        }

        #endregion

    }
}
