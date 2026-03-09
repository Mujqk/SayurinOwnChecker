param (
    [string]$FilePath
)

$CertName = "Sayurin Development"
$CertEmail = "support@sayurin.com"
$CertFile = "Sayurin.cer"

# Redirect all output to a log file
$LogFile = "sign_log.txt"
"--- Code Signing Debug ($FilePath) ---" | Out-File $LogFile -Append
"Target file: $FilePath" | Out-File $LogFile -Append

Write-Host "--- Code Signing Debug ---" -ForegroundColor Gray
Write-Host "Target file: $FilePath"

if (-not (Test-Path $FilePath)) {
    $err = "CRITICAL: Target file not found at $FilePath"
    Write-Error $err
    $err | Out-File $LogFile -Append
    Read-Host "Press Enter to exit..."
    exit 1
}

# Find certificate (searching for CN and E)
$Cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*CN=$CertName*" -and $_.Subject -like "*E=$CertEmail*" } | Select-Object -First 1

if (-not $Cert) {
    Write-Host "Certificate with email '$CertEmail' not found. Creating a new one..." -ForegroundColor Cyan
    try {
        $DN = "CN=$CertName, E=$CertEmail, O=Sayurin, L=Moscow, C=RU"
        $Cert = New-SelfSignedCertificate -Type Custom -Subject $DN -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") -KeyUsage DigitalSignature -FriendlyName "Sayurin Checker Code Signing" -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
        Write-Host "Successfully created certificate with Publisher and Email." -ForegroundColor Green
        
        Write-Host "Exporting to $CertFile..." -ForegroundColor Yellow
        Export-Certificate -Cert $Cert -FilePath $CertFile | Out-Null
        "Certificate created and exported." | Out-File $LogFile -Append
    } catch {
        $err = "Failed to create or export certificate: $_"
        Write-Error $err
        $err | Out-File $LogFile -Append
        Read-Host "Press Enter to exit..."
        exit 1
    }
} else {
    $msg = "Found existing certificate: $($Cert.Thumbprint)"
    Write-Host $msg -ForegroundColor Green
    $msg | Out-File $LogFile -Append
}

# Sign
Write-Host "Using Certificate: $($Cert.Subject) (Thumbprint: $($Cert.Thumbprint))" -ForegroundColor Cyan
Write-Host "Attempting to sign (SHA256)..." -ForegroundColor Blue
$Status = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $Cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"

if ($Status.Status -ne "Valid" -and $Status.Status -ne "UnknownError" -and $Status.Status -ne "Incompatible") { 
    $msg = "Signing failed with status: $($Status.Status). Retrying without timestamp..."
    Write-Host $msg -ForegroundColor Yellow
    $msg | Out-File $LogFile -Append
    $Status = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $Cert -HashAlgorithm SHA256
}

$msg = "Final Status: $($Status.Status) ($($Status.StatusMessage))"
Write-Host $msg
$msg | Out-File $LogFile -Append

if ($Status.Status -eq "Valid" -or $Status.Status -eq "UnknownError" -or $Status.Status -eq "Incompatible") {
    Write-Host "File successfully signed!" -ForegroundColor Green
    "SUCCESS" | Out-File $LogFile -Append
} else {
    Write-Error "Signing FAILED. Error: $($Status.StatusMessage)"
    "FAILED: $($Status.StatusMessage)" | Out-File $LogFile -Append
}
Write-Host "--------------------------"
Write-Host "Log saved to $LogFile"
Read-Host "Press Enter to continue..."
