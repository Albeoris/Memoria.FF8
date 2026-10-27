[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../Common.ps1')
$assertions = 0
function Assert-True([bool]$Value, [string]$Message) {
    $script:assertions++
    if (!$Value) {
        throw $Message
    }
}
function Assert-Throws([scriptblock]$Action, [string]$Message) {
    $script:assertions++
    try {
        & $Action
    }
    catch {
        return
    }
    throw $Message
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('Memoria.LinuxTests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
Assert-True ((ConvertTo-ShellLiteral "a'b `$x; z") -ceq ("'a'" + '"' + "'" + '"' + "'b `$x; z'")) 'Remote shell arguments must remain literal.'
Assert-Throws { Assert-SshHost '-oProxyCommand=bad' } 'SSH options must not be host names.'
Assert-Throws { Assert-SshHost 'host;touch bad' } 'SSH shell syntax must not be accepted.'
Assert-SshHost 'deck@example.invalid'
Assert-True ((Select-WslDistribution "  NAME STATE VERSION`n* Debian Stopped 2`n  Ubuntu-24.04 Running 2" '') -eq 'Ubuntu-24.04') 'Automatic WSL selection should prefer Ubuntu 24.04.'
Assert-True ((Select-WslDistribution "  Ubuntu-22.04 Остановлено 2`n  Ubuntu-24.04 Stopped 1" '') -eq 'Ubuntu-22.04') 'A suitable existing WSL2 distro should be reused instead of a WSL1 preference.'
Assert-True ((Select-WslDistribution "* Debian Stopped 2" '') -eq 'Debian') 'Generic existing WSL2 distributions should be reusable.'
Assert-True ((Select-WslDistribution '' 'Explicit distro') -eq 'Explicit distro') 'Explicit distro selection is validated by its actual host probe.'
Assert-Throws { Select-WslDistribution 'Ubuntu Stopped 1' '' } 'A WSL1-only installation must not pass WSL2 detection.'
$changed = Get-ChangedPayloadFiles @{ 'same' = 'a'; 'changed' = 'new'; 'new file' = 'c' } @{ 'same' = 'a'; 'changed' = 'old' }
Assert-True (($changed -join ',') -eq 'changed,new file') 'Only changed payload files should transfer.'
$cache = Join-Path $temp 'cache'
$payload = Join-Path $cache 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Set-Content -LiteralPath (Join-Path $payload 'Memoria.Launcher') -Value 'synthetic executable'
Get-PayloadManifest $payload | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $cache 'payload-manifest.json')
@{ fingerprint = 'source-a'; runtimeIdentifier = 'linux-x64' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $cache 'build.json')
Assert-True (Test-PayloadCache $cache 'source-a') 'Matching complete cache should be reused.'
Assert-True (!(Test-PayloadCache $cache 'source-b')) 'Another source must invalidate cache.'
Add-Content -LiteralPath (Join-Path $payload 'Memoria.Launcher') -Value 'tamper'
Assert-True (!(Test-PayloadCache $cache 'source-a')) 'Changed payload must invalidate cache.'
$settingsFile = Join-Path $temp 'settings.json'
@{ GraphicalEnvironment = @{ LD_PRELOAD = 'unexpected.so' } } | ConvertTo-Json | Set-Content -LiteralPath $settingsFile
Assert-Throws { Get-TestSettings $settingsFile } 'Non-graphical environment keys must fail.'
$defaults = Get-TestSettings (Join-Path $temp 'absent.json')
Assert-True (!$defaults.Distro) 'Default should detect a suitable existing WSL2 distro, preferring Ubuntu 24.04.'
Assert-True (!$defaults.GameDirectory) 'Game directory must not be guessed.'
$script:commands = [Collections.Generic.List[object]]::new()
function Invoke-TestProcess {
    param([string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds = 1800, [switch]$AllowFailure)
    $script:commands.Add(@{ executable = $Executable; arguments = $Arguments })
    if ($Executable -eq 'git') {
        return [pscustomobject]@{ ExitCode = 0; Output = "source.cs`0Directory.Packages.props`0tools/test.ps1`0"; Error = '' }
    }
    if ($Executable -eq 'dotnet') {
        return [pscustomobject]@{ ExitCode = 0; Output = '10.0.test'; Error = '' }
    }
    return [pscustomobject]@{ ExitCode = 0; Output = '/mnt/w/Game files'; Error = '' }
}
$converted = ConvertTo-WslPath 'Ubuntu-24.04' 'W:\Game files'
Assert-True ($converted -eq '/mnt/w/Game files') 'Mapped drives must use wslpath output.'
$last = $script:commands[$script:commands.Count - 1]
Assert-True ($last.arguments[3] -eq 'wslpath') 'Path conversion must invoke wslpath.'
Assert-True ($last.arguments[-1].EndsWith('W:\Game files')) 'Spaces/drive letters must stay one argument.'
[void](Invoke-SshCommand 'deck' @('python3', '/path with spaces/target.py', 'probe'))
$last = $script:commands[$script:commands.Count - 1]
Assert-True ($last.arguments[-1].Contains("'/path with spaces/target.py'")) 'SSH arguments must be individually quoted.'
New-Item -ItemType Directory -Path (Join-Path $temp 'tools') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $temp 'source.cs') -Value 'first'
Set-Content -LiteralPath (Join-Path $temp 'Directory.Packages.props') -Value 'pins'
Set-Content -LiteralPath (Join-Path $temp 'tools/test.ps1') -Value 'script'
$first = Get-SourceFingerprint $temp
Set-Content -LiteralPath (Join-Path $temp 'source.cs') -Value 'dirty working tree'
$dirty = Get-SourceFingerprint $temp
Assert-True ($first -cne $dirty) 'Dirty source contents must invalidate cache.'
Set-Content -LiteralPath (Join-Path $temp 'Directory.Packages.props') -Value 'new pin'
Assert-True ($dirty -cne (Get-SourceFingerprint $temp)) 'Dependency pins must change cache identity.'
$beforeScript = Get-SourceFingerprint $temp
Set-Content -LiteralPath (Join-Path $temp 'tools/test.ps1') -Value 'changed orchestration'
Assert-True ($beforeScript -cne (Get-SourceFingerprint $temp)) 'Scripts must change cache identity.'
Assert-UnchangedBuildSource $temp (Get-SourceFingerprint $temp)
Assert-Throws { Assert-UnchangedBuildSource $temp $beforeScript } 'A source change during publishing must prevent sealing a verified build receipt.'
$named = Get-TestSettings (Join-Path $temp 'absent.json')
$named.Targets = @{ SteamDeck = @{ SshHost = 'deck'; TargetDirectory = '.local/share/deck-tests' }; Desktop = @{ SshHost = 'linux-desktop' } }
Assert-True ((Select-HardwareTarget $named 'SteamDeck').SshHost -eq 'deck') 'SteamDeck must select its named settings.'
Assert-True ((Select-HardwareTarget $named 'Desktop').SshHost -eq 'linux-desktop') 'Generic targets must select distinct settings.'
Assert-Throws { Select-HardwareTarget $named 'Missing' } 'Missing named targets must fail explicitly.'
foreach ($count in @(0, 1, 2)) {
    $local = @{}
    for ($index = 0; $index -lt $count; $index++) {
        $local["file$index"] = 'changed'
        Set-Content -LiteralPath (Join-Path $payload "file$index") -Value "content$index"
    }
    $planned = @(Get-ChangedPayloadFiles $local @{})
    Assert-True ($planned.Count -eq $count) "StrictMode deployment planning must preserve cardinality $count."
    $archivePath = Join-Path $temp "changed-$count.zip"
    New-ChangedPayloadArchive $payload $planned $archivePath
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        Assert-True ($archive.Entries.Count -eq $count) "Changed-file archive must contain exactly $count files."
    }
    finally {
        $archive.Dispose()
    }
}
Write-Host "$assertions orchestration assertions passed; no WSL/SSH connection or installation performed."
# Keep the tiny synthetic fixture directory for inspection; no game/user data is used.
