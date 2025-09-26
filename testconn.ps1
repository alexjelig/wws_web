param (
    [string]$Server = "178.128.36.174",
    [int]$Port = 3306,
    [string]$Database = "wws",
    [string]$User = "ajroot",
    [string]$Password = "2909AJ2909"
)

$LoggingDllPath = "E:\my.vs2022\packages\Microsoft.Extensions.Logging.Abstractions.8.0.0\lib\net462\Microsoft.Extensions.Logging.Abstractions.dll"
$MySqlConnectorDllPath = "E:\my.vs2022\packages\MySqlConnector.2.3.7\lib\net48\MySqlConnector.dll"

if (-not (Test-Path $LoggingDllPath)) {
    Write-Error "Microsoft.Extensions.Logging.Abstractions.dll not found at $LoggingDllPath. Please check the path."
    exit 1
}
if (-not (Test-Path $MySqlConnectorDllPath)) {
    Write-Error "MySqlConnector.dll not found at $MySqlConnectorDllPath. Please check the path."
    exit 1
}

Add-Type -Path $LoggingDllPath
Add-Type -Path $MySqlConnectorDllPath

$connectionString = "Server=$Server;Port=$Port;Database=$Database;User ID=$User;Password=$Password"

try {
    $connection = New-Object MySqlConnector.MySqlConnection($connectionString)
    $connection.Open()
    Write-Host "Connection to MariaDB/MySQL server succeeded!"
    $connection.Close()
}
catch {
    Write-Error "Connection failed: $($_.Exception.Message)"
}
