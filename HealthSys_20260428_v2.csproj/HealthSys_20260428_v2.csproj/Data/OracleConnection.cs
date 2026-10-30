using Microsoft.Extensions.Configuration;
using ManagedOracleConnection = Oracle.ManagedDataAccess.Client.OracleConnection;
using ManagedOracleDataAdapter = Oracle.ManagedDataAccess.Client.OracleDataAdapter;
using ManagedOracleCommand = Oracle.ManagedDataAccess.Client.OracleCommand;
using ManagedOracleParameter = Oracle.ManagedDataAccess.Client.OracleParameter;
using ManagedOracleDbType = Oracle.ManagedDataAccess.Client.OracleDbType;
using System.Data;
using System.Data.Common;


namespace HealthSys_20260428_v2.csproj.Data;

public sealed class OracleParameterValue
{
    private OracleParameterValue(object? value, ManagedOracleDbType oracleDbType, int? size = null)
    {
        Value = value;
        OracleDbType = oracleDbType;
        Size = size;
    }

    public object? Value { get; }

    public ManagedOracleDbType OracleDbType { get; }

    public int? Size { get; }

    public static OracleParameterValue Char(string value, int size) =>
        new(value.PadRight(size), ManagedOracleDbType.Char, size);

    public static OracleParameterValue Varchar2(string value, int size) =>
        new(value, ManagedOracleDbType.Varchar2, size);

    public static OracleParameterValue Number(decimal value) =>
        new(value, ManagedOracleDbType.Decimal);
}

public sealed class OracleTransactionCommand
{
    public OracleTransactionCommand(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        bool allowZeroAffectedRows = false)
    {
        Sql = sql;
        Parameters = parameters;
        AllowZeroAffectedRows = allowZeroAffectedRows;
    }

    public string Sql { get; }

    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public bool AllowZeroAffectedRows { get; }
}

public sealed class OracleConnectionFactory
{
    private const bool DefaultWriteUsesTestConnection = true;

    static OracleConnectionFactory()
    {
        // ODP.NET Core data-source aliases (the equivalent of oracle.manageddataaccess.client).
        Oracle.ManagedDataAccess.Client.OracleConfiguration.OracleDataSources.Add(
            "DBGEN",
            "(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=femhrac-scan.ad.femh.local)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=DBGEN)))");
        Oracle.ManagedDataAccess.Client.OracleConfiguration.OracleDataSources.Add(
            "DBGENTEST",
            "(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=femhtest-scan.ad.femh.local)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=DBGEN)))");
        Oracle.ManagedDataAccess.Client.OracleConfiguration.OracleDataSources.Add(
            "DBGENDEV",
            "(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=femhdev-scan.ad.femh.local)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=DBTEST3)))");
        Oracle.ManagedDataAccess.Client.OracleConfiguration.OracleDataSources.Add(
            "SampleDataSource",
            "(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCL)))");
    }

    private string conn_dbgen()
    {
        throw new InvalidOperationException("示範模式已停用資料庫連線。");
      
    }

    private string conn_dbtest()
    {
        throw new InvalidOperationException("示範模式已停用資料庫連線。");
    
    }

    private string conn_dbtest3()
    {
        throw new InvalidOperationException("示範模式已停用資料庫連線。");
   
    }

    private ManagedOracleConnection GETOracleConnection(string dbconstr = "", bool TestConnection = false)
    {
        if (TestConnection == true)
        {
            dbconstr = conn_dbtest3();
        }
        else
        {
            if (dbconstr == "")
            {
                dbconstr = conn_dbgen();
            }           
        }

        ManagedOracleConnection cn = new ManagedOracleConnection();
        cn.ConnectionString = dbconstr;
        return cn;
    }

    public ManagedOracleConnection CreateDbGenConnection()
    {
        var connection = new ManagedOracleConnection
        {
            ConnectionString = conn_dbgen()
        };
        return connection;
    }

    public DataTable GETDataTable(string sql, string dbconstr = "", bool TestConnection = false)
    {
        try
        {
            using (ManagedOracleConnection cn = GETOracleConnection(dbconstr, TestConnection))
            {
                return GETDataTable(sql, cn);
            } 
        }
        catch (Exception ex)
        {
            BreakOnDatabaseError(ex, sql, TestConnection);
            throw;
        }
    }

    public DataTable GETDataTable(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        string dbconstr = "",
        bool TestConnection = false,
        int commandTimeoutSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (ManagedOracleConnection cn = GETOracleConnection(dbconstr, TestConnection))
            {
                cn.Open();
                cancellationToken.ThrowIfCancellationRequested();

                using (ManagedOracleCommand cmd = cn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = sql;
                    cmd.BindByName = true;

                    if (commandTimeoutSeconds > 0)
                    {
                        cmd.CommandTimeout = commandTimeoutSeconds;
                    }

                    AddParameters(cmd, parameters);

                    using var cancellationRegistration = cancellationToken.CanBeCanceled
                        ? cancellationToken.Register(static commandState =>
                        {
                            try
                            {
                                ((ManagedOracleCommand)commandState!).Cancel();
                            }
                            catch
                            {
                                // The command may already be complete or disposed.
                            }
                        }, cmd)
                        : default;

                    using (ManagedOracleDataAdapter adapter = new ManagedOracleDataAdapter(cmd))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        DataSet ds = new DataSet();
                        adapter.Fill(ds, "temp");
                        cancellationToken.ThrowIfCancellationRequested();
                        return ds.Tables[0];
                    }
                }
            }
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
            when (ex.Number == 1013 && cancellationToken.IsCancellationRequested)
        {
            // Oracle reports Cancel() as ORA-01013; preserve the caller's cancellation reason.
            throw new OperationCanceledException("Oracle query was canceled.", ex, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            BreakOnDatabaseError(ex, sql, TestConnection, parameters);
            throw;
        }
    }

