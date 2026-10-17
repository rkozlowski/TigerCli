<#
    .SYNOPSIS
    Builds TigerCli's release set from one commit and closes it.

    .DESCRIPTION
    This is the build the release workflow runs, and it runs the same way on a
    developer machine. From a clean checkout of the release commit it:

      1. packs ItTiger.Core, ItTiger.TigerCli and ItTiger.TigerCli.PngSink
         once, as a continuous-integration build stamped with the commit
         (ContinuousIntegrationBuild, RepositoryCommit);
      2. inspects every package and symbol package (Test-TigerCliPackage):
         identity, version, the commit, metadata, README, icon, assemblies,
         dependencies at exactly this version, PDBs, PngSink's assets and fonts;
      3. restores and runs the packages from a local feed
         (Invoke-TigerCliPackageSmoke), so nothing reaches a feed unproven;
      4. freezes LICENSE, the exact bytes the commit holds for it;
      5. writes release-artifacts.json and SHA256SUMS.txt over the closed set.

    The output directory then holds exactly the release set. Only the set the
    release workflow builds is the release; a set built anywhere else is a
    candidate, however it was built.

    -Rehearsal builds from a working tree that need not be a clean checkout:
    preparation's way to prove the release build before the release commit
    exists. Its licence is the working tree's, and its packages and record name
    HEAD, so a rehearsal set is never a release: nothing publishes it.

    .EXAMPLE
    pwsh -File eng\release\Build-ReleaseArtifacts.ps1 -Version 0.9.5 -Rehearsal
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    # The commit being built; defaults to HEAD, and must be HEAD.
    [ValidatePattern('^([0-9a-fA-F]{40})?$')] [string] $CommitSha = '',
    # Defaults to artifacts\release-candidate\<version>; the workflow names its own.
    [string] $OutputDirectory,
    # Allow a working tree that is not a clean checkout of the commit.
    [switch] $Rehearsal,
    # A GITHUB_OUTPUT file to append record_sha256 to.
    [string] $GitHubOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TigerCliRelease.psm1')
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$facts = Get-TigerCliReleaseFacts

# The commit and the tree.
$versionCheck = Test-TigerCliSourceVersionMatches -RepositoryRoot $repoRoot -Version $Version
if ($versionCheck.status -cne 'PASS') { throw $versionCheck.observed }
$head = (Invoke-TigerCliGit $repoRoot @('rev-parse', 'HEAD')).output.ToLowerInvariant()
if (-not $CommitSha) { $CommitSha = $head }
$CommitSha = $CommitSha.ToLowerInvariant()
if ($head -cne $CommitSha) { throw "HEAD is $head, not the release commit $CommitSha." }
if (-not $Rehearsal) {
    $status = (Invoke-TigerCliGit $repoRoot @('status', '--porcelain', '--untracked-files=normal')).output
    if ($status) { throw "The working tree is not a clean checkout of $CommitSha (use -Rehearsal to build a candidate):`n$status" }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repoRoot "artifacts\release-candidate\$Version" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$workDirectory = Join-Path $repoRoot "artifacts\release-work\$Version"
$assetNames = @(Get-TigerCliReleaseAssetName -Version $Version)
foreach ($directory in @($OutputDirectory, $workDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        # Only a directory this script wrote is emptied.
        $foreign = @(Get-ChildItem -LiteralPath $directory -Force | Where-Object { $_.Name -cnotin $assetNames -and $_.Name -cne 'packages' })
        if ($foreign.Count) { throw "$directory holds files this script did not write: $($foreign.Name -join ', ')." }
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    $null = New-Item -ItemType Directory -Path $directory -Force
}
$packageDirectory = Join-Path $workDirectory 'packages'

# 1. Pack once, stamped with the commit.
foreach ($package in $facts.Packages) {
    $result = Invoke-TigerCliDotNet @('pack', (Join-Path $repoRoot $package.Project), '-c', 'Release', '-o', $packageDirectory,
        '-p:ContinuousIntegrationBuild=true', "-p:RepositoryCommit=$CommitSha", '--disable-build-servers', '--nologo')
    if (-not $result.ok) { throw "dotnet pack $($package.Id) failed ($($result.exitCode)):`n$($result.output)" }
}
$produced = @(Get-ChildItem -LiteralPath $packageDirectory -File | ForEach-Object Name | Sort-Object)
$expectedPackages = @(Get-TigerCliReleaseAsset -Version $Version | Where-Object packageId | ForEach-Object name | Sort-Object)
if (($produced -join ';') -cne ($expectedPackages -join ';')) { throw "Pack produced [$($produced -join ', ')], not [$($expectedPackages -join ', ')]." }

# 2. Inspect.
$checks = [Collections.Generic.List[object]]::new()
foreach ($package in $facts.Packages) {
    $problems = @(Test-TigerCliPackage -Directory $packageDirectory -PackageId $package.Id -Version $Version -CommitSha $CommitSha)
    if ($problems.Count) { throw "$($package.Id) $Version is not releasable: $($problems -join '; ')." }
    $checks.Add((New-TigerCliReleaseCheck -Id "package/$($package.Id)" -Status PASS -Observed "$Version, commit $($CommitSha.Substring(0, 12)), metadata, README, icon, assemblies, dependencies and symbols as required."))
}

# 3. Usable from a feed.
foreach ($check in @(Invoke-TigerCliPackageSmoke -PackageDirectory $packageDirectory -Version $Version)) { $checks.Add($check) }

# 4. The licence, frozen from the commit.
$license = Copy-TigerCliReleaseLicense -RepositoryRoot $repoRoot -Directory $OutputDirectory -CommitSha $CommitSha -WorkingTree:$Rehearsal

# 5. The closed set.
foreach ($name in $expectedPackages) { Copy-Item -LiteralPath (Join-Path $packageDirectory $name) -Destination (Join-Path $OutputDirectory $name) }
Remove-Item -LiteralPath $workDirectory -Recurse -Force
$recordSha256 = Write-TigerCliReleaseRecord -Directory $OutputDirectory -Version $Version -CommitSha $CommitSha
$null = Assert-TigerCliReleaseRecord -Directory $OutputDirectory -Version $Version -CommitSha $CommitSha -ExpectedRecordSha256 $recordSha256
if (-not $Rehearsal) { $null = Assert-TigerCliReleaseLicense -RepositoryRoot $repoRoot -Directory $OutputDirectory -CommitSha $CommitSha }
if ($GitHubOutput) { "record_sha256=$recordSha256" | Out-File -LiteralPath $GitHubOutput -Encoding utf8 -Append }

foreach ($file in @(Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name)) {
    $checks.Add((New-TigerCliReleaseCheck -Id "asset/$($file.Name)" -Status PASS -Observed "$($file.Length) bytes, sha256 $(Get-TigerCliFileSha256 $file.FullName)"))
}
$checks.Add((New-TigerCliReleaseCheck -Id 'license' -Status PASS -Observed "LICENSE is blob $($license.blob)$(if ($Rehearsal) { ' (the working tree''s)' } else { " at $($CommitSha.Substring(0, 12))" })."))
$sdk = (Invoke-TigerCliDotNet @('--version')).output.Trim()
$checks.Add((New-TigerCliReleaseCheck -Id 'record' -Status PASS -Observed "release-artifacts.json sha256 $recordSha256, commit $CommitSha, .NET SDK $sdk."))
$title = "$($facts.Product) $Version release set$(if ($Rehearsal) { ' (REHEARSAL - a candidate, never a release)' }) at $OutputDirectory"
exit (Write-TigerCliReleaseReport -Title $title -Checks $checks.ToArray())
