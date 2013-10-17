#if __MonoCS__
#else
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using VistaDB;
using VistaDB.Provider;


namespace GreenQloud.Persistence.SQLite
{

    public class SQLiteDatabase
    {
        private VistaDBConnection cnn;
        private static SQLiteDatabase instance = new SQLiteDatabase();

        private SQLiteDatabase()
        {
            cnn = new VistaDBConnection(ConnectionString);
        }

        public static SQLiteDatabase Instance()
        {
            if (instance == null)
                instance = new SQLiteDatabase();
            return instance;
        }


        public static string ConnectionString
        {
            get
            {
                return String.Format(@"Data Source='{0}'", RuntimeSettings.DatabaseFile);
            }
        }

        public void CreateDataBase()
        {
            if (!File.Exists(RuntimeSettings.DatabaseFile))
            {
                string pathFromDatabase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".." + Path.DirectorySeparatorChar.ToString() + "Resources" + Path.DirectorySeparatorChar.ToString() + ConfigFile.GetInstance().Read("DatabaseFile"));
                if (!File.Exists(pathFromDatabase))
                {
                    pathFromDatabase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFile.GetInstance().Read("DatabaseFile"));
                }
                File.Copy(pathFromDatabase, RuntimeSettings.DatabaseFile);
            }
            cnn.Open();
            ExecuteNonQuery("CREATE TABLE Repository (RepositoryID int IDENTITY(1,1) PRIMARY KEY, Path ntext, RECOVERING ntext, RemoteFolder ntext, Active ntext)");
            ExecuteNonQuery("CREATE TABLE RepositoryItem (RepositoryItemID int IDENTITY(1,1) PRIMARY KEY, RepositoryItemKey ntext, RepositoryId ntext, IsFolder ntext, ResultItemId ntext, eTag ntext, eTagLocal ntext,  Moved ntext, UpdatedAt ntext)");
            ExecuteNonQuery("CREATE TABLE EVENT (EventID int IDENTITY(1,1) PRIMARY KEY, ItemId ntext, TYPE ntext, REPOSITORY ntext, SYNCHRONIZED ntext, INSERTTIME ntext, USER ntext, APPLICATION ntext, APPLICATION_VERSION ntext, DEVICE_ID ntext, OS ntext, BUCKET ntext, TRY_QNT ntext, RESPONSE ntext, RepositoryId ntext)");
            ExecuteNonQuery("CREATE TABLE TimeDiff (TimeDiffID int IDENTITY(1,1) PRIMARY KEY, Diff ntext)");
            ExecuteNonQuery("CREATE TABLE RepositoryIgnore (RepositoryIgnoreID int IDENTITY(1,1) PRIMARY KEY, RepositoryId ntext, Path ntext)");
            //ExecuteNonQuery (string.Format("INSERT INTO Repository (Path) VALUES (\"{0}\")", RuntimeSettings.HomePath));
        }

        /// <summary>
        ///     Allows the programmer to run a query against the Database.
        /// </summary>
        /// <param name="sql">The SQL to run</param>
        /// <returns>A DataTable containing the result set.</returns>
        public DataTable GetDataTable(string sql)
        {
            cnn.Open();
            DataTable dt = new DataTable();
            using (VistaDBCommand mycommand = new VistaDBCommand(sql, cnn))
            {
                using (VistaDBDataReader reader = mycommand.ExecuteReader())
                {

                    // Add all the columns.
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        DataColumn col = new DataColumn();
                        col.DataType = reader.GetFieldType(i);
                        col.ColumnName = reader.GetName(i);
                        dt.Columns.Add(col);
                    }
                    while (reader.Read())
                    {
                        DataRow row = dt.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            // Ignore Null fields.
                            if (reader.IsDBNull(i))
                                continue;

                            if (reader.GetFieldType(i) == typeof(String))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetString(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Int16))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetInt16(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Int32))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetInt32(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Int64))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetInt64(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Boolean))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetBoolean(i);
                                ;
                            }
                            else if (reader.GetFieldType(i) == typeof(Byte))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetByte(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Char))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetChar(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(DateTime))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetDateTime(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Decimal))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetDecimal(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Double))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetDouble(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(float))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetFloat(i);
                            }
                            else if (reader.GetFieldType(i) == typeof(Guid))
                            {
                                row[dt.Columns[i].ColumnName] = reader.GetGuid(i);
                            }
                        }

                        dt.Rows.Add(row);
                    }
                    dt.Dispose();
                    return dt;
                }
            }
        }


        /// <summary>
        ///     Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="sql">The SQL to be run.</param>
        /// <returns>An Integer containing the number of rows updated.</returns>
        public int ExecuteNonQuery(string sql)
        {
            return ExecuteNonQuery(sql, false);
        }
        public int ExecuteNonQuery(string sql, bool returnId)
        {
            cnn.Open();
            int result = 0;
            using (VistaDBCommand mycommand = new VistaDBCommand(sql, cnn))
            {
                result = mycommand.ExecuteNonQuery();
                if (returnId)
                {
                    string last_insert_rowid = @"select @@IDENTITY";
                    mycommand.CommandText = last_insert_rowid;
                    System.Object temp = mycommand.ExecuteScalar();
                    int id = int.Parse(temp.ToString());
                    return id;
                }
                return result;
            }
        }

        /// <summary>
        ///     Allows the programmer to retrieve single items from the DB.
        /// </summary>
        /// <param name="sql">The query to run.</param>
        /// <returns>A string.</returns>
        public string ExecuteScalar(string sql)
        {
            cnn.Open();
            object value;
            using (VistaDBCommand mycommand = new VistaDBCommand(sql, cnn))
            {
                value = mycommand.ExecuteScalar();
                if (value != null)
                {
                    return value.ToString();
                }
                return "";
            }
        }

    }
}
#endif
