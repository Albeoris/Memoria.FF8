[CmdletBinding()]
param(
    [ValidateSet('Wsl', 'Hardware')][string]$Mode = 'Wsl',
    [string]$Target = 'SteamDeck',
    [ValidateSet('All', 'Build', 'Deploy', 'Launch', 'Test', 'Diagnostics')][string]$Phase = 'All',
    [ValidateSet('Auto', 'Launcher', 'Audio', 'Game')][string]$Component = 'Auto',
    [string]$Config = '',
    [string]$Distro = '',
    [string]$GameDirectory = '',
    [ValidateRange(1, 3600)][int]$TimeoutSeconds = 3600,
    [ValidateSet('Auto', 'Software', 'Hardware', 'None')][string]$Vulkan = 'Auto',
    [switch]$SetupDependencies,
    [switch]$SyncGameAssets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Phase -eq 'Test' -and !$PSBoundParameters.ContainsKey('TimeoutSeconds')) {
    $TimeoutSeconds = 15
}
. (Join-Path $PSScriptRoot 'linux-testing/Common.ps1')
$product = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Config) {
    $Config = Join-Path $PSScriptRoot 'linux-testing/local.settings.json'
}
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$localRun = Join-Path $product "artifacts/linux-test/runs/$runId"
New-Item -ItemType Directory -Force -Path $localRun | Out-Null
$exitCode = 1
$targetRoot = $null
$targetRunner = $null
$settings = $null
$streamProcess = $null
$started = $false
try {
    $settings = Get-TestSettings $Config
    if ($Mode -eq 'Hardware') {
        $settings = Select-HardwareTarget $settings $Target
    }
    if ($Distro) {
        $settings.Distro = $Distro
    }
    if ($GameDirectory) {
        $settings.GameDirectory = $GameDirectory
    }
    $profile = Get-LinuxBuildProfile $product
    $selectedComponent = if ($Component -eq 'Auto') { if ($profile.gameHost) { 'Game' } else { 'Launcher' } } else { $Component }
    $targetDisplay = if ($Mode -eq 'Hardware') { $Target } elseif ($settings.Distro) { $settings.Distro } else { 'WSL2 (auto)' }
    Write-Host "Linux testing: mode=$Mode target=$targetDisplay phase=$Phase component=$selectedComponent"
    Write-Host "Evidence directory: $localRun"
    if (!$profile.gameHost) {
        Write-Host 'Boundary: Linux gameplay is unavailable. This workflow tests the portable launcher or explicitly configured audio harness; it cannot accept gameplay.'
    }
    if ($Mode -eq 'Wsl') {
        Write-Host 'WSL/WSLg uses a virtualized GPU path. Its results do not accept native Linux or Steam Deck GPU behavior.'
    }
    if ($selectedComponent -eq 'Game' -and !$profile.gameHost) {
        throw 'The committed Linux build profile has no supported game host. Select Launcher or Audio.'
    }
    if ($Phase -ne 'Build') {
        if ($Mode -eq 'Wsl') {
            $inventory = Invoke-TestProcess wsl.exe @('--list', '--verbose') -AllowFailure -TimeoutSeconds 30
            ($inventory.Output + $inventory.Error).Replace([string][char]0, '') | Set-Content -LiteralPath (Join-Path $localRun 'wsl-inventory.txt') -Encoding utf8
            if ($inventory.ExitCode -ne 0) {
                throw 'WSL2 is not installed or unavailable. In an administrator terminal run: wsl --install -d Ubuntu-24.04 . Restart if requested, complete the Ubuntu first-run user setup, then retry this command. No installation or system changes were attempted.'
            }
            $settings.Distro = Select-WslDistribution $inventory.Output $settings.Distro
            Write-Host "Selected WSL2 distribution: $($settings.Distro)"
            $probe = Invoke-WslCommand $settings.Distro (Get-LinuxHostProbeCommand) -AllowFailure -TimeoutSeconds 30
            ($probe.Output + $probe.Error) | Set-Content -LiteralPath (Join-Path $localRun 'host-probe.txt') -Encoding utf8
            if ($probe.ExitCode -ne 0) {
                throw "Distro '$($settings.Distro)' is unavailable or lacks a .NET 10 SDK on PATH. Complete the distro setup and install a supported .NET 10 SDK, then retry. See docs/developers/linux-testing.md."
            }
            $hostInfo = ConvertFrom-LinuxHostProbe $probe.Output
            if ($hostInfo.machine -ne 'x86_64' -or $hostInfo.release -notmatch '(?i)microsoft.*wsl2') {
                throw 'This workflow requires an x86_64 WSL2 distro, not WSL1 or ARM emulation.'
            }
            if ($SetupDependencies) {
                Write-Host 'Explicit setup: installing ordinary Ubuntu runtime/diagnostic packages from configured distro repositories. sudo may request your Linux password.'
                # Sudo runs interactively; credentials are never captured by this script.
                & wsl.exe --distribution $settings.Distro --exec sh -c 'sudo apt-get update && sudo apt-get install -y fonts-dejavu-core fonts-noto-core fonts-noto-cjk libx11-6 libice6 libsm6 libfontconfig1 libfreetype6 libxcb1 libxkbcommon0 libgl1 libegl1 libgtk-3-0t64 dbus-user-session xdg-desktop-portal xdg-desktop-portal-gtk libasound2t64 libpulse0 libvulkan1 vulkan-tools mesa-utils mesa-vulkan-drivers'
                if ($LASTEXITCODE -ne 0) {
                    throw 'Explicit Ubuntu dependency setup failed. Review apt output; no external repository or graphics driver was requested.'
                }
            }
            $targetRoot = $hostInfo.home.TrimEnd('/') + '/.local/share/memoria-linux-test'
        }
        else {
            Assert-SshHost $settings.SshHost
            if ($SetupDependencies) {
                throw 'Hardware setup does not install packages or change SteamOS. Configure an existing .NET 10 SDK/OpenSSH and a graphical session; use the target vendor supported setup.'
            }
            $probe = Invoke-SshCommand $settings.SshHost (Get-LinuxHostProbeCommand) -AllowFailure -TimeoutSeconds 30
            ($probe.Output + $probe.Error) | Set-Content -LiteralPath (Join-Path $localRun 'host-probe.txt') -Encoding utf8
            if ($probe.ExitCode -ne 0) {
                throw 'The hardware target is unavailable or lacks a .NET 10 SDK on PATH. Check SSH access and install a supported .NET 10 SDK using your vendor instructions, then retry. See docs/developers/linux-testing.md.'
            }
            $hostInfo = ConvertFrom-LinuxHostProbe $probe.Output
            if ($hostInfo.machine -ne 'x86_64') {
                throw 'Hardware testing requires an x86_64 Linux target.'
            }
            $targetRoot = if ($settings.TargetDirectory.StartsWith('/')) { $settings.TargetDirectory } else { $hostInfo.home.TrimEnd('/') + '/' + $settings.TargetDirectory }
            if ($targetRoot -eq '/' -or $targetRoot.TrimEnd('/') -eq $hostInfo.home -or $targetRoot -match '(^|/)\.\.(/|$)' -or $targetRoot.Contains(':')) {
                throw 'Select a dedicated target staging directory below the home directory, not a home root or traversal path.'
            }
        }
        $helper = Get-LinuxTestingBootstrap $product $localRun
        $targetRunner = Initialize-LinuxTestingTarget $Mode $settings $helper $targetRoot
        $probe = Invoke-LinuxTarget $Mode $settings @($targetRunner, 'probe') -TimeoutSeconds 30
        $probe.Output | Set-Content -LiteralPath (Join-Path $localRun 'host-probe.json') -Encoding utf8
    }
    if ($Phase -eq 'Diagnostics') {
        $requestPath = Join-Path $localRun 'request.json'
        @{ root = $targetRoot; runId = $runId; mode = $Mode; component = $selectedComponent; diagnosticsOnly = $true; vulkan = $Vulkan; graphicalEnvironment = $settings.GraphicalEnvironment } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $requestPath -Encoding utf8
        $started = $true
        if ($Mode -eq 'Wsl') {
            $diagnostics = Invoke-WslCommand $settings.Distro @($targetRunner, 'run', (ConvertTo-WslPath $settings.Distro $requestPath)) -AllowFailure -TimeoutSeconds 90
        }
        else {
            [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '--', $requestPath, "$($settings.SshHost):$targetRoot/request.json"))
            $diagnostics = Invoke-SshCommand $settings.SshHost @($targetRunner, 'run', "$targetRoot/request.json") -AllowFailure -TimeoutSeconds 90
        }
        Write-Host $diagnostics.Output
        Write-Host $diagnostics.Error
        ($diagnostics.Output + $diagnostics.Error) | Set-Content -LiteralPath (Join-Path $localRun 'transport.log') -Encoding utf8
        $exitCode = $diagnostics.ExitCode
        return
    }
    Write-Host 'Build phase: fingerprint includes tracked/working source, dependency pins, build scripts and SDK version.'
    $fingerprint = Get-SourceFingerprint $product
    $cache = Join-Path $product "artifacts/linux-test/cache/$fingerprint"
    $payload = Join-Path $cache 'payload'
    if (!(Test-PayloadCache $cache $fingerprint)) {
        if ($Phase -in @('Deploy', 'Launch', 'Test')) {
            throw 'No verified cache matches the current working tree. Run the default command or -Phase Build first.'
        }
        if (Test-Path -LiteralPath $cache) {
            # A corrupted cache is preserved for diagnosis; fresh output gets a distinct staging path.
            $preserved = $cache + '-invalid-' + $runId
            $cacheRoot = [IO.Path]::GetFullPath((Join-Path $product 'artifacts/linux-test/cache')).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            if (![IO.Path]::GetFullPath($cache).StartsWith($cacheRoot, [StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::GetFullPath($preserved).StartsWith($cacheRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Cache preservation escaped the dedicated Linux test cache directory.'
            }
            Move-Item -LiteralPath $cache -Destination $preserved
        }
        New-Item -ItemType Directory -Force -Path $payload | Out-Null
        $isolatedBuild = Join-Path $product 'artifacts/linux-test/build'
        if ($profile.scope -eq 'Launcher') {
            $build = Invoke-TestProcess dotnet @('publish', (Join-Path $product 'src/Memoria.Launcher/Memoria.Launcher.csproj'), '--artifacts-path', $isolatedBuild, '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'true', '-p:PublishTrimmed=false', '-o', $payload)
        }
        else {
            $build = Invoke-TestProcess pwsh @('-NoProfile', '-File', (Join-Path $product 'Build.ps1'), '-Scope', $profile.scope, '-RuntimeIdentifier', 'linux-x64')
            $built = Join-Path $product $profile.gameOutput
            Get-ChildItem -LiteralPath $built -Force | Copy-Item -Destination $payload -Recurse
        }
        ($build.Output + $build.Error) | Set-Content -LiteralPath (Join-Path $localRun 'build.log') -Encoding utf8
        if (!(Test-Path -LiteralPath (Join-Path $payload 'Memoria.Launcher'))) {
            throw 'Linux launcher output is missing its x64 apphost.'
        }
        if ($profile.gameHost -and !(Test-Path -LiteralPath (Join-Path $payload $profile.gameExecutable))) {
            throw 'Declared Linux game distribution is missing its game executable.'
        }
        $audio = Invoke-TestProcess dotnet @('publish', (Join-Path $product 'src/Memoria.FF8.Music/Memoria.FF8.Music.csproj'), '--artifacts-path', $isolatedBuild, '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', (Join-Path $payload 'audio'))
        ($audio.Output + $audio.Error) | Set-Content -LiteralPath (Join-Path $localRun 'audio-build.log') -Encoding utf8
        foreach ($testProject in @('Memoria.FF8.Platform.Tests', 'Memoria.FF8.Audio.Tests', 'Memoria.Launcher.Tests', 'Memoria.FF8.Presentation.Tests', 'Memoria.FF8.LinuxTesting.Tests')) {
            $testBuild = Invoke-TestProcess dotnet @('publish', (Join-Path $product "tests/$testProject/$testProject.csproj"), '--artifacts-path', $isolatedBuild, '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', '-o', (Join-Path $payload "tests/$testProject"))
            ($testBuild.Output + $testBuild.Error) | Set-Content -LiteralPath (Join-Path $localRun "$testProject-build.log") -Encoding utf8
        }
        Copy-Item -LiteralPath (Join-Path $product 'global.json') -Destination (Join-Path $payload 'global.json')
        if ((Get-SourceFingerprint $product) -cne $fingerprint) {
            throw 'Source files changed during the build. Partial output was preserved; rerun after edits settle to create a verified current build.'
        }
        Assert-UnchangedBuildSource $product $fingerprint
        Get-PayloadManifest $payload | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $cache 'payload-manifest.json') -Encoding utf8
        @{ schemaVersion = 1; fingerprint = $fingerprint; runtimeIdentifier = 'linux-x64'; gameHost = [bool]$profile.gameHost } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $cache 'build.json') -Encoding utf8
        if (!(Test-PayloadCache $cache $fingerprint)) {
            throw 'New build failed cache integrity verification.'
        }
    }
    else {
        Write-Host "Reusing content-verified build $fingerprint"
    }
    if ($Phase -eq 'Build') {
        $exitCode = 0
        return
    }
    $manifest = Get-Content -LiteralPath (Join-Path $cache 'payload-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    $syncPath = Join-Path $localRun 'sync.json'
    $sync = @{ root = $targetRoot; manifest = $manifest; source = '' }
    if ($Mode -eq 'Wsl') {
        $sync.source = ConvertTo-WslPath $settings.Distro $payload
        $sync | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $syncPath -Encoding utf8
        [void](Invoke-WslCommand $settings.Distro @($targetRunner, 'sync', (ConvertTo-WslPath $settings.Distro $syncPath)))
    }
    else {
        $sync | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $syncPath -Encoding utf8
        [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '--', $syncPath, "$($settings.SshHost):$targetRoot/sync.json"))
        $remote = Invoke-SshCommand $settings.SshHost @($targetRunner, 'plan', "$targetRoot/sync.json")
        $remoteManifest = $remote.Output.Trim() | ConvertFrom-Json -AsHashtable
        $changed = @(Get-ChangedPayloadFiles $manifest $remoteManifest)
        Write-Host "Deploy phase: transferring $($changed.Count) changed payload files."
        if ($changed.Count -gt 0) {
            $archivePath = Join-Path $localRun 'deployment.zip'
            New-ChangedPayloadArchive $payload $changed $archivePath
            [void](Invoke-SshCommand $settings.SshHost @('mkdir', '-p', '--', "$targetRoot/inbox"))
            $remoteArchive = "$targetRoot/inbox/$runId.zip"
            [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '--', $archivePath, "$($settings.SshHost):$remoteArchive"))
            [void](Invoke-SshCommand $settings.SshHost @($targetRunner, 'accept', "$targetRoot/sync.json", '--archive', $remoteArchive))
        }
        [void](Invoke-SshCommand $settings.SshHost @($targetRunner, 'commit', "$targetRoot/sync.json"))
    }
    if ($SyncGameAssets) {
        if ($Mode -ne 'Hardware') {
            throw '-SyncGameAssets is a hardware-only one-time copy. WSL accesses explicitly selected Windows game files through wslpath.'
        }
        if (!$settings.GameDirectory -or !(Test-Path -LiteralPath $settings.GameDirectory -PathType Container)) {
            throw 'One-time game asset sync requires an explicitly configured existing GameDirectory on Windows.'
        }
        $assetPath = "$targetRoot/assets/game"
        $exists = Invoke-SshCommand $settings.SshHost @('test', '-e', $assetPath) -AllowFailure
        if ($exists.ExitCode -eq 0) {
            throw 'Target assets/game already exists. It is preserved. Configure HardwareGameDirectory to reuse it without -SyncGameAssets.'
        }
        Write-Host 'Explicit one-time asset sync: copying selected legal game files; subsequent payload deployments do not touch these files.'
        [void](Invoke-SshCommand $settings.SshHost @('mkdir', '-p', '--', "$targetRoot/assets"))
        [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '-r', '--', ([IO.Path]::GetFullPath($settings.GameDirectory)), "$($settings.SshHost):$assetPath") -TimeoutSeconds 7200)
        $settings.HardwareGameDirectory = $assetPath
        Write-Host "For subsequent runs set HardwareGameDirectory to: $assetPath"
    }
    if ($Phase -eq 'Deploy') {
        $exitCode = 0
        return
    }
    $request = @{ fingerprint = $fingerprint; vulkan = $Vulkan; root = $targetRoot; runId = $runId; mode = $Mode; component = $selectedComponent; timeoutSeconds = $TimeoutSeconds; smoke = $Phase -eq 'Test'; gameHost = [bool]$profile.gameHost; gameExecutable = $profile.gameExecutable; graphicalEnvironment = $settings.GraphicalEnvironment; screenshotCommand = @($settings.ScreenshotCommand); audioSeconds = [Math]::Min(10, $TimeoutSeconds); gameDirectory = $null; dlsPath = $null; sgtPath = $null }
    if ($Mode -eq 'Wsl') {
        $request.gameDirectory = ConvertTo-WslPath $settings.Distro $settings.GameDirectory
        $request.dlsPath = ConvertTo-WslPath $settings.Distro $settings.DlsPath
        $request.sgtPath = ConvertTo-WslPath $settings.Distro $settings.SgtPath
    }
    else {
        $request.gameDirectory = $settings.HardwareGameDirectory
        $request.dlsPath = $settings.HardwareDlsPath
        $request.sgtPath = $settings.HardwareSgtPath
        if (@($settings.StreamCommand).Count -gt 0) {
            $streamStart = [Diagnostics.ProcessStartInfo]::new($settings.StreamCommand[0])
            $streamStart.UseShellExecute = $false
            foreach ($argument in @($settings.StreamCommand | Select-Object -Skip 1)) {
                $streamStart.ArgumentList.Add($argument)
            }
            $streamProcess = [Diagnostics.Process]::Start($streamStart)
        }
    }
    $requestPath = Join-Path $localRun 'request.json'
    $request | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $requestPath -Encoding utf8
    Write-Host 'Launch/test phase: existing graphical session, bounded process tree, separate staging settings and diagnostics.'
    if ($request.smoke) {
        Write-Host "Bounded smoke interval: $TimeoutSeconds seconds."
    }
    elseif ($selectedComponent -ne 'Audio') {
        Write-Host 'Interact with the window or configured streaming client; close the application to collect its exit status. Ctrl+C cancels and cleans up owned processes.'
    }
    $started = $true
    if ($Mode -eq 'Wsl') {
        $run = Invoke-WslCommand $settings.Distro @($targetRunner, 'run', (ConvertTo-WslPath $settings.Distro $requestPath)) -AllowFailure -TimeoutSeconds ($TimeoutSeconds + 1080)
    }
    else {
        [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '--', $requestPath, "$($settings.SshHost):$targetRoot/request.json"))
        $run = Invoke-SshCommand $settings.SshHost @($targetRunner, 'run', "$targetRoot/request.json") -AllowFailure -TimeoutSeconds ($TimeoutSeconds + 1080)
    }
    ($run.Output + $run.Error) | Set-Content -LiteralPath (Join-Path $localRun 'transport.log') -Encoding utf8
    Write-Host $run.Output
    Write-Host $run.Error
    $exitCode = $run.ExitCode
}
catch {
    $_.Exception.Message | Set-Content -LiteralPath (Join-Path $localRun 'orchestration-error.txt') -Encoding utf8
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
}
finally {
    if ($started) {
        try {
            if ($Mode -eq 'Wsl') {
                [void](Invoke-WslCommand $settings.Distro @($targetRunner, 'stop', "$targetRoot/runs/$runId") -AllowFailure -TimeoutSeconds 15)
                $destination = ConvertTo-WslPath $settings.Distro $localRun
                [void](Invoke-WslCommand $settings.Distro @('cp', '-a', '--', "$targetRoot/runs/$runId/.", $destination) -AllowFailure -TimeoutSeconds 30)
            }
            else {
                [void](Invoke-SshCommand $settings.SshHost @($targetRunner, 'stop', "$targetRoot/runs/$runId") -AllowFailure -TimeoutSeconds 15)
                [void](Invoke-TestProcess scp @('-s', '-o', 'BatchMode=yes', '-r', '--', "$($settings.SshHost):$targetRoot/runs/$runId/.", $localRun) -AllowFailure -TimeoutSeconds 30)
            }
        }
        catch {
            Write-Warning "Could not finish cleanup/evidence retrieval: $($_.Exception.Message)"
        }
    }
    $targetResult = Join-Path $localRun 'result.json'
    if (Test-Path -LiteralPath $targetResult) {
        $observed = Get-Content -LiteralPath $targetResult -Raw | ConvertFrom-Json
        Write-Host ($observed | ConvertTo-Json -Depth 8)
    }
    if ($null -ne $streamProcess) {
        if (!$streamProcess.HasExited) {
            $streamProcess.Kill($true)
        }
        $streamProcess.Dispose()
    }
    @{ schemaVersion = 1; exitCode = $exitCode; phase = $Phase; mode = $Mode; gameplayAccepted = $false; nativeGpuAcceptance = $false; runId = $runId } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $localRun 'orchestration-result.json') -Encoding utf8
    Write-Host "Linux test exit code: $exitCode. Evidence: $localRun"
}
exit $exitCode
