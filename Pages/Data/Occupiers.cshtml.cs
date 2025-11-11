using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Diagnostics; // For Debug.WriteLine

namespace wws_web.Pages.Data
{
    public class OccupiersModel : PageModel
    {
        private readonly string _dbPath = @"C:\wws\wws.db3";

        public List<Occupier> Occupiers { get; set; } = new List<Occupier>();

        [BindProperty]
        public string? OccTag { get; set; }

        [BindProperty]
        public string? OccName { get; set; }

        [BindProperty]
        public bool IsEditMode { get; set; }

        [BindProperty]
        public long EditId { get; set; }  // For tracking the row being edited

        public void OnGet()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                Debug.WriteLine($"Connecting to database: {_dbPath}");
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT rowid, occTag, occName FROM occupiers";
                    Debug.WriteLine($"Query: {command.CommandText}");
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetInt64(0);
                            var tag = reader.GetString(1);
                            var name = reader.GetString(2);

                            Debug.WriteLine($"Fetched Occupier: ID={id}, Tag={tag}, Name={name}");

                            Occupiers.Add(new Occupier { Id = id, OccTag = tag, OccName = name });
                        }
                    }
                }
            }
        }

        public void OnGetEdit(long id)
        {
            Debug.WriteLine($"Editing Occupier: ID={id}");
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT occTag, occName FROM occupiers WHERE rowid = @id";
                    command.Parameters.AddWithValue("@id", id);
                    Debug.WriteLine($"Query: {command.CommandText}");
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            OccTag = reader.GetString(0);
                            OccName = reader.GetString(1);
                            EditId = id;
                            IsEditMode = true;

                            Debug.WriteLine($"Loaded for Edit: Tag={OccTag}, Name={OccName}");
                        }
                    }
                }
            }
        }

        public IActionResult OnPostAdd()
        {
            Debug.WriteLine($"Adding New Occupier: Tag={OccTag}, Name={OccName}");
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO occupiers (occTag, occName) VALUES (@occTag, @occName)";
                    command.Parameters.AddWithValue("@occTag", OccTag);
                    command.Parameters.AddWithValue("@occName", OccName);
                    Debug.WriteLine($"Query: {command.CommandText}");
                    command.ExecuteNonQuery();
                }
            }

            return RedirectToPage();
        }

        public IActionResult OnPostSave()
        {
            Debug.WriteLine($"Saving Occupier: ID={EditId}, Tag={OccTag}, Name={OccName}");
            if (EditId <= 0)
            {
                Debug.WriteLine("No valid record selected for update.");
                ModelState.AddModelError(string.Empty, "No valid record selected for editing.");
                return Page();
            }

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE occupiers SET occTag = @occTag, occName = @occName WHERE rowid = @id";
                    command.Parameters.AddWithValue("@occTag", OccTag);
                    command.Parameters.AddWithValue("@occName", OccName);
                    command.Parameters.AddWithValue("@id", EditId); // Correctly use EditId for update

                    var affectedRows = command.ExecuteNonQuery();
                    Debug.WriteLine($"Rows Updated: {affectedRows}");
                    if (affectedRows == 0)
                    {
                        Debug.WriteLine("Failed to update record.");
                        ModelState.AddModelError(string.Empty, "Failed to update the record.");
                        return Page();
                    }
                }
            }

            IsEditMode = false; // Reset edit mode
            return RedirectToPage(); // Refresh the page after saving changes
        }

        public IActionResult OnPostDelete(long id)
        {
            Debug.WriteLine($"Deleting Occupier: ID={id}");
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM occupiers WHERE rowid = @id";
                    command.Parameters.AddWithValue("@id", id);
                    Debug.WriteLine($"Query: {command.CommandText}");
                    var affectedRows = command.ExecuteNonQuery();
                    Debug.WriteLine($"Rows Deleted: {affectedRows}");
                }
            }

            return RedirectToPage();
        }
    }

    public class Occupier
    {
        public long Id { get; set; }
        public string OccTag { get; set; }
        public string OccName { get; set; }
    }
}
