<#
    .SYNOPSIS
    Publishes the exact recorded packages of a release to GitHub Packages and
    nuget.org, in dependency order. nuget.org publication is irreversible.

    .DESCRIPTION
    The release workflow's publication step, and only that: outside the
    `Release TigerCli` workflow run for this commit on main it refuses to push
    (-PlanOnly still reports). It proves the set it received is the one the
    build recorded (-ExpectedRecordSha256), for this version and commit, then
    reads every feed before it pushes anything:

      absent     the package is pushed, without --skip-duplicate, and on
                 nuget.org its symbol package after it
      identical  the feed already has this release's package - the same
                 entries, byte for byte, apart from nuget.org's repository
                 signature - left by an earlier attempt of this same run;
                 it is not pushed again
      different  the feed has another build of this version: nothing is
                 pushed at all, and the release is FAIL - a version is
                 released once, so the fix is the next version
      unknown    a feed cannot be read: nothing is pushed, BLOCKED

    A rerun after a partial failure ("Re-run failed jobs", which keeps the
    built set) therefore completes the same publication. A push the feed
    refuses because the version exists although the feed does not serve it
    yet (nuget.org indexes a new package for some minutes) is BLOCKED: wait,
    then rerun. Nothing is reported published that was not proven.

    GitHub Packages is written first: it is the feed a person can still clean
    up. Credentials come from GITHUB_PACKAGES_TOKEN (the workflow's
    GITHUB_TOKEN) and NUGET_API_KEY (the short-lived Trusted Publishing key).

    .EXAMPLE
    pwsh -File eng\release\Publish-Packages.ps1 -Version 0.9.5 -ArtifactDirectory artifacts\release -CommitSha <sha> -ExpectedRecordSha256 <sha256> -PlanOnly
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $ArtifactDirectory,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{64}$')] [string] $ExpectedRecordSha256,
    [switch] $PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TigerCliRelease.psm1')
$facts = Get-TigerCliReleaseFacts
$CommitSha = $CommitSha.ToLowerInvariant()
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)

if (-not $PlanOnly -and -not (Test-TigerCliReleaseWorkflowRun -CommitSha $CommitSha)) {
    throw "Only the '$($facts.ReleaseWorkflowName)' workflow run for $CommitSha on $($facts.DefaultBranch) publishes packages; use -PlanOnly to see what it would do."
}
$null = Assert-TigerCliReleaseRecord -Directory $ArtifactDirectory -Version $Version -CommitSha $CommitSha -ExpectedRecordSha256 $ExpectedRecordSha256

# Read every feed before anything is pushed.
$plan = [Collections.Generic.List[object]]::new()
foreach ($feed in $facts.Feeds) {
    $token = [Environment]::GetEnvironmentVariable($feed.TokenVariable)
    foreach ($package in $facts.Packages) {
        $nupkg = Join-Path $ArtifactDirectory "$($package.Id).$Version.nupkg"
        $state = Get-TigerCliFeedPackageState -Feed $feed -PackageId $package.Id -Version $Version -ExpectedPackage $nupkg -Token $token
        $plan.Add([pscustomobject]@{ feed = $feed; package = $package; nupkg = $nupkg; state = $state.state; observed = $state.observed; token = $token })
    }
}

Write-Host "Publication plan for $($facts.Product) $Version at $CommitSha"
foreach ($step in $plan) {
    $action = switch ($step.state) { 'absent' { 'push' } 'identical' { 'already published' } 'different' { 'REFUSE' } default { 'cannot read' } }
    Write-Host ('  {0,-16} {1,-26} {2,-18} {3}' -f $step.feed.Name, $step.package.Id, $action, $step.observed)
}

