using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Diagnostics;

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
        public long EditId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1; // Added this property to resolve the missing symbol error

        public bool HasNextPage { get; set; } // Added this property to support pagination logic

        public void OnGet()
        {
            const int PageSize = 10;

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT rowid, occTag, occName FROM occupiers LIMIT @limit OFFSET @offset";
                    command.Parameters.AddWithValue("@limit", PageSize);
                    command.Parameters.AddWithValue("@offset", (PageNumber - 1) * PageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetInt64(0);
                            var tag = reader.GetString(1);
                            var name = reader.GetString(2);

                            Occupiers.Add(new Occupier { Id = id, OccTag = tag, OccName = name });
                        }

                        HasNextPage = Occupiers.Count == PageSize; // Determine if there's a next page
                    }
                }
            }
        }

        public void OnGetEdit(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT occTag, occName FROM occupiers WHERE rowid = @id";
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            OccTag = reader.GetString(0);
                            OccName = reader.GetString(1);
                            EditId = id;
                            IsEditMode = true;
                        }
                    }
                }
            }
        }

        public IActionResult OnPostAdd()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO occupiers (occTag, occName) VALUES (@occTag, @occName)";
                    command.Parameters.AddWithValue("@occTag", OccTag);
                    command.Parameters.AddWithValue("@occName", OccName);
                    command.ExecuteNonQuery();
                }
            }

            return RedirectToPage();
        }

        public IActionResult OnPostSave()
        {
            if (EditId <= 0)
            {
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
                    command.Parameters.AddWithValue("@id", EditId);

                    if (command.ExecuteNonQuery() == 0)
                    {
                        ModelState.AddModelError(string.Empty, "Failed to update the record.");
                        return Page();
                    }
                }
            }

            IsEditMode = false; // Reset mode to Add
            EditId = 0; // Reset tracking ID
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM occupiers WHERE rowid = @id";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
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
