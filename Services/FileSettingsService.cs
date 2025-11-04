using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace wws_web.Services
{
    public interface IFileSettingsService
    {
        Task<T?> ReadAsync<T>(string filePath) where T : class;
        Task WriteAsync<T>(string filePath, T data) where T : class;
    }

    public class FileSettingsService : IFileSettingsService
    {
        private readonly JsonSerializerOptions _jsonOptions;
        // one semaphore per file path to avoid concurrent write/read races
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        public FileSettingsService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        private SemaphoreSlim GetLock(string filePath) =>
            _fileLocks.GetOrAdd(Path.GetFullPath(filePath), _ => new SemaphoreSlim(1, 1));

        private void EnsureDirectoryExists(string filePath)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public async Task<T?> ReadAsync<T>(string filePath) where T : class
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var sem = GetLock(filePath);
            await sem.WaitAsync();
            try
            {
                if (!File.Exists(filePath))
                    return null; // caller can create default instance or handle null

                // Use FileStream for safe async reading
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await JsonSerializer.DeserializeAsync<T>(fs, _jsonOptions);
            }
            catch (JsonException)
            {
                // malformed JSON - bubble up or return null depending on your policy
                throw;
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task WriteAsync<T>(string filePath, T data) where T : class
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            var sem = GetLock(filePath);
            await sem.WaitAsync();
            try
            {
                EnsureDirectoryExists(filePath);

                // Write to temp file and move (atomic-ish) to reduce partial-write risk
                var tmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath))!, Path.GetRandomFileName());
                await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(fs, data, _jsonOptions);
                }

                // Replace target (overwrites)
                if (File.Exists(filePath))
                    File.Replace(tmp, filePath, null);
                else
                    File.Move(tmp, filePath);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}
