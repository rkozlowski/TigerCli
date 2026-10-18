<#
    .SYNOPSIS
    Release validation: retrieves a release's set from its GitHub Release and
    proves it is the release, and that the feeds serve these exact packages.

    .DESCRIPTION
    Preparation accepted the product, and the release action inspected and
    smoke-tested these packages before it published them; this proves the
    chain from the commit to what users receive. It downloads the draft (or
    published) release's files into artifacts\release\<version> and checks:

      release     the GitHub Release exists, named 'TigerCli <version>'
      bytes       every file matches GitHub's recorded asset digest
      record      the set is closed and is the bytes release-artifacts.json
                  and SHA256SUMS.txt name
      tag         v<version> is the release workflow's annotated tag at the
                  record's commit, naming that record's SHA-256
      license     LICENSE is the exact bytes the commit holds for it
      packages    every package names the record's commit
      nuget.org   each package on nuget.org is this release's: the same
                  entries, byte for byte, apart from the repository signature
      GitHub      the same for GitHub Packages, which needs a token that can
                  read packages (read:packages)

    A draft is visible only to an authenticated session with push access, so
    gh must be logged in. Every check is required: no later stage runs one this
    script could not, so a check that cannot run here (no read:packages, an
    unreachable feed) is BLOCKED, never READY. Exit codes: 0 READY, 2 BLOCKED,
    1 FAIL.

    .EXAMPLE
    pwsh -File eng\release\Get-ReleaseArtifacts.ps1 -Version 0.9.5
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $OutputDirectory,
    [string] $RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TigerCliRelease.psm1')
if (-not $RepositoryRoot) { $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$facts = Get-TigerCliReleaseFacts
$tag = Get-TigerCliReleaseTag -Version $Version
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $RepositoryRoot "artifacts\release\$Version" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string] $Id, [string] $Status, [string] $Observed, [string] $Remediation = '') { $checks.Add((New-TigerCliReleaseCheck -Id $Id -Status $Status -Observed $Observed -Remediation $Remediation)) }
function Finish { exit (Write-TigerCliReleaseReport -Title "Release validation for $($facts.Product) $Version" -Checks $checks.ToArray() -Next @('READY TO PUBLISH: review the draft and publish it.') -RequireEveryCheck) }

try { $release = Get-TigerCliGitHubRelease -Tag $tag }
catch { Add-Check 'release' BLOCKED $_.Exception.Message 'Authenticate gh (gh auth login) and rerun.'; Finish }
if ($null -eq $release) { Add-Check 'release' BLOCKED "No GitHub Release for $tag is visible to this session." 'Run the release workflow, or authenticate gh with push access to see a draft.'; Finish }
if ([string] $release.name -cne "$($facts.Product) $Version") { Add-Check 'release' FAIL "The release for $tag is named '$($release.name)'."; Finish }
Add-Check 'release' PASS "$(if ($release.draft) { 'Draft' } else { 'Published release' }) '$($release.name)': $($release.html_url)"

if (Test-Path -LiteralPath $OutputDirectory) {
    $foreign = @(Get-ChildItem -LiteralPath $OutputDirectory -Force | Where-Object { $_.Name -cnotin @(Get-TigerCliReleaseAssetName -Version $Version) })
    if ($foreign.Count) { throw "$OutputDirectory holds files this script did not write: $($foreign.Name -join ', ')." }
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$token = if ($release.draft) { (Invoke-TigerCliGitHubCli @('auth', 'token')).output.Trim() } else { '' }
$badBytes = $false
foreach ($asset in @($release.assets)) {
    $path = Join-Path $OutputDirectory $asset.name
    $headers = @{ Accept = 'application/octet-stream' }
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    $code = Invoke-TigerCliDownload -Uri $asset.url -OutFile $path -Headers $headers
    if ($code -ne 200) { Add-Check "bytes/$($asset.name)" FAIL "Downloading it answered HTTP $code."; $badBytes = $true; continue }
    $digest = if ($null -ne $asset.PSObject.Properties['digest']) { [string] $asset.digest } else { '' }
    if ($digest -cne "sha256:$(Get-TigerCliFileSha256 $path)") { Add-Check "bytes/$($asset.name)" FAIL "The download does not match GitHub's recorded digest '$digest'."; $badBytes = $true }
}
if ($badBytes) { Finish }
Add-Check 'bytes' PASS "The $(@($release.assets).Count) downloads match GitHub's recorded digests."

try {
    $record = Assert-TigerCliReleaseProvenance -RepositoryRoot $RepositoryRoot -Directory $OutputDirectory -Version $Version
    Add-Check 'record' PASS "The closed set is the bytes release-artifacts.json names, for commit $($record.sourceCommit)."
    Add-Check 'tag' PASS "$tag is the release workflow's annotated tag at $($record.sourceCommit), naming this record."
}
catch { Add-Check 'provenance' FAIL $_.Exception.Message; Finish }
$commit = [string] $record.sourceCommit

$null = Invoke-TigerCliGit $RepositoryRoot @('fetch', '--quiet', 'origin', $commit)
try {
    $license = Assert-TigerCliReleaseLicense -RepositoryRoot $RepositoryRoot -Directory $OutputDirectory -CommitSha $commit
    Add-Check 'license' PASS "LICENSE is blob $($license.blob) of $commit."
}
catch { Add-Check 'license' FAIL $_.Exception.Message }

foreach ($package in $facts.Packages) {
    $metadata = (Read-TigerCliNuspec -Path (Join-Path $OutputDirectory "$($package.Id).$Version.nupkg")).package.metadata
    $named = [string] $metadata.repository.GetAttribute('commit')
    if ($named -ceq $commit) { Add-Check "package/$($package.Id)" PASS "names commit $commit." }
    else { Add-Check "package/$($package.Id)" FAIL "names commit '$named', not $commit." }
}

$githubToken = if ($env:GITHUB_PACKAGES_TOKEN) { $env:GITHUB_PACKAGES_TOKEN } else { $auth = Invoke-TigerCliGitHubCli @('auth', 'token'); if ($auth.ok) { $auth.output.Trim() } else { '' } }
foreach ($feed in $facts.Feeds) {
    foreach ($package in $facts.Packages) {
        $state = Get-TigerCliFeedPackageState -Feed $feed -PackageId $package.Id -Version $Version -ExpectedPackage (Join-Path $OutputDirectory "$($package.Id).$Version.nupkg") -Token $(if ($feed.AuthenticatedRead) { $githubToken } else { '' })
        $id = "$($feed.Key)/$($package.Id)"
        switch ($state.state) {
            'identical' { Add-Check $id PASS $state.observed }
            'different' { Add-Check $id FAIL $state.observed 'The feed serves another build of this version; it is not this release.' }
            'absent' { Add-Check $id BLOCKED $state.observed 'The release action publishes before it drafts; wait for the feed to serve it, then rerun.' }
            default {
                if ($feed.AuthenticatedRead) { Add-Check $id BLOCKED $state.observed 'Give the session a token with read:packages (gh auth refresh -s read:packages, or GITHUB_PACKAGES_TOKEN), then rerun.' }
                else { Add-Check $id BLOCKED $state.observed 'Rerun when the feed is reachable.' }
            }
        }
    }
}
Finish
