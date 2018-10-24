using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Miniblog.Core.Helper
{
    public abstract class SqlHelper
    {
        /*連線字串跟DBPrividerFactory設定在Startup.cs*/
        public static string connectionString;
        public static System.Data.Common.DbProviderFactory dbProviderFactory ;
        public static IDbConnection CreateDefaultConnection()
        {
            var conn = dbProviderFactory.CreateConnection();
            conn.ConnectionString = connectionString;
            conn.Open();
            return conn;
        }
    }
}
