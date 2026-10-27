Set-StrictMode -Version Latest

function ConvertTo-ShellLiteral([string]$Value) {
    return "'" + $Value.Replace("'", "'" + '"' + "'" + '"' + "'") + "'"
}

function Get-LinuxHostProbeCommand {
    return @('sh', '-c', 'uname -m && uname -r && printf ''%s\n'' "$HOME" && dotnet --list-sdks')
}

function ConvertFrom-LinuxHostProbe([string]$Output) {
    $lines = @($Output.TrimEnd().Split("`n") | ForEach-Object { $_.TrimEnd("`r") })
    if ($lines.Count -lt 4 -or !(@($lines | Select-Object -Skip 3) -match '^10\.\d+\.\d+\s+\[')) {
        throw 'The Linux target requires an existing .NET 10 SDK on PATH. Install a supported .NET 10 SDK using your distribution/vendor instructions, then retry. SDK installation is never performed by this workflow.'
    }
    if (!$lines[2].StartsWith('/')) {
        throw 'The Linux target did not report an absolute home directory.'
    }
    return [pscustomobject]@{ machine = $lines[0]; release = $lines[1]; home = $lines[2] }
}

function Get-LinuxTestingBootstrap([string]$ProductRoot, [string]$LogDirectory) {
    $fingerprint = Get-SourceFingerprint $ProductRoot
    $cache = Join-Path $ProductRoot "artifacts/linux-test/bootstrap/$fingerprint"
    $helper = Join-Path $cache 'Memoria.FF8.LinuxTesting'
    $receiptPath = Join-Path $cache 'bootstrap.json'
    if ((Test-Path -LiteralPath $helper -PathType Leaf) -and (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        try {
            $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
            if ($receipt.fingerprint -ceq $fingerprint -and $receipt.sha256 -ceq (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash.ToLowerInvariant()) {
                return $helper
            }
        }
        catch {
            # An incomplete cache is rebuilt and verified before use.
        }
    }
    New-Item -ItemType Directory -Force -Path $cache | Out-Null
    $build = Invoke-TestProcess dotnet @('publish', (Join-Path $ProductRoot 'tools/Memoria.FF8.LinuxTesting/Memoria.FF8.LinuxTesting.csproj'), '--artifacts-path', (Join-Path $ProductRoot 'artifacts/linux-test/bootstrap-build'), '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'false', '-p:PublishSingleFile=true', '-o', $cache)
    ($build.Output + $build.Error) | Set-Content -LiteralPath (Join-Path $LogDirectory 'bootstrap-build.log') -Encoding utf8
    Assert-UnchangedBuildSource $ProductRoot $fingerprint
    if (!(Test-Path -LiteralPath $helper -PathType Leaf)) {
        throw 'Linux testing helper publish did not produce its apphost.'
    }
    @{ fingerprint = $fingerprint; sha256 = (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding utf8
    return $helper
}

function Invoke-LinuxTarget([string]$Mode, $Settings, [string[]]$Command, [switch]$AllowFailure, [int]$TimeoutSeconds = 1800) {
    if ($Mode -eq 'Wsl') {
        return Invoke-WslCommand $Settings.Distro $Command -AllowFailure:$AllowFailure -TimeoutSeconds $TimeoutSeconds
    }
    return Invoke-SshCommand $Settings.SshHost $Command -AllowFailure:$AllowFailure -TimeoutSeconds $TimeoutSeconds
}

function Initialize-LinuxTestingTarget([string]$Mode, $Settings, [string]$Helper, [string]$TargetRoot) {
    $temporary = (Invoke-LinuxTarget $Mode $Settings @('mktemp', '-d', '/tmp/memoria-linux-test.XXXXXXXXXX')).Output.Trim()
    if ($temporary -cnotmatch '^/tmp/memoria-linux-test\.[A-Za-z0-9]{10}$') {
        throw 'The Linux target returned an unexpected bootstrap temporary directory.'
    }
    $temporaryHelper = "$temporary/Memoria.FF8.LinuxTesting"
    $targetHelper = "$TargetRoot/Memoria.FF8.LinuxTesting"
    try {
        if ($Mode -eq 'Wsl') {
            $source = ConvertTo-WslPath $Settings.Distro $Helper
            [void](Invoke-LinuxTarget $Mode $Settings @('cp', '--', $source, $temporaryHelper))
        }
        else {
            [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '--', $Helper, "$($Settings.SshHost):$temporaryHelper"))
        }
        [void](Invoke-LinuxTarget $Mode $Settings @('chmod', '700', '--', $temporaryHelper))
        # Verify ownership and control paths before copying anything into staging.
        [void](Invoke-LinuxTarget $Mode $Settings @($temporaryHelper, 'init', $TargetRoot))
        [void](Invoke-LinuxTarget $Mode $Settings @('cp', '--', $temporaryHelper, $targetHelper))
        [void](Invoke-LinuxTarget $Mode $Settings @('chmod', '700', '--', $targetHelper))
        return $targetHelper
    }
    finally {
        [void](Invoke-LinuxTarget $Mode $Settings @('rm', '-f', '--', $temporaryHelper) -AllowFailure -TimeoutSeconds 15)
        [void](Invoke-LinuxTarget $Mode $Settings @('rmdir', '--', $temporary) -AllowFailure -TimeoutSeconds 15)
    }
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
