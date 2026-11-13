# Downloads the .NET SDK into -InstallDir and shows a progress bar.
# Used by start-windows.bat on the first start. If the download address cannot be found,
# it falls back to Microsoft's own installer script (without progress bar).
param(
    [Parameter(Mandatory = $true)][string]$Arch,
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$Work
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force -Path $Work | Out-Null

function Show-Bar([int]$Percent) {
    $width = 30
    $filled = [int]($width * $Percent / 100)
    $bar = ('#' * $filled) + ('-' * ($width - $filled))
    Write-Host -NoNewline ("`r          [{0}] {1,3}%" -f $bar, $Percent)
}

function Get-FileWithProgress([string]$Url, [string]$Dest) {
    $req = [Net.HttpWebRequest]::Create($Url)
    $resp = $req.GetResponse()
    $total = $resp.ContentLength
    $in = $resp.GetResponseStream()
    $out = [IO.File]::Create($Dest)
    $buf = New-Object byte[] 1048576
    $read = 0; $last = -1
    try {
        while (($n = $in.Read($buf, 0, $buf.Length)) -gt 0) {
            $out.Write($buf, 0, $n)
            $read += $n
            if ($total -gt 0) {
                $p = [int](100 * $read / $total)
                if ($p -ne $last) { $last = $p; Show-Bar $p }
            }
        }
    } finally { $out.Close(); $in.Close(); $resp.Close() }
    Write-Host ""
}

$installer = Join-Path $Work 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing

$url = $null
try {
    $dry = & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 10.0 -Architecture $Arch -InstallDir $InstallDir -NoPath -DryRun | Out-String
    if ($dry -match 'Primary named payload URL:\s*(\S+)') { $url = $Matches[1] }
} catch { }

if ($url) {
    $zip = Join-Path $Work 'dotnet-sdk.zip'
    Get-FileWithProgress $url $zip
    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
    Write-Host "          Installing components"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        # templates, F# and the extra command-line tools are not needed to build Notium
        $entries = @($archive.Entries | Where-Object { $_.FullName -notlike 'templates/*' -and $_.FullName -notmatch '^sdk/[^/]+/(FSharp|DotnetTools)/' })
        $count = $entries.Count; $done = 0; $last = -1
        foreach ($e in $entries) {
            $target = Join-Path $InstallDir $e.FullName
            if ($e.FullName.EndsWith('/')) { New-Item -ItemType Directory -Force -Path $target | Out-Null }
            else {
                $dir = Split-Path $target -Parent
                if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
                [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $target, $true)
            }
            $done++
            $p = [int](100 * $done / $count)
            if ($p -ne $last) { $last = $p; Show-Bar $p }
        }
    } finally { $archive.Dispose() }
    Write-Host ""
    Remove-Item -Force $zip
} else {
    Write-Host "          Installing..."
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 10.0 -Architecture $Arch -InstallDir $InstallDir -NoPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The .NET installer failed." }
}
Remove-Item -Force $installer -ErrorAction SilentlyContinue
if (-not (Test-Path (Join-Path $InstallDir 'dotnet.exe'))) { throw "dotnet.exe was not found after the download." }
