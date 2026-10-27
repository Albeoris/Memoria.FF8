Set-StrictMode -Version Latest

function ConvertTo-ShellLiteral([string]$Value) {
    return "'" + $Value.Replace("'", "'" + '"' + "'" + '"' + "'") + "'"
}

function Get-StagingInitializationCode {
    return @'
import pathlib, sys
p = pathlib.Path(sys.argv[1])
m = p / ".memoria-linux-test-owner"
if p.is_symlink() or m.is_symlink():
    raise ValueError("Staging root/ownership marker cannot be a symlink")
if p.exists() and any(p.iterdir()) and not m.is_file():
    raise ValueError("Existing nonempty staging directory has no ownership marker; select a fresh directory")
if m.exists() and m.read_text() != "memoria-linux-test-v1":
    raise ValueError("Staging ownership marker is unrecognized")
for name in ("target.py", "request.json", "sync.json", "payload-manifest.json", "payload", "state", "runs", "test-results", "deployment.json", "deployment-staging", "deployment-backup", "inbox"):
    candidate = p / name
    if candidate.is_symlink() or (candidate.is_dir() and any(item.is_symlink() for item in candidate.rglob("*"))):
        raise ValueError("Staging control/state paths cannot contain symlinks")
p.mkdir(parents=True, exist_ok=True)
m.write_text("memoria-linux-test-v1")
'@
}

