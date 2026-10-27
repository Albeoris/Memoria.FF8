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
$hostProbe = ConvertFrom-LinuxHostProbe "x86_64`n6.6-microsoft-standard-WSL2`n/home/test user`n9.0.100 [/usr/share/dotnet/sdk]`n10.0.100 [/usr/share/dotnet/sdk]`n"
Assert-True ($hostProbe.machine -eq 'x86_64' -and $hostProbe.home -eq '/home/test user') 'Shell host probing must preserve architecture and home path spaces.'
Assert-Throws { ConvertFrom-LinuxHostProbe "x86_64`n6.6`n/home/test`n9.0.100 [/usr/share/dotnet/sdk]" } 'The target must have a .NET 10 SDK, not only an older SDK.'
Assert-Throws { ConvertFrom-LinuxHostProbe "x86_64`n6.6`n/home/test`n" } 'An absent SDK must fail before bootstrap.'
Assert-Throws { ConvertFrom-LinuxHostProbe "x86_64`n6.6`nrelative-home`n10.0.100 [/usr/share/dotnet/sdk]" } 'A relative target home path must fail.'
Assert-True (((Get-LinuxHostProbeCommand) -join ' ').Contains('dotnet --list-sdks')) 'The shell bootstrap probe must check the installed SDK.'
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
@{ MediaToolsDirectory = '/home/test/approved media/bin'; Targets = @{ Desktop = @{ HardwareMediaToolsDirectory = '/opt/approved media/bin' } } } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $settingsFile
$mediaSettings = Get-TestSettings $settingsFile
Assert-True ($mediaSettings.MediaToolsDirectory -eq '/home/test/approved media/bin') 'Explicit WSL media tools must preserve path spaces.'
Assert-True ((ConvertTo-WslPath 'Ubuntu-24.04' $mediaSettings.MediaToolsDirectory) -eq '/home/test/approved media/bin') 'Existing absolute Linux media paths must not become Windows drive paths.'
Assert-True ((Select-HardwareTarget $mediaSettings 'Desktop').HardwareMediaToolsDirectory -eq '/opt/approved media/bin') 'Hardware media tools must select the target-specific native directory.'
$parentPrepared = [Environment]::GetEnvironmentVariable('MemoriaPsfDecoderPrepared')
$child = Invoke-TestProcess pwsh @('-NoProfile', '-Command', '[Console]::Write($env:MemoriaPsfDecoderPrepared)') -Environment @{ MemoriaPsfDecoderPrepared = 'test-child-only' }
Assert-True ($child.Output -ceq 'test-child-only') 'Prepared codec properties must reach only the child build process.'
Assert-True ([Environment]::GetEnvironmentVariable('MemoriaPsfDecoderPrepared') -ceq $parentPrepared) 'Successful child builds must leave the parent environment unchanged.'
Assert-Throws { Invoke-TestProcess pwsh @('-NoProfile', '-Command', 'exit 3') -Environment @{ MemoriaPsfDecoderPrepared = 'test-child-only' } } 'Child build failures must propagate.'
Assert-True ([Environment]::GetEnvironmentVariable('MemoriaPsfDecoderPrepared') -ceq $parentPrepared) 'Failed child builds must leave the parent environment unchanged.'
$nativeSource = Join-Path $temp 'native-source'
New-Item -ItemType Directory -Path $nativeSource | Out-Null
Set-Content -LiteralPath (Join-Path $nativeSource 'decoder.c') -Value 'native-first'
Set-Content -LiteralPath (Join-Path $nativeSource 'CMakeLists.txt') -Value 'cmake-first'
$nativeIdentity = Get-PsfNativeBuildIdentity $nativeSource '/mnt/c/source with spaces' 'compiler-first'
Assert-True ($nativeIdentity -ceq (Get-PsfNativeBuildIdentity $nativeSource '/mnt/c/source with spaces' 'compiler-first')) 'Unchanged native source and toolchain must reuse the CMake cache identity.'
Assert-True ($nativeIdentity -cne (Get-PsfNativeBuildIdentity $nativeSource '/mnt/d/source with spaces' 'compiler-first')) 'Moving the native source must invalidate absolute CMake source paths.'
Assert-True ($nativeIdentity -cne (Get-PsfNativeBuildIdentity $nativeSource '/mnt/c/source with spaces' 'compiler-second')) 'Changing native compilers must invalidate the CMake cache.'
Set-Content -LiteralPath (Join-Path $nativeSource 'decoder.c') -Value 'native-second'
Assert-True ($nativeIdentity -cne (Get-PsfNativeBuildIdentity $nativeSource '/mnt/c/source with spaces' 'compiler-first')) 'Changing codec source must invalidate native build identity.'
$decoder = Join-Path $nativeSource 'Memoria.PsfDecoder'
$elf = [byte[]]::new(20)
$elf[0] = 127
$elf[1] = 69
$elf[2] = 76
$elf[3] = 70
$elf[4] = 2
$elf[5] = 1
$elf[6] = 1
$elf[16] = 3
$elf[18] = 62
[IO.File]::WriteAllBytes($decoder, $elf)
Assert-LinuxX64Executable $decoder
Copy-Item -LiteralPath $decoder -Destination $payload
Assert-PayloadPsfDecoder $payload $nativeSource
Add-Content -LiteralPath (Join-Path $payload 'Memoria.PsfDecoder') -Value 'tamper'
Assert-Throws { Assert-PayloadPsfDecoder $payload $nativeSource } 'A copied codec with a different hash must prevent accepting the payload.'
$elf[18] = 183
[IO.File]::WriteAllBytes($decoder, $elf)
Assert-Throws { Assert-LinuxX64Executable $decoder } 'An ARM ELF codec must not pass x64 preparation.'
[IO.File]::WriteAllBytes($decoder, [byte[]]@(77, 90))
Assert-Throws { Assert-LinuxX64Executable $decoder } 'A Windows or truncated codec must not pass Linux preparation.'
$script:commands = [Collections.Generic.List[object]]::new()
function Invoke-TestProcess {
    param([string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds = 1800, [switch]$AllowFailure, [hashtable]$Environment = @{})
    $script:commands.Add(@{ executable = $Executable; arguments = $Arguments })
    if ($Executable -eq 'git') {
        return [pscustomobject]@{ ExitCode = 0; Output = "source.cs`0Directory.Packages.props`0tools/test.ps1`0"; Error = '' }
    }
    if ($Executable -eq 'dotnet') {
        if ($Arguments[0] -eq 'publish') {
            $outputIndex = [Array]::IndexOf($Arguments, '-o') + 1
            Set-Content -LiteralPath (Join-Path $Arguments[$outputIndex] 'Memoria.FF8.LinuxTesting') -Value 'synthetic helper'
            return [pscustomobject]@{ ExitCode = 0; Output = 'published'; Error = '' }
        }
        return [pscustomobject]@{ ExitCode = 0; Output = '10.0.test'; Error = '' }
    }
    return [pscustomobject]@{ ExitCode = 0; Output = '/mnt/w/Game files'; Error = '' }
}
$converted = ConvertTo-WslPath 'Ubuntu-24.04' 'W:\Game files'
Assert-True ($converted -eq '/mnt/w/Game files') 'Mapped drives must use wslpath output.'
$last = $script:commands[$script:commands.Count - 1]
Assert-True ($last.arguments[3] -eq 'wslpath') 'Path conversion must invoke wslpath.'
Assert-True ($last.arguments[-1].EndsWith('W:\Game files')) 'Spaces/drive letters must stay one argument.'
[void](Invoke-SshCommand 'deck' @('/path with spaces/Memoria.FF8.LinuxTesting', 'probe'))
$last = $script:commands[$script:commands.Count - 1]
Assert-True ($last.arguments[-1].Contains("'/path with spaces/Memoria.FF8.LinuxTesting'")) 'SSH helper arguments must be individually quoted.'
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
$helper = Get-LinuxTestingBootstrap $temp $temp
$publishes = @($script:commands | Where-Object { $_.executable -eq 'dotnet' -and $_.arguments[0] -eq 'publish' })
Assert-True ($publishes.Count -eq 1) 'The bootstrap helper must publish once on a cache miss.'
Assert-True ($publishes[0].arguments -contains '-p:PublishSingleFile=true') 'The bootstrap helper must be a single-file publish.'
$selfContainedIndex = [Array]::IndexOf($publishes[0].arguments, '--self-contained')
Assert-True ($selfContainedIndex -ge 0 -and $publishes[0].arguments[$selfContainedIndex + 1] -eq 'false') 'The bootstrap helper must use the required target .NET SDK runtime instead of embedding another runtime.'
Assert-True ((Get-LinuxTestingBootstrap $temp $temp) -ceq $helper) 'A verified bootstrap cache must be reused.'
$publishes = @($script:commands | Where-Object { $_.executable -eq 'dotnet' -and $_.arguments[0] -eq 'publish' })
Assert-True ($publishes.Count -eq 1) 'A verified bootstrap helper must not rebuild.'
Add-Content -LiteralPath $helper -Value 'tampered'
[void](Get-LinuxTestingBootstrap $temp $temp)
$publishes = @($script:commands | Where-Object { $_.executable -eq 'dotnet' -and $_.arguments[0] -eq 'publish' })
Assert-True ($publishes.Count -eq 2) 'A corrupted bootstrap helper must be rebuilt.'
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
$script:targetCommands = [Collections.Generic.List[object]]::new()
$script:rejectInitialization = $false
function Invoke-LinuxTarget {
    param([string]$Mode, $Settings, [string[]]$Command, [switch]$AllowFailure, [int]$TimeoutSeconds = 1800)
    $script:targetCommands.Add($Command)
    if ($Command[0] -eq 'mktemp') {
        return [pscustomobject]@{ ExitCode = 0; Output = '/tmp/memoria-linux-test.A1b2C3d4E5'; Error = '' }
    }
    if ($Command[1] -eq 'init' -and $script:rejectInitialization) {
        throw 'Synthetic staging ownership rejection'
    }
    return [pscustomobject]@{ ExitCode = 0; Output = ''; Error = '' }
}
$targetHelper = Initialize-LinuxTestingTarget 'Wsl' $defaults $helper '/home/test/staging'
Assert-True ($targetHelper -eq '/home/test/staging/Memoria.FF8.LinuxTesting') 'Bootstrap must return the staged helper executable.'
$initIndex = -1
$copyIndex = -1
for ($index = 0; $index -lt $script:targetCommands.Count; $index++) {
    $command = $script:targetCommands[$index]
    if ($command[1] -eq 'init') {
        $initIndex = $index
    }
    if ($command[0] -eq 'cp' -and $command[-1] -eq $targetHelper) {
        $copyIndex = $index
    }
}
Assert-True ($initIndex -ge 0 -and $copyIndex -gt $initIndex) 'Ownership initialization must finish before copying into staging.'
Assert-True ($script:targetCommands[-2][0] -eq 'rm' -and $script:targetCommands[-1][0] -eq 'rmdir') 'Bootstrap must remove only its transferred helper and temporary directory.'
$script:targetCommands.Clear()
$script:rejectInitialization = $true
Assert-Throws { Initialize-LinuxTestingTarget 'Hardware' (@{ SshHost = 'deck' }) $helper '/home/test/staging' } 'A rejected ownership check must abort bootstrap.'
Assert-True (@($script:targetCommands | Where-Object { $_[0] -eq 'cp' -and $_[-1] -eq $targetHelper }).Count -eq 0) 'Ownership rejection must prevent copying into staging.'
Assert-True ($script:targetCommands[-1][0] -eq 'rmdir') 'Bootstrap temporary cleanup must run after ownership rejection.'
Write-Host "$assertions orchestration assertions passed; no WSL/SSH connection or installation performed."
# Keep the tiny synthetic fixture directory for inspection; no game/user data is used.