$checks = [Collections.Generic.List[object]]::new()
$different = @($plan | Where-Object state -CEQ 'different')
$unknown = @($plan | Where-Object state -CEQ 'unknown')
if ($different.Count) {
    foreach ($step in $different) {
        $checks.Add((New-TigerCliReleaseCheck -Id "$($step.feed.Key)/$($step.package.Id)" -Status FAIL -Observed $step.observed -Remediation 'This version was published from another build and is not this release. Nothing was pushed; release the next version.'))
    }
}
if ($unknown.Count) {
    foreach ($step in $unknown) {
        $checks.Add((New-TigerCliReleaseCheck -Id "$($step.feed.Key)/$($step.package.Id)" -Status $(if ($PlanOnly) { 'NOT RUN' } else { 'BLOCKED' }) -Observed $step.observed -Remediation 'Nothing was pushed. Make the feed readable, then rerun the failed job.'))
    }
}
if ($PlanOnly) {
    if (-not $checks.Count) { $checks.Add((New-TigerCliReleaseCheck -Id 'plan' -Status PASS -Observed 'Every feed was read; the plan above is what a release run would do.')) }
    $null = Write-TigerCliReleaseReport -Title "Publication plan for $($facts.Product) $Version (PLAN ONLY: nothing was pushed)" -Checks $checks.ToArray()
    exit $(if ($different.Count) { 1 } else { 0 })
}
if ($checks.Count) { exit (Write-TigerCliReleaseReport -Title "Publish $($facts.Product) $Version" -Checks $checks.ToArray()) }

function Push-Package([object] $Step, [string] $Path, [bool] $Symbols) {
    $arguments = @('nuget', 'push', $Path, '--source', $Step.feed.PushSource, '--api-key', $Step.token)
    if (-not $Symbols) { $arguments += '--no-symbols' }
    Invoke-TigerCliDotNet $arguments
}
function Test-Conflict([object] $Result) { $Result.output -match '\b409\b|already exists|already contains' }

foreach ($step in $plan) {
    $id = "$($step.feed.Key)/$($step.package.Id)"
    $snupkg = [IO.Path]::ChangeExtension($step.nupkg, '.snupkg')
    if ($step.state -ceq 'absent') {
        $pushed = Push-Package $step $step.nupkg $false
        if (-not $pushed.ok) {
            if (Test-Conflict $pushed) {
                $checks.Add((New-TigerCliReleaseCheck -Id $id -Status BLOCKED -Observed "$($step.feed.Name) reports $($step.package.Id) $Version exists, but does not serve it yet for comparison." -Remediation 'Wait until the feed serves it (nuget.org indexes new packages for some minutes), then rerun the failed job; it verifies the package before going on.'))
            }
            else { $checks.Add((New-TigerCliReleaseCheck -Id $id -Status FAIL -Observed "Push failed ($($pushed.exitCode)): $($pushed.output)" -Remediation 'Fix the cause, then rerun the failed job.')) }
            break
        }
        $checks.Add((New-TigerCliReleaseCheck -Id $id -Status PASS -Observed "pushed the recorded $(Split-Path -Leaf $step.nupkg)."))
        if ($step.feed.Symbols) {
            $symbols = Push-Package $step $snupkg $true
            if (-not $symbols.ok) {
                $checks.Add((New-TigerCliReleaseCheck -Id "$id/symbols" -Status FAIL -Observed "Symbol push failed ($($symbols.exitCode)): $($symbols.output)" -Remediation 'Fix the cause, then rerun the failed job; the package is verified and only the symbols are pushed again.'))
                break
            }
            $checks.Add((New-TigerCliReleaseCheck -Id "$id/symbols" -Status PASS -Observed "pushed the recorded $(Split-Path -Leaf $snupkg)."))
        }
    }
    else {
        $checks.Add((New-TigerCliReleaseCheck -Id $id -Status PASS -Observed $step.observed))
        if ($step.feed.Symbols) {
            # The package on the feed is this run's, so any symbol package the feed already
            # has for it was pushed with it by this run; a conflict means it is there.
            $symbols = Push-Package $step $snupkg $true
            if ($symbols.ok) { $checks.Add((New-TigerCliReleaseCheck -Id "$id/symbols" -Status PASS -Observed "pushed the recorded $(Split-Path -Leaf $snupkg).")) }
            elseif (Test-Conflict $symbols) { $checks.Add((New-TigerCliReleaseCheck -Id "$id/symbols" -Status PASS -Observed "$($step.feed.Name) already has the symbols this run pushed with the package.")) }
            else {
                $checks.Add((New-TigerCliReleaseCheck -Id "$id/symbols" -Status FAIL -Observed "Symbol push failed ($($symbols.exitCode)): $($symbols.output)" -Remediation 'Fix the cause, then rerun the failed job.'))
                break
            }
        }
    }
}

exit (Write-TigerCliReleaseReport -Title "Publish $($facts.Product) $Version" -Checks $checks.ToArray() -Next @('Every package is on every feed. The tag and the draft release follow.'))
