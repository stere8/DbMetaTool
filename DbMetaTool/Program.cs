using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;
using System;
using System.Data;
using System.IO;
using System.Text;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            FbConnectionStringBuilder csb = new()
            {
                DataSource = "localhost",
                Database = Path.Combine(databaseDirectory, "database.fdb"),
                UserID = "SYSDBA",
                Password = "masterkey",
                ServerType = FbServerType.Default
            };
            string connectionString = csb.ToString();
            Console.WriteLine($"Connextion string: {connectionString}");
            FbConnection.CreateDatabase(connectionString);
            Console.WriteLine("Creation succeded");
            DirectoryInfo scriptsDirInfo = new(scriptsDirectory);

            List<string> domainScripts = new List<string>();
            List<string> tableScripts = new List<string>();
            List<string> procedureScripts = new List<string>();

            foreach (var file in scriptsDirInfo.GetFiles("*.sql"))
            {
                string fileName = file.Name.ToLowerInvariant();
                if (fileName.Contains("domain"))
                {
                    domainScripts.Add(file.FullName);
                    Console.WriteLine($"{file.FullName} added to domains list");
                }
                else if (fileName.Contains("table"))
                {
                    tableScripts.Add(file.FullName);
                    Console.WriteLine($"{file.FullName} added to tables list");
                }
                else if (fileName.Contains("procedure"))
                {
                    procedureScripts.Add(file.FullName);
                    Console.WriteLine($"{file.FullName} added to procedure list");
                }
                else
                    Console.WriteLine($"Nieznany typ skryptu: {file.Name}");
                Console.WriteLine($"found sql {file.FullName}");
            }

            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                // 1. Domains
                ExecuteScripts(domainScripts, connection);

                // 2. Tables
                ExecuteScripts(tableScripts, connection);

                // 3. Procedures
                Console.WriteLine("Doing Procedures");

                string sb = BuildProceduresSqlWithSetTerm(procedureScripts);
                Console.WriteLine($"Doing Procedures Concanated");
                var fbep = new FbBatchExecution(connection);
                var fbScriptp = new FbScript(sb);
                fbScriptp.Parse();
                fbep.AppendSqlStatements(fbScriptp);
                fbep.Execute();
            }
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            using var connection = new FbConnection(connectionString);
            connection.Open();

            // 1. Export Domains
            var dt = connection.GetSchema("Domains");
            foreach (System.Data.DataRow row in dt.Rows)
            {
                string name = row["DOMAIN_NAME"].ToString().Trim();
                // 1. Filter: Skip system domains
                if (name.StartsWith("RDB$") || name.StartsWith("MON$") || name.StartsWith("SEC$")) continue;

                // 2. Defensive Read: Check for DBNull before reading attributes
                string type = row["DOMAIN_DATA_TYPE"]?.ToString();
                // Simple SQL reconstruction (expand logic for precision/scale as needed)
                string sql = $"CREATE DOMAIN {name} AS {type};";

                File.WriteAllText(Path.Combine(outputDirectory, $"DOMAIN_{name}.sql"), sql);
                Console.WriteLine($"Exported: {name} Domains");
            }

            // 2. Export Tables
            var tables = connection.GetSchema("Tables");
            foreach (System.Data.DataRow row in tables.Rows)
            {
                string tableName = row["TABLE_NAME"].ToString().Trim();
                if (tableName.StartsWith("RDB$") || tableName.StartsWith("MON$") || tableName.StartsWith("SEC$")) continue;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"CREATE TABLE {tableName} (");

                // Get Columns for this table
                var columns = connection.GetSchema(
                    "Columns",
                    new[] { null, null, tableName, null }
                );
                List<string> colDefs = new List<string>();

                foreach (System.Data.DataRow col in columns.Rows)
                {
                    string colName = col["COLUMN_NAME"].ToString().Trim();
                    string type = col["COLUMN_DATA_TYPE"].ToString().Trim();

                    // Handle length/precision
                    if (col["COLUMN_SIZE"] != DBNull.Value && type.Contains("CHAR"))
                        type += $"({col["COLUMN_SIZE"]})";
                    else if (col["NUMERIC_PRECISION"] != DBNull.Value && type.Contains("DECIMAL"))
                        type += $"({col["NUMERIC_PRECISION"]},{col["NUMERIC_SCALE"]})";

                    string definition = $"    {colName} {type}";

                    // Nullable check
                    if (col["IS_NULLABLE"] != DBNull.Value && (bool)col["IS_NULLABLE"] == false)
                        definition += " NOT NULL";

                    colDefs.Add(definition);
                }

                sb.AppendLine(string.Join(",\n", colDefs));
                sb.AppendLine(");");

                File.WriteAllText(Path.Combine(outputDirectory, $"TABLE_{tableName}.sql"), sb.ToString());
                Console.WriteLine($"Exported: {tableName} Tables");
            }

            // 3. Export Procedures with try-catch for debugging
            try
            {
                Console.WriteLine("Starting procedure export...");

                // Use a simpler query first to debug
                using var cmd = new FbCommand(
                    "SELECT RDB$PROCEDURE_NAME, RDB$PROCEDURE_SOURCE " +
                    "FROM RDB$PROCEDURES " +
                    "WHERE RDB$SYSTEM_FLAG = 0 OR RDB$SYSTEM_FLAG IS NULL",
                    connection);

                Console.WriteLine($"Command text: {cmd.CommandText}");

                using var reader = cmd.ExecuteReader();

                Console.WriteLine($"Reader field count: {reader.FieldCount}");
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Console.WriteLine($"  Column {i}: {reader.GetName(i)}");
                }

                int procedureCount = 0;
                while (reader.Read())
                {
                    procedureCount++;
                    Console.WriteLine($"Processing procedure #{procedureCount}");

                    try
                    {
                        if (!HasColumn(reader, "RDB$PROCEDURE_NAME"))
                        {
                            Console.WriteLine("Warning: Column RDB$PROCEDURE_NAME not found");
                            continue;
                        }

                        string name = reader["RDB$PROCEDURE_NAME"].ToString().Trim();
                        Console.WriteLine($"Found procedure: {name}");

                        // Check if source column exists
                        if (!HasColumn(reader, "RDB$PROCEDURE_SOURCE"))
                        {
                            Console.WriteLine($"Warning: Column RDB$PROCEDURE_SOURCE not found for procedure {name}");
                            continue;
                        }

                        object sourceObj = reader["RDB$PROCEDURE_SOURCE"];
                        if (sourceObj == DBNull.Value || sourceObj == null)
                        {
                            Console.WriteLine($"Skipping {name} (Metadata source is NULL)");
                            continue;
                        }

                        string source = sourceObj.ToString();

                        // Reconstruct the full SQL with terminators
                        string sql = $"SET TERM ^ ;\nCREATE OR ALTER PROCEDURE {name}\n{source}\n^\nSET TERM ; ^";

                        string fileName = Path.Combine(outputDirectory, $"PROC_{name}.sql");
                        File.WriteAllText(fileName, sql);
                        Console.WriteLine($"Exported Procedure: {name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing procedure row: {ex.Message}");
                    }
                }

                if (procedureCount == 0)
                {
                    Console.WriteLine("No procedures found in the database.");
                }
                else
                {
                    Console.WriteLine($"Total procedures processed: {procedureCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting procedures: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        //check if a column exists
        private static bool HasColumn(IDataRecord record, string columnName)
        {
            try
            {
                return record.GetOrdinal(columnName) >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            using var connection = new FbConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                var domainScripts = new List<string>();
                var tableScripts = new List<string>();
                var procedureScripts = new List<string>();

                foreach (var file in Directory.GetFiles(scriptsDirectory, "*.sql"))
                {
                    var name = Path.GetFileName(file).ToLowerInvariant();

                    if (name.Contains("domain"))
                        domainScripts.Add(file);
                    else if (name.Contains("table"))
                        tableScripts.Add(file);
                    else if (name.Contains("proc"))
                        procedureScripts.Add(file);
                }

                ExecuteScripts(domainScripts, connection);
                ExecuteScripts(tableScripts, connection);

                if (procedureScripts.Count > 0)
                {
                    string sql = BuildProceduresSqlWithSetTerm(procedureScripts);

                    var fbe = new FbBatchExecution(connection);
                    var script = new FbScript(sql);
                    script.Parse();
                    fbe.AppendSqlStatements(script);
                    fbe.Execute();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        static void ExecuteScripts(IEnumerable<string> scripts, FbConnection connection)
        {
            foreach (var file in scripts)
            {
                Console.WriteLine($"Executing {Path.GetFileName(file)}");

                var fbe = new FbBatchExecution(connection);
                var script = new FbScript(File.ReadAllText(file));
                script.Parse();
                fbe.AppendSqlStatements(script);
                fbe.Execute();
            }
        }

        static string BuildProceduresSqlWithSetTerm(IEnumerable<string> procedureScripts)
        {
            StringBuilder sb = new();
            foreach (var file in procedureScripts)
            {
                sb.AppendLine("SET TERM ^ ;");
                sb.AppendLine(File.ReadAllText(file));
                sb.AppendLine("^");
                sb.AppendLine("SET TERM ; ^");
            }
            return sb.ToString();
        } 
    }
}