    public DataTable GETDbGenDataTable(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        int commandTimeoutSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        return GETDataTable(
            sql,
            parameters,
            conn_dbgen(),
            false,
            commandTimeoutSeconds,
            cancellationToken);
    }

    private DataTable GETDataTable(string sql, ManagedOracleConnection cn)
    {
        DataSet ds = new DataSet();
        ManagedOracleDataAdapter Adp = new ManagedOracleDataAdapter(sql, cn);
        Adp.Fill(ds, "temp");
        return ds.Tables[0];
    }

    public int ExecuteNonQuery(string exesql) =>
        ExecuteNonQuery(exesql, "", DefaultWriteUsesTestConnection);

    public int ExecuteNonQuery(string exesql, IReadOnlyDictionary<string, object?> parameters) =>
        ExecuteNonQuery(exesql, parameters, "", DefaultWriteUsesTestConnection);

    public int[] ExecuteNonQueryTransaction(
        IReadOnlyList<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> commands,
        string dbconstr = "",
        bool TestConnection = DefaultWriteUsesTestConnection)
    {
        return ExecuteNonQueryTransaction(
            commands
                .Select(command => new OracleTransactionCommand(command.Sql, command.Parameters))
                .ToArray(),
            dbconstr,
            TestConnection);
    }

    public int[] ExecuteNonQueryTransaction(
        IReadOnlyList<OracleTransactionCommand> commands,
        string dbconstr = "",
        bool TestConnection = DefaultWriteUsesTestConnection)
    {
        if (commands.Count == 0)
        {
            return [];
        }

        try
        {
            using (ManagedOracleConnection cn = GETOracleConnection(dbconstr, TestConnection))
            {
                cn.Open();

                using (var transaction = cn.BeginTransaction())
                {
                    var results = new int[commands.Count];
                    OracleTransactionCommand? currentCommand = null;
                    var currentCommandIndex = -1;

                    try
                    {
                        for (var index = 0; index < commands.Count; index++)
                        {
                            var command = commands[index];
                            currentCommand = command;
                            currentCommandIndex = index;

                            if (string.IsNullOrWhiteSpace(command.Sql))
                            {
                                throw new InvalidOperationException("交易 SQL 不可為空。");
                            }

                            using (ManagedOracleCommand cmd = cn.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandType = CommandType.Text;
                                cmd.CommandText = command.Sql;
                                cmd.BindByName = true;

                                AddParameters(cmd, command.Parameters);

                                var affectedRows = cmd.ExecuteNonQuery();
                                if (affectedRows < 1 && !command.AllowZeroAffectedRows)
                                {
                                    throw new InvalidOperationException($"交易第 {index + 1} 筆 SQL 未寫入任何資料。");
                                }

                                results[index] = affectedRows;
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        BreakOnDatabaseError(
                            ex,
                            currentCommand?.Sql ?? string.Empty,
                            TestConnection,
                            currentCommand?.Parameters,
                            currentCommandIndex);
                        transaction.Rollback();
                        throw;
                    }

                    return results;
                }
            }
        }
        catch
        {
            throw;
        }
    }

    public int ExecuteNonQuery(string exesql, string dbconstr = "", bool TestConnection = DefaultWriteUsesTestConnection)
    {
        try
        {
            using (ManagedOracleConnection cn = GETOracleConnection(dbconstr, TestConnection))
            {
                cn.Open();

                using (ManagedOracleCommand cmd = cn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = exesql;

                    return cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            BreakOnDatabaseError(ex, exesql, TestConnection);
            throw;
        }
    }

    public int ExecuteNonQuery(string exesql, IReadOnlyDictionary<string, object?> parameters, string dbconstr = "", bool TestConnection = DefaultWriteUsesTestConnection)
    {
        try
        {
            using (ManagedOracleConnection cn = GETOracleConnection(dbconstr, TestConnection))
            {
                cn.Open();

                using (ManagedOracleCommand cmd = cn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = exesql;
                    cmd.BindByName = true;

                    AddParameters(cmd, parameters);

                    return cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            BreakOnDatabaseError(ex, exesql, TestConnection, parameters);
            throw;
        }
    }

    private static void AddParameters(ManagedOracleCommand cmd, IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.Value is OracleParameterValue typedValue)
            {
                var oracleParameter = new ManagedOracleParameter(parameter.Key, typedValue.OracleDbType)
                {
                    Value = typedValue.Value ?? DBNull.Value
                };

                if (typedValue.Size.HasValue)
                {
                    oracleParameter.Size = typedValue.Size.Value;
                }

                cmd.Parameters.Add(oracleParameter);
                continue;
            }

            cmd.Parameters.Add(new ManagedOracleParameter(parameter.Key, parameter.Value ?? DBNull.Value));
        }
    }

    private static void BreakOnDatabaseError(
        Exception ex,
        string sql,
        bool testConnection,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int commandIndex = -1)
    {
        // Request cancellation and CommandTimeout are expected control flow, including in VS.
        if (ex is OperationCanceledException
            || ex is Oracle.ManagedDataAccess.Client.OracleException { Number: 1013 })
        {
            return;
        }

        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }
    }


}
