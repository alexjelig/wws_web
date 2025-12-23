using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Collections.Generic;
using wws_web.Models;

namespace wws_web.Data
{
    public class SqliteDbHandler
    {
        private readonly string _dbFilePath = @"C:\wws\wws.db3";

        public void InitializeDatabase()
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(_dbFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Ensure the DB file exists (opening a connection will create it if missing)
            try
            {
                using (var conn = new SqliteConnection($"Data Source={_dbFilePath}"))
                {
                    conn.Open();
                }
            }
            catch (Exception ex)
            {
                // Optional: log the error
                Console.WriteLine($"Failed to create/open SQLite DB file: {ex.Message}");
                throw;
            }

            // Always ensure required tables exist (CREATE TABLE IF NOT EXISTS)
            EnsureSchema();

            // Ensure default rows are present (insert only when table is empty)
            EnsureDefaultWaste();
        }

        // Create tables with IF NOT EXISTS - safe to run every time
        private void EnsureSchema()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    // Occupiers
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS occupiers (
                            occTag CHAR(21),
                            occName CHAR(21)
                        );";
                    command.ExecuteNonQuery();

                    // Reports
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS reports (
                            repDate CHAR(10),
                            repTime CHAR(8),
                            repGross CHAR(10),
                            repWaste CHAR(20),
                            repOccupier CHAR(20)
                        );";
                    command.ExecuteNonQuery();

