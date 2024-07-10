using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;


namespace YaGpt
{
    public static class SqlGeneric
    {
       

        public static OracleCommand SetParameters(this OracleCommand cmd, string text, params object[] args)
        {
            if (text.EndsWith(';'))
                text = text.Substring(0, text.Length - 1);

            if (args?.Length > 0)
            {
                Dictionary<string, object> parameters = new();

                var cursor = args.GetEnumerator();
                while (cursor.MoveNext())
                {
                    var a = cursor.Current;
                    if (a is OracleParameter)
                        parameters.Add((a as OracleParameter).ParameterName, a);
                    else if (cursor.MoveNext())
                        parameters.Add(a as string, cursor.Current ?? DBNull.Value);
                }

                var nmArray = Regex.Matches(text, "(?<e>:[a-zA-Z0-9-_]+)")?.Select(m => m.Groups["e"]?.Value?.Substring(1));
                if (nmArray?.Count() > 0)
                    foreach (var nmItem in nmArray)
                    {
                        var param = parameters.FirstOrDefault(m => m.Key.Equals(nmItem, StringComparison.OrdinalIgnoreCase));
                        if (param.Value != null)
                        {
                            if (param.Value is OracleParameter)
                                cmd.Parameters.Add(param.Value);
                            else
                                cmd.Parameters.Add(param.Key, param.Value);
                        }
                    }
                else
                {
                    List<string> varList = new();
                    foreach (var param in parameters)
                    {
                        if (param.Value is OracleParameter)
                        {
                            cmd.Parameters.Add(param.Value);
                            var op = param.Value as OracleParameter;
                            if (op.Direction == ParameterDirection.ReturnValue)
                                text = $":{op.ParameterName} := {text}";
                            else
                                varList.Add(op.ParameterName);
                        }
                        else
                        {
                            cmd.Parameters.Add(param.Key, param.Value);
                            varList.Add(param.Key);
                        }
                    }
                    if (varList.Count > 0)
                    {
                        if (text.EndsWith("()"))
                            text = text.Substring(0, text.Length - 2);
                        text = text + "(" + string.Join(", ", varList.Select(m => $"{m} => :{m}")) + ")";
                    }
                }
            }

            if (text.Contains("SELECT ", StringComparison.OrdinalIgnoreCase))
                cmd.CommandText = $"{text};";
            else if (text.Contains("INSERT ", StringComparison.OrdinalIgnoreCase)
                  || text.Contains("DELETE ", StringComparison.OrdinalIgnoreCase)
                  || text.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase))
            {
                if (text.Contains("COMMIT"))
                    cmd.CommandText = $"BEGIN\n{text};\nEND;";
                else
                    cmd.CommandText = $"BEGIN\n{text};\nCOMMIT;\nEND;";
            }
            else
                cmd.CommandText = $"BEGIN\n{text};\nEND;";

            return cmd;
        }
    }

    public static class SQL
    {
        public interface ITConnection : IDisposable
        {
            OracleConnection conn { get; set; }
            string GetDbConnStr();
            void Init(string connStr);
        }

        public static void InitDB()
        {
            using (TConnection conn = new())
                foreach (var cmd in Sql.Templates.InitObjects)
                    ExecuteText(conn, cmd);
        }

        public class TConnection : ITConnection
        {
            public OracleConnection conn { get; set; }

            public string GetDbConnStr()
            {
                return conn?.ConnectionString;
            }

            public void Dispose()
            {
                conn?.Close();
                conn?.Dispose();
            }

            public void Init(string connStr)
            {
                if (conn?.State != ConnectionState.Open)
                {
                    conn?.Close();
                    conn?.Dispose();
                    conn = new OracleConnection(connStr);
                    conn.KeepAlive = true;
                    conn.Open();
                }
            }
        }

        public static object GetValue(ITConnection sqlEntry, string cmdText)
        {
            try
            {
                sqlEntry.Init(Const.Config.db_conn);

                using (var cur = sqlEntry.conn.CreateCommand())
                {
                    cur.CommandText = cmdText;
                    cur.CommandType = CommandType.Text;

                    using (var reader = cur.ExecuteReader())
                        if (reader.HasRows)
                            while (reader.Read())
                                return reader.GetValue(0);

                }

                return null;
            }
            catch (Exception e)
            {
                throw new ErrorsHelper.SqlException(cmdText, e);
            }
        }


        public static List<Dictionary<string, object>> GetRows(ITConnection sqlEntry, string cmdText)
        {
            try
            {
                sqlEntry.Init(Const.Config.db_conn);
                List<Dictionary<string, object>> result = new();

                using (var cur = sqlEntry.conn.CreateCommand())
                {
                    cur.CommandText = cmdText;
                    cur.CommandType = CommandType.Text;

                    using (var reader = cur.ExecuteReader())
                        if (reader.HasRows)
                        {
                            var columns = reader.GetColumnSchema();
                            while (reader.Read())
                            {
                                Dictionary<string, object> row = new(columns.Count);
                                foreach (var c in columns)
                                    row.Add(c.ColumnName.ToUpper(), reader[c.ColumnName]);

                                result.Add(row);
                            }

                        }
                }

                return result;
            }
            catch (Exception e)
            {
                throw new ErrorsHelper.SqlException(cmdText, e);
            }
        }

        public static object ExecuteCommand(ITConnection sqlEntry, string cmdText, params object[] parameters)
        {
            try
            {

                sqlEntry.Init(Const.Config.db_conn);
                object result = null;

                using (var cmd = sqlEntry.conn.CreateCommand())
                {
                    cmd.CommandTimeout = 5;
                    var res = cmd.SetParameters(cmdText, parameters).ExecuteNonQuery();

                    foreach (OracleParameter p in cmd.Parameters)
                        if (p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.ReturnValue)
                        {
                            result = p.Value;

                            switch (p.OracleDbType)
                            {
                                case OracleDbType.Clob:
                                    result = ((Oracle.ManagedDataAccess.Types.OracleClob)result).Value;
                                    break;
                                case OracleDbType.Int64:
                                case OracleDbType.Int32:
                                case OracleDbType.Decimal:
                                    result = ((Oracle.ManagedDataAccess.Types.OracleDecimal)result).Value;
                                    break;
                                case OracleDbType.Varchar2:
                                case OracleDbType.NVarchar2:
                                    result = ((Oracle.ManagedDataAccess.Types.OracleString)result).Value;
                                    break;
                                default: break;
                            }

                            break;
                        }

                }

                return result;
            }
            catch (Exception e)
            {
                throw new ErrorsHelper.SqlException(cmdText, e);
            }
        }

        public static void ExecuteText(ITConnection sqlEntry, string cmdText)
        {
            try
            {
                sqlEntry.Init(Const.Config.db_conn);

                using (var cmd = sqlEntry.conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = cmdText;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                throw new ErrorsHelper.SqlException(cmdText, e);
            }
        }
    }
}