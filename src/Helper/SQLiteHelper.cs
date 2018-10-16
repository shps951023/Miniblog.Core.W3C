using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace Miniblog.Core.Helper
{
    public abstract class SQLiteHelper
    {
        public static string connectionString;
        private static readonly System.Data.Common.DbProviderFactory dbProviderFactory = System.Data.SQLite.SQLiteFactory.Instance;
        public static IDbConnection CreateDefaultConnection()
        {
            var conn = dbProviderFactory.CreateConnection();
            conn.ConnectionString = connectionString;
            conn.Open();
            return conn;
        }
    }
}