                    // Waste (with primary key)
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS waste (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            wasteType TEXT NOT NULL,
                            wastePicture TEXT
                        );";
                    command.ExecuteNonQuery();
                }
            }
        }

        // Insert default waste rows only if table is empty
        // Replace or add this EnsureDefaultWaste() method in your existing SqliteDbHandler class.
        // It is idempotent: it only inserts defaults when table is empty and only copies images if
        // the source file exists and the destination doesn't already have it. If a copy succeeds,
        // the DB row is updated to store the web path (/images/waste/filename.ext).

        private void EnsureDefaultWaste()
        {
            var defaults = new[]
            {
        (Type: "general waste", Pic: @"c:\wws\waste\1-general_waste.bmp"),
        (Type: "dry mix recyclables", Pic: @"c:\wws\waste\2-dry_mixed_recyclables.bmp"),
        (Type: "mixed glass", Pic: @"c:\wws\waste\3-mixed_glass.bmp"),
        (Type: "paper", Pic: @"c:\wws\waste\4-paper.bmp"),
        (Type: "cardboard", Pic: @"c:\wws\waste\5-cardboard.bmp")
    };

            using (var connection = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                connection.Open();

                // If the table is empty, insert defaults using the raw Pic values (same behavior as before)
                using (var countCmd = connection.CreateCommand())
                {
                    countCmd.CommandText = "SELECT COUNT(1) FROM waste;";
                    object? result = countCmd.ExecuteScalar();
                    long count = 0;
                    if (result != null && result != DBNull.Value)
                    {
                        count = Convert.ToInt64(result);
                    }

                    if (count == 0)
                    {
                        using (var transaction = connection.BeginTransaction())
                        {
                            using (var insertCmd = connection.CreateCommand())
                            {
                                insertCmd.CommandText = "INSERT INTO waste (wasteType, wastePicture) VALUES ($type, $pic);";
                                var pType = insertCmd.CreateParameter();
                                pType.ParameterName = "$type";
                                insertCmd.Parameters.Add(pType);

                                var pPic = insertCmd.CreateParameter();
                                pPic.ParameterName = "$pic";
                                insertCmd.Parameters.Add(pPic);

                                foreach (var d in defaults)
                                {
                                    pType.Value = d.Type;
                                    pPic.Value = d.Pic;
                                    insertCmd.ExecuteNonQuery();
                                }
                            }
                            transaction.Commit();
                        }
                    }
                }

                // Next: attempt to copy any referenced files from the source C:\wws\waste\ to wwwroot/images/waste
                // and update the DB to store the web path (/images/waste/filename.ext).
                // This step is idempotent and will only copy when source exists and destination is missing.
                var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesWasteDir = Path.Combine(webRoot, "images", "waste");
                if (!Directory.Exists(imagesWasteDir)) Directory.CreateDirectory(imagesWasteDir);

                // Select all rows and try to normalize wastePicture to web path if the corresponding file is available
                using (var selectCmd = connection.CreateCommand())
                {
                    selectCmd.CommandText = "SELECT id, wasteType, wastePicture FROM waste;";
                    using (var reader = selectCmd.ExecuteReader())
                    {
                        var updates = new List<(int id, string webPath)>();
                        while (reader.Read())
                        {
                            var id = reader.GetInt32(0);
                            var pic = reader.IsDBNull(2) ? "" : reader.GetString(2);

                            if (string.IsNullOrWhiteSpace(pic)) continue;

                            // If already a web path (starts with '/'), leave it
                            if (pic.StartsWith("/")) continue;

                            // Otherwise treat it as file path; attempt to copy the file into wwwroot/images/waste
                            var fileName = Path.GetFileName(pic);
                            if (string.IsNullOrWhiteSpace(fileName)) continue;

                            var destPathPhysical = Path.Combine(imagesWasteDir, fileName);
                            var destWebPath = "/images/waste/" + fileName;

                            // If dest already exists, just update DB to the web path
                            if (File.Exists(destPathPhysical))
                            {
                                updates.Add((id, destWebPath));
                                continue;
                            }

                            // If source file exists, copy it
                            if (File.Exists(pic))
                            {
                                try
                                {
                                    File.Copy(pic, destPathPhysical, overwrite: false);
                                    updates.Add((id, destWebPath));
                                }
                                catch
                                {
                                    // If copy fails, ignore for now (keep original path in DB).
                                }
                            }
                        }

                        // Apply any updates
                        if (updates.Count > 0)
                        {
                            using (var trans = connection.BeginTransaction())
                            {
                                using (var updateCmd = connection.CreateCommand())
                                {
                                    updateCmd.CommandText = "UPDATE waste SET wastePicture = $web WHERE id = $id;";
                                    var pWeb = updateCmd.CreateParameter();
                                    pWeb.ParameterName = "$web";
                                    updateCmd.Parameters.Add(pWeb);

                                    var pId = updateCmd.CreateParameter();
                                    pId.ParameterName = "$id";
                                    updateCmd.Parameters.Add(pId);

                                    foreach (var u in updates)
                                    {
                                        pWeb.Value = u.webPath;
                                        pId.Value = u.id;
                                        updateCmd.ExecuteNonQuery();
                                    }
                                }
                                trans.Commit();
                            }
                        }
                    }
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

        //

        public List<WasteItem> GetAllWaste()
        {
            var list = new List<WasteItem>();
            using (var conn = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, wasteType, wastePicture FROM waste ORDER BY wasteType;";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new WasteItem
                            {
                                Id = r.GetInt32(0),
                                WasteType = r.IsDBNull(1) ? "" : r.GetString(1),
                                WastePicture = r.IsDBNull(2) ? "" : r.GetString(2)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public WasteItem? GetWasteById(int id)
        {
            using (var conn = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, wasteType, wastePicture FROM waste WHERE id = $id LIMIT 1;";
                    cmd.Parameters.AddWithValue("$id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new WasteItem
                            {
                                Id = r.GetInt32(0),
                                WasteType = r.IsDBNull(1) ? "" : r.GetString(1),
                                WastePicture = r.IsDBNull(2) ? "" : r.GetString(2)
                            };
                        }
                    }
                }
            }
            return null;
        }

        // Replace only the AddWaste method in your SqliteDbHandler class with this safe version.
        public int AddWaste(WasteItem item)
        {
            using (var conn = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO waste (wasteType, wastePicture) VALUES ($type, $pic); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("$type", item.WasteType ?? "");
                    cmd.Parameters.AddWithValue("$pic", item.WastePicture ?? "");

                    object? result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                    {
                        // Nothing returned — return 0 or throw if you prefer
                        return 0;
                    }

                    // last_insert_rowid() is typically a 64-bit integer; convert safely
                    long lastId = Convert.ToInt64(result);
                    return (int)lastId;
                }
            }
        }

        public void UpdateWaste(WasteItem item)
        {
            using (var conn = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE waste SET wasteType = $type, wastePicture = $pic WHERE id = $id;";
                    cmd.Parameters.AddWithValue("$type", item.WasteType ?? "");
                    cmd.Parameters.AddWithValue("$pic", item.WastePicture ?? "");
                    cmd.Parameters.AddWithValue("$id", item.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteWaste(int id)
        {
            using (var conn = new SqliteConnection($"Data Source={_dbFilePath}"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM waste WHERE id = $id;";
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        //
    }
}
