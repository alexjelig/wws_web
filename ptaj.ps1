param (
    [string]$AppName,
    [string]$AppMod,
    [string]$UserName,
    [string]$Email,
    [string]$Password
)

function Show-Help {
    Write-Host "ptaj.ps1 - Add a new user to the login table."
    Write-Host "Usage:"
    Write-Host "  .\ptaj.ps1 <AppName> <AppMod> <UserName> <Email> <Password>"
    Write-Host ""
    Write-Host "Example:"
    Write-Host "  .\ptaj.ps1 MyApp MainModule johndoe johndoe@email.com MySecretPassword"
    Write-Host ""
    Write-Host "All other fields will be set to their default values as defined in the database."
}

function Hash-Password {
    param([string]$Password)
    $salt = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $hash = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        $Password,
        $salt,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        32
    )
    # Output: salt:hash (both base64)
    "$([Convert]::ToBase64String($salt)):$([Convert]::ToBase64String($hash))"
}

function Hash-Password-Argon2id {
    param([string]$Password)

    $salt = [byte[]]::new(16)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($salt)
    $passwordBytes = [System.Text.Encoding]::UTF8.GetBytes($Password)

    # Используем базовый класс Argon2id
    $typeName = "Konscious.Security.Cryptography.Argon2id"
    $argon2Type = [Type]::GetType($typeName)
    if (-not $argon2Type) {
        # Попробуем получить тип через все загруженные сборки (для PowerShell 7+)
        $argon2Type = [AppDomain]::CurrentDomain.GetAssemblies() |
            ForEach-Object { $_.GetType($typeName, $false) } |
            Where-Object { $_ } | Select-Object -First 1
    }
    if (-not $argon2Type) {
        throw "Не удалось найти тип $typeName в загруженных сборках"
    }

    $argon2 = [Activator]::CreateInstance($argon2Type, $passwordBytes)

    $argon2.Salt = $salt
    $argon2.DegreeOfParallelism = 2
    $argon2.MemorySize = 65536
    $argon2.Iterations = 4

    $hashBytes = $argon2.GetBytes(32)
    "$([Convert]::ToBase64String($salt)):$([Convert]::ToBase64String($hashBytes))"
}

# Show help if arguments are missing or invalid
if (!$AppName -or !$AppMod -or !$UserName -or !$Email -or !$Password) {
    Show-Help
    exit 1
}

# DLL paths - update if needed
$LoggingDllPath = "E:\my.vs2022\packages\Microsoft.Extensions.Logging.Abstractions.8.0.0\lib\net462\Microsoft.Extensions.Logging.Abstractions.dll"
$MySqlConnectorDllPath = "E:\my.vs2022\packages\MySqlConnector.2.3.7\lib\net48\MySqlConnector.dll"
$Blake2DllPath = "E:\my.vs2022\packages\Konscious.Security.Cryptography.Blake2.1.1.1\lib\net46\Konscious.Security.Cryptography.Blake2.dll"
$Argon2DllPath = "E:\my.vs2022\packages\Konscious.Security.Cryptography.Argon2.1.3.1\lib\net46\Konscious.Security.Cryptography.Argon2.dll"
Add-Type -Path $Blake2DllPath
Add-Type -Path $Argon2DllPath

# DB connection details
$Server = "178.128.36.174"
$Port = 3306
$Database = "wws"
$User = "ajroot"
$PasswordDB = "2909AJ2909"

# Check both DLLs exist
if (-not (Test-Path $LoggingDllPath)) {
    Write-Error "Microsoft.Extensions.Logging.Abstractions.dll not found at $LoggingDllPath. Please check the path."
    exit 1
}
if (-not (Test-Path $MySqlConnectorDllPath)) {
    Write-Error "MySqlConnector.dll not found at $MySqlConnectorDllPath. Please check the path."
    exit 1
}

# Load required assemblies
Add-Type -Path $LoggingDllPath
Add-Type -Path $MySqlConnectorDllPath

#[Reflection.Assembly]::LoadFrom("E:\my.vs2022\packages\Konscious.Security.Cryptography.Argon2.1.3.1\lib\net46\Konscious.Security.Cryptography.Argon2.dll").GetTypes() | Where-Object { $_.FullName -like "*Argon2*" }

# Build connection string
$connectionString = "Server=$Server;Port=$Port;Database=$Database;User ID=$User;Password=$PasswordDB"

# Hash the password with PBKDF2 and random salt
# $PasswordHash = Hash-Password $Password

# Применяем Argon2id для пароля:
$PasswordHash = Hash-Password-Argon2id $Password

try {
    $connection = New-Object MySqlConnector.MySqlConnection($connectionString)
    $connection.Open()
    Write-Host "Connected to MariaDB/MySQL server!"

    # Prepare SQL insert statement
    # PasswordHash is in the format salt:hash (both Base64)
    $sql = @"
INSERT INTO login 
    (AppName, AppMod, UserName, Email, PasswordHash, LastLogin, AmountOfLogins, CreatedAt, IsActive, Role, EmailVerified, PasswordResetToken, PasswordResetExpires, LastFailedLogin, FailedLoginCount, ProfileData)
VALUES
    (@AppName, @AppMod, @UserName, @Email, @PasswordHash, NULL, 0, CURRENT_TIMESTAMP, TRUE, 'user', TRUE, '0', NULL, NULL, 0, NULL)
"@

    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.Parameters.Add("@AppName",    [MySqlConnector.MySqlDbType]::VarChar, 100).Value = $AppName
    $cmd.Parameters.Add("@AppMod",     [MySqlConnector.MySqlDbType]::VarChar, 100).Value = $AppMod
    $cmd.Parameters.Add("@UserName",   [MySqlConnector.MySqlDbType]::VarChar, 100).Value = $UserName
    $cmd.Parameters.Add("@Email",      [MySqlConnector.MySqlDbType]::VarChar, 255).Value = $Email
    $cmd.Parameters.Add("@PasswordHash",[MySqlConnector.MySqlDbType]::VarChar, 255).Value = $PasswordHash

    $rows = $cmd.ExecuteNonQuery()
    if ($rows -gt 0) {
        Write-Host "User '$UserName' added successfully!"
    } else {
        Write-Error "Failed to add user."
    }
    $connection.Close()
}
catch {
    Write-Error "Error: $($_.Exception.Message)"
}