function Invoke-TestProcess {
    param([string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds = 1800, [switch]$AllowFailure)
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $processStarted = $false
    try {
        if (!$process.Start()) {
            throw "Could not start $Executable."
        }
        $processStarted = $true
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (!$process.WaitForExit(200)) {
            if ([DateTime]::UtcNow -gt $deadline) {
                throw "$Executable timed out after $TimeoutSeconds seconds."
            }
        }
        $result = [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult(); Error = $stderr.GetAwaiter().GetResult() }
        if ($result.ExitCode -ne 0 -and !$AllowFailure) {
            throw "$Executable exited $($result.ExitCode).`n$($result.Output)`n$($result.Error)"
        }
        return $result
    }
    finally {
        if ($processStarted -and !$process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $process.Dispose()
    }
}

function Get-TestSettings([string]$Path) {
    $settings = @{ Distro = ''; GameDirectory = $null; DlsPath = $null; SgtPath = $null; SshHost = ''; TargetDirectory = '.local/share/memoria-linux-test'; HardwareGameDirectory = $null; HardwareDlsPath = $null; HardwareSgtPath = $null; GraphicalEnvironment = @{}; StreamCommand = @(); ScreenshotCommand = @(); Targets = @{} }
    if (Test-Path -LiteralPath $Path) {
        $configured = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
        foreach ($key in $configured.Keys) {
            if (!$settings.ContainsKey($key)) {
                throw "Unknown Linux test setting '$key'."
            }
            $settings[$key] = $configured[$key]
        }
    }
    foreach ($key in $settings.GraphicalEnvironment.Keys) {
        if ($key -notin @('DISPLAY', 'WAYLAND_DISPLAY', 'XDG_RUNTIME_DIR', 'DBUS_SESSION_BUS_ADDRESS', 'XAUTHORITY', 'PULSE_SERVER')) {
            throw "Unsupported graphical environment key '$key'."
        }
    }
    return $settings
}

function Select-WslDistribution([string]$Inventory, [string]$Configured) {
    if ($Configured) {
        return $Configured
    }
    $candidates = @($Inventory.Replace([string][char]0, '').Split("`n") | ForEach-Object {
        if ($_ -match '^\s*(?:\*\s*)?(?<name>.+?)\s+\S+\s+2\s*$') {
            $Matches.name.Trim()
        }
    })
    foreach ($preferred in @('Ubuntu-24.04', 'Ubuntu', 'Ubuntu-22.04')) {
        if ($candidates -contains $preferred) {
            return $preferred
        }
    }
    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }
    throw 'No WSL2 distribution is available. Install Ubuntu-24.04, or explicitly convert a suitable existing distro with wsl --set-version <name> 2. No system changes were attempted.'
}

function Select-HardwareTarget($Settings, [string]$Name) {
    if (!$Settings.Targets.Contains($Name)) {
        throw "Configure a named hardware entry Targets.$Name in local.settings.json."
    }
    $selected = $Settings.Clone()
    foreach ($key in $Settings.Targets[$Name].Keys) {
        if ($key -notin @('SshHost', 'TargetDirectory', 'HardwareGameDirectory', 'HardwareDlsPath', 'HardwareSgtPath', 'GraphicalEnvironment', 'StreamCommand', 'ScreenshotCommand')) {
            throw "Unsupported hardware target setting '$key'."
        }
        $selected[$key] = $Settings.Targets[$Name][$key]
    }
    foreach ($key in $selected.GraphicalEnvironment.Keys) {
        if ($key -notin @('DISPLAY', 'WAYLAND_DISPLAY', 'XDG_RUNTIME_DIR', 'DBUS_SESSION_BUS_ADDRESS', 'XAUTHORITY', 'PULSE_SERVER')) {
            throw "Unsupported graphical environment key '$key'."
        }
    }
    return $selected
}

function Get-LinuxBuildProfile([string]$ProductRoot) {
    $profile = Get-Content -LiteralPath (Join-Path $ProductRoot 'tools/linux-testing/build-capabilities.json') -Raw | ConvertFrom-Json
    if ($profile.schemaVersion -ne 1 -or $profile.runtimeIdentifier -ne 'linux-x64' -or $profile.scope -notin @('Launcher', 'Distribution')) {
        throw 'Unsupported committed Linux build capability profile.'
    }
    if ($profile.gameHost -and ($profile.scope -ne 'Distribution' -or !$profile.gameOutput -or !$profile.gameExecutable)) {
        throw 'A Linux game capability requires a complete distribution recipe and executable.'
    }
    foreach ($relative in @($profile.launcherOutput, $profile.gameOutput, $profile.gameExecutable)) {
        if ($relative -and ([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative.Replace('\', '/').Split('/') -contains '..')) {
            throw 'Build capability paths must stay inside the public product/payload tree.'
        }
    }
    return $profile
}

function Get-SourceFingerprint([string]$ProductRoot) {
    $listed = Invoke-TestProcess git @('-C', $ProductRoot, 'ls-files', '-c', '-o', '--exclude-standard', '-z')
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($relative in @($listed.Output.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries) | Sort-Object -Unique)) {
            if ($relative -match '(^|/)(bin|obj|artifacts|TestResults)/' -or $relative -eq 'tools/linux-testing/local.settings.json') {
                continue
            }
            $path = Join-Path $ProductRoot $relative
            $hash.AppendData([Text.Encoding]::UTF8.GetBytes($relative.Replace('\', '/') + "`0"))
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $hash.AppendData([IO.File]::ReadAllBytes($path))
            }
            else {
                $hash.AppendData([Text.Encoding]::UTF8.GetBytes('<deleted>'))
            }
        }
        $sdk = Invoke-TestProcess dotnet @('--version')
        $hash.AppendData([Text.Encoding]::UTF8.GetBytes($sdk.Output.Trim() + '|Release|linux-x64|self-contained'))
        return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    }
    finally {
        $hash.Dispose()
    }
}

function Get-PayloadManifest([string]$Root) {
    $manifest = [ordered]@{}
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName)) {
        if ($file.LinkType) {
            throw 'Payload symlinks are unsupported by this testing workflow.'
        }
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $manifest[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    return $manifest
}

function Assert-UnchangedBuildSource([string]$ProductRoot, [string]$Fingerprint) {
    if ((Get-SourceFingerprint $ProductRoot) -cne $Fingerprint) {
        throw 'The source or toolchain changed while publishing. The candidate was not marked verified; rerun the command to build the current source.'
    }
}

function Test-PayloadCache([string]$Cache, [string]$Fingerprint) {
    $receipt = Join-Path $Cache 'build.json'
    $manifestPath = Join-Path $Cache 'payload-manifest.json'
    $payload = Join-Path $Cache 'payload'
    if (!(Test-Path -LiteralPath $receipt) -or !(Test-Path -LiteralPath $manifestPath) -or !(Test-Path -LiteralPath $payload)) {
        return $false
    }
    try {
        $build = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
        if ($build.fingerprint -ne $Fingerprint -or $build.runtimeIdentifier -ne 'linux-x64') {
            return $false
        }
        $expected = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
        $actual = Get-PayloadManifest $payload
        if ($expected.Count -ne $actual.Count) {
            return $false
        }
        foreach ($key in $expected.Keys) {
            if (!$actual.Contains($key) -or $actual[$key] -cne $expected[$key]) {
                return $false
            }
        }
        return $true
    }
    catch {
        return $false
    }
}

function Assert-SshHost([string]$HostName) {
    if (!$HostName -or $HostName.StartsWith('-') -or $HostName -notmatch '^[a-zA-Z0-9_.@:\[\]-]+$') {
        throw 'Configure SshHost as an OpenSSH host alias or user@host, without shell syntax or options.'
    }
}

function Get-ChangedPayloadFiles($LocalManifest, $RemoteManifest) {
    return @($LocalManifest.Keys | Where-Object { !$RemoteManifest.Contains($_) -or $LocalManifest[$_] -cne $RemoteManifest[$_] } | Sort-Object)
}

function Invoke-WslCommand([string]$Distro, [string[]]$Command, [switch]$AllowFailure, [int]$TimeoutSeconds = 1800) {
    return Invoke-TestProcess 'wsl.exe' (@('--distribution', $Distro, '--exec') + $Command) -AllowFailure:$AllowFailure -TimeoutSeconds $TimeoutSeconds
}

function ConvertTo-WslPath([string]$Distro, [string]$Path) {
    if (!$Path) {
        return $null
    }
    $result = Invoke-WslCommand $Distro @('wslpath', '-a', '-u', [IO.Path]::GetFullPath($Path))
    return $result.Output.Trim()
}

function Invoke-SshCommand([string]$SshHost, [string[]]$Command, [switch]$AllowFailure, [int]$TimeoutSeconds = 1800) {
    Assert-SshHost $SshHost
    $remote = ($Command | ForEach-Object { ConvertTo-ShellLiteral $_ }) -join ' '
    return Invoke-TestProcess ssh @('-o', 'BatchMode=yes', '-o', 'ConnectTimeout=15', '--', $SshHost, $remote) -AllowFailure:$AllowFailure -TimeoutSeconds $TimeoutSeconds
}

function New-ChangedPayloadArchive([string]$Payload, [string[]]$ChangedFiles, [string]$ArchivePath) {
    $archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relative in $ChangedFiles) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $Payload $relative), $relative, [IO.Compression.CompressionLevel]::Fastest)
        }
    }
    finally {
        $archive.Dispose()
    }
}
