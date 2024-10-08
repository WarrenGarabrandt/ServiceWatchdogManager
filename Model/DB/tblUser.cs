﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWManager.Model.DB
{
    public class tblUser
    {

        /// <summary>
        /// Creates a new instance that has not been saved in the database.
        /// </summary>
        public tblUser(string displayName, string email, string salt, string passHash, bool enabled, bool admin, bool maildrop, long? mailGateway)
        {
            UserID = null;
            DisplayName = displayName;
            Email = email;
            Salt = salt;
            PassHash = passHash;
            Enabled = enabled;
            Admin = admin;
            MailGateway = mailGateway;
            Maildrop = maildrop;
        }

        /// <summary>
        /// Creates a new instance that exists in the database.
        /// </summary>
        public tblUser(long userID, string displayName, string email, string salt, string passHash, int enabled, int admin, int maildrop, long? mailGateway)
        {
            UserID = userID;
            DisplayName = displayName;
            Email = email;
            Salt = salt;
            PassHash = passHash;
            EnabledInt = enabled;
            AdminInt = admin;
            MaildropInt = maildrop;
            MailGateway = mailGateway;
        }

        public override string ToString()
        {
            return string.Format("{0} <{1}>", DisplayName, Email);
        }

        /// <summary>
        /// Generated when record is inserted. 
        /// On Save: Null = INSERT, NotNull = UPDATE
        /// </summary>
        public long? UserID { get; set; }

        public string DisplayName { get; set; }

        public string Email { get; set; }

        public string Salt { get; set; }

        public string PassHash { get; set; }

        public bool Enabled { get; set; }
        public int EnabledInt
        {
            get
            {
                return Enabled ? 1 : 0;
            }
            set
            {
                Enabled = value > 0;
            }
        }
        public bool Admin { get; set; }

        public int AdminInt
        {
            get
            {
                return Admin ? 1 : 0;
            }
            set
            {
                Admin = value > 0;
            }
        }

        public bool Maildrop { get; set; }

        public int MaildropInt
        {
            get
            {
                return Maildrop ? 1 : 0;
            }
            set
            {
                Maildrop = value > 0;
            }
        }

        public long? MailGateway { get; set; }

    }
}
