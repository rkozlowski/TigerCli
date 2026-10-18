<#
    .SYNOPSIS
    The release workflow's first gate: may this commit be released as this
    version? Nothing is built until it passes.

    .DESCRIPTION
    Checks, in order, and reports every result:

      version          the version is <Major>.<Minor>.<Patch> and is exactly
                       what Version.props records
      references       every current-version install example states it
      workflow         the release workflow's version input defaults to it
      notes            .github/release-notes/<version>.md exists and is useful
      license          the commit holds LICENSE, the release's License
      dispatch         in the workflow, it was dispatched on main
      commit/on-main   the commit is reachable from origin/main
      commit/promotion the commit is the accepted dev candidate, promoted with
                       its history and an identical tree
      tag/available    origin has no tag v<version>
      nuget/available  nuget.org has none of the packages at this version

    It proves only what the authoritative build needs to start safely; the
    verification gate and release acceptance ran on dev before promotion and
    are not repeated here.

    Exit codes: 0 PASS, 2 BLOCKED (the promotion has not happened yet, or a
    remote cannot be read), 1 FAIL. A maintainer runs the same script on dev
    before promotion, where commit/on-main is BLOCKED and dispatch is NOT RUN.

    .EXAMPLE
    pwsh -File eng\release\Assert-ReleaseCommitReady.ps1 -Version 0.9.5
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    # The commit to release; defaults to HEAD.
    [ValidatePattern('^([0-9a-fA-F]{40})?$')] [string] $CommitSha = '',
    # The checkout to check; defaults to this repository.
    [string] $RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TigerCliRelease.psm1')
$repoRoot = if ($RepositoryRoot) { $RepositoryRoot } else { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$facts = Get-TigerCliReleaseFacts
if (-not $CommitSha) { $CommitSha = (Invoke-TigerCliGit $repoRoot @('rev-parse', 'HEAD')).output }
$CommitSha = $CommitSha.ToLowerInvariant()

$checks = [Collections.Generic.List[object]]::new()
$checks.Add((Test-TigerCliSourceVersionMatches -RepositoryRoot $repoRoot -Version $Version))
$checks.Add((Test-TigerCliVersionReference -RepositoryRoot $repoRoot -Version $Version))
$checks.Add((Test-TigerCliWorkflowDefaultVersion -RepositoryRoot $repoRoot -Version $Version))
$checks.Add((Test-TigerCliReleaseNotes -RepositoryRoot $repoRoot -Version $Version))

$license = Invoke-TigerCliGit $repoRoot @('rev-parse', '--verify', '--quiet', "${CommitSha}:$($facts.License.source)")
$checks.Add($(if ($license.ok) { New-TigerCliReleaseCheck -Id 'license' -Status PASS -Observed "The commit holds $($facts.License.source) (blob $($license.output))." }
        else { New-TigerCliReleaseCheck -Id 'license' -Status FAIL -Observed "The commit has no $($facts.License.source)." -Remediation 'Commit the licence that applies to this release.' }))

$checks.Add($(if ($env:GITHUB_ACTIONS -cne 'true') {
            New-TigerCliReleaseCheck -Id 'dispatch' -Status 'NOT RUN' -Observed 'Not in a workflow; the release workflow checks that it was dispatched on main.'
        }
        elseif ($env:GITHUB_REF -ceq "refs/heads/$($facts.DefaultBranch)" -and "$env:GITHUB_SHA".ToLowerInvariant() -ceq $CommitSha) {
            New-TigerCliReleaseCheck -Id 'dispatch' -Status PASS -Observed "Dispatched on $env:GITHUB_REF at $CommitSha."
        }
        else {
            New-TigerCliReleaseCheck -Id 'dispatch' -Status FAIL -Observed "Dispatched on '$env:GITHUB_REF' at '$env:GITHUB_SHA'." -Remediation "Run the release workflow on $($facts.DefaultBranch); nothing is released from another branch."
        }))

$checks.Add((Test-TigerCliCommitOnMain -RepositoryRoot $repoRoot -CommitSha $CommitSha))
$checks.Add((Test-TigerCliPromotion -RepositoryRoot $repoRoot -CommitSha $CommitSha))

$tag = Get-TigerCliReleaseTag -Version $Version
try {
    $tagCommit = Get-TigerCliRemoteTagCommit -RepositoryRoot $repoRoot -Tag $tag
    $checks.Add($(if ($null -eq $tagCommit) { New-TigerCliReleaseCheck -Id 'tag/available' -Status PASS -Observed "origin has no tag $tag." }
            else { New-TigerCliReleaseCheck -Id 'tag/available' -Status FAIL -Observed "origin already has $tag at $tagCommit." -Remediation 'A version is released once. Release the next version instead.' }))
}
catch { $checks.Add((New-TigerCliReleaseCheck -Id 'tag/available' -Status BLOCKED -Observed $_.Exception.Message -Remediation 'Make origin readable and rerun.')) }

$nugetOrg = @($facts.Feeds | Where-Object Key -CEQ 'nugetorg')[0]
$published = [Collections.Generic.List[string]]::new()
$unknown = [Collections.Generic.List[string]]::new()
foreach ($package in $facts.Packages) {
    $state = Get-TigerCliFeedPackageState -Feed $nugetOrg -PackageId $package.Id -Version $Version -ExpectedPackage ''
    if ($state.state -ceq 'present') { $published.Add($package.Id) }
    elseif ($state.state -cne 'absent') { $unknown.Add($state.observed) }
}
$checks.Add($(if ($published.Count) {
            New-TigerCliReleaseCheck -Id 'nuget/available' -Status FAIL -Observed "nuget.org already has $Version of $($published -join ', ')." -Remediation 'A version is released once. If a previous run of this release failed after publishing, resume it with "Re-run failed jobs" instead; otherwise release the next version.'
        }
        elseif ($unknown.Count) { New-TigerCliReleaseCheck -Id 'nuget/available' -Status BLOCKED -Observed ($unknown -join ' ') -Remediation 'Rerun when nuget.org is reachable.' }
        else { New-TigerCliReleaseCheck -Id 'nuget/available' -Status PASS -Observed "nuget.org has no package at $Version." }))

$code = Write-TigerCliReleaseReport -Title "Release prerequisites for $($facts.Product) $Version at $CommitSha" -Checks $checks.ToArray() `
    -Next @("Start '$($facts.ReleaseWorkflowName)' for $Version on $($facts.DefaultBranch).")
exit $code
