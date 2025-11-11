using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace wws_web.Data
{
    public class SqliteDbHandler
    {
        private readonly string _dbFilePath = @"C:\wws\wws.db3";

        // Ensure the database schema is created if the file doesn't exist.
        public void InitializeDatabase()
        {
            if (!File.Exists(_dbFilePath))
            {
                CreateDatabase();
            }
        }

        private void CreateDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    // Create `occupiers` table
                    command.CommandText = @"
                        CREATE TABLE occupiers (
                            occTag CHAR(21),
                            occName CHAR(21)
                        );";
                    command.ExecuteNonQuery();

                    // Create `reports` table
                    command.CommandText = @"
                        CREATE TABLE reports (
                            repDate CHAR(10),
                            repTime CHAR(8),
                            repGross CHAR(10),
                            repWaste CHAR(20),
                            repOccupier CHAR(20)
                        );";
                    command.ExecuteNonQuery();
                }
            }
        }

        // Query for testing connectivity (Optional)
        public bool CheckConnection()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_dbFilePath}"))
                {
                    connection.Open();
                    return connection.State == System.Data.ConnectionState.Open;
                }
            }
            catch
            {
                Console.WriteLine("Unable to connect to SQLite database.");
                return false;
            }
        }
    }
}
