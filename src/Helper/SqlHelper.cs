﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Miniblog.Core.Helper
{
    public abstract class SQLHelper
    {
        public static string connectionString ;
        private static readonly System.Data.Common.DbProviderFactory dbProviderFactory = System.Data.SqlClient.SqlClientFactory.Instance;
        public static IDbConnection CreateDefaultConnection()
        {
            var conn = dbProviderFactory.CreateConnection();
            conn.ConnectionString = connectionString;
            conn.Open();
            return conn;
        }
    }
}
