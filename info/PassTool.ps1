param (
    [Parameter(Position=0)]
    [ValidateSet("hash","verify")]
    [string]$Mode,

    [Parameter(Position=1)]
    [string]$Password,

    [Parameter(Position=2)]
    [string]$StoredHash
)

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

function Verify-Password {
    param(
        [string]$Password,
        [string]$Stored
    )
    $parts = $Stored.Split(":")
    if ($parts.Length -ne 2) {
        Write-Error "Stored hash format is invalid (expected 'salt:hash')."
        return $false
    }
    $salt = [Convert]::FromBase64String($parts[0])
    $hash = [Convert]::FromBase64String($parts[1])
    $testHash = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        $Password,
        $salt,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        32
    )
    # Fixed time comparison for byte arrays
    $match = $true
    if ($hash.Length -ne $testHash.Length) {
        $match = $false
    } else {
        for ($i = 0; $i -lt $hash.Length; $i++) {
            if ($hash[$i] -ne $testHash[$i]) {
                $match = $false
                break
            }
        }
    }
    if ($match) {
        Write-Output "Password is VALID."
    } else {
        Write-Output "Password is INVALID."
    }
    return $match
}

if ($Mode -eq "hash") {
    if (-not $Password) {
        Write-Host "Usage: .\PasswordTool.ps1 hash <password>"
        exit 1
    }
    $result = Hash-Password $Password
    Write-Host "Hash (store this in DB):"
    Write-Host $result
} elseif ($Mode -eq "verify") {
    if (-not $Password -or -not $StoredHash) {
        Write-Host "Usage: .\PasswordTool.ps1 verify <password> <salt:hash>"
        exit 1
    }
    Verify-Password $Password $StoredHash
} else {
    Write-Host "Usage:"
    Write-Host "  .\PasswordTool.ps1 hash <password>"
    Write-Host "  .\PasswordTool.ps1 verify <password> <salt:hash>"
    exit 1
}
