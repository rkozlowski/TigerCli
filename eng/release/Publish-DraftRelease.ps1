<#
    .SYNOPSIS
    Tags the release commit and creates the draft GitHub Release carrying the
    exact release set. The draft is not published.

    .DESCRIPTION
    The release workflow's last step: outside the `Release TigerCli` workflow
    run for this commit on main it refuses to change anything (-PlanOnly still
    reports), so a set built anywhere else cannot become the release. It
    proves the set it received is the one the build recorded
    (-ExpectedRecordSha256), for this version and commit, that its LICENSE is
    the commit's, and that HEAD is that commit; then:

      tag    creates the annotated tag v<version> at the commit as
             github-actions[bot], its message naming the record's SHA-256 and
             the run, and pushes it; an existing tag is accepted only when it
             is that tag for this record - a tag is never moved or reused
      draft  creates the draft release 'TigerCli <version>' from
             .github/release-notes/<version>.md with the release set, or
             accepts an existing draft only when every asset it already
             carries is byte-identical (GitHub's recorded digest); missing
             assets are uploaded, nothing is replaced

    A rerun after a partial failure therefore completes the same release. A
    published release, a foreign asset or a different byte is refused.

    .EXAMPLE
    pwsh -File eng\release\Publish-DraftRelease.ps1 -Version 0.9.5 -ArtifactDirectory artifacts\release -CommitSha <sha> -ExpectedRecordSha256 <sha256> -PlanOnly
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $ArtifactDirectory,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{64}$')] [string] $ExpectedRecordSha256,
    # The workflow run that built the set, recorded in the tag.
    [string] $RunUrl,
    [switch] $PlanOnly,
    # The checkout to tag from; defaults to this repository.
    [string] $RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TigerCliRelease.psm1')
if (-not $RepositoryRoot) { $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$facts = Get-TigerCliReleaseFacts
$CommitSha = $CommitSha.ToLowerInvariant()
$ExpectedRecordSha256 = $ExpectedRecordSha256.ToLowerInvariant()
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
$tag = Get-TigerCliReleaseTag -Version $Version
$title = "$($facts.Product) $Version"

if (-not $PlanOnly -and -not (Test-TigerCliReleaseWorkflowRun -CommitSha $CommitSha)) {
    throw "Only the '$($facts.ReleaseWorkflowName)' workflow run for $CommitSha on $($facts.DefaultBranch) creates the tag and the draft; use -PlanOnly to see what it would do."
}

# What was received is what was built, from this commit.
$notes = Test-TigerCliReleaseNotes -RepositoryRoot $RepositoryRoot -Version $Version
if ($notes.status -cne 'PASS') { throw "Release notes: $($notes.observed)" }
$notesFile = Join-Path $RepositoryRoot "$($facts.ReleaseNotesDirectory)/$Version.md"
$null = Assert-TigerCliReleaseRecord -Directory $ArtifactDirectory -Version $Version -CommitSha $CommitSha -ExpectedRecordSha256 $ExpectedRecordSha256
$head = (Invoke-TigerCliGit $RepositoryRoot @('rev-parse', 'HEAD')).output.ToLowerInvariant()
if ($head -cne $CommitSha) { throw "HEAD is $head, not the release commit $CommitSha." }
$null = Assert-TigerCliReleaseLicense -RepositoryRoot $RepositoryRoot -Directory $ArtifactDirectory -CommitSha $CommitSha
$assetNames = @(Get-TigerCliReleaseAssetName -Version $Version)
$assets = @($assetNames | ForEach-Object { Get-Item -LiteralPath (Join-Path $ArtifactDirectory $_) })

# The tag: absent, or exactly the release workflow's tag for this record.
$existingTag = Get-TigerCliRemoteTag -RepositoryRoot $RepositoryRoot -Tag $tag
if ($null -ne $existingTag) {
    if ($existingTag.commit -cne $CommitSha) { throw "origin's $tag names $($existingTag.commit), not $CommitSha; a tag is never moved." }
    if (-not $existingTag.annotated -or $existingTag.tagger -cne 'github-actions[bot]' -or
        $existingTag.message -notmatch "(?m)^release-artifacts\.json sha256 $ExpectedRecordSha256$") {
        throw "origin's $tag is not the release workflow's tag for this release record; it is not reused."
    }
}

# The draft.
$release = Get-TigerCliGitHubRelease -Tag $tag
$missing = $assets
if ($null -ne $release) {
    if (-not $release.draft -or [string] $release.name -cne $title) {
        throw "A release for $tag already exists and is not the draft '$title' (draft: $($release.draft), name: '$($release.name)')."
    }
    $remote = @($release.assets)
    $remoteNames = @($remote | ForEach-Object { [string] $_.name })
    $foreign = @($remoteNames | Where-Object { $_ -cnotin $assetNames })
    if ($foreign.Count) { throw "The draft carries assets that are not this release's: $($foreign -join ', ')." }
    foreach ($asset in $assets) {
        $match = @($remote | Where-Object { [string] $_.name -ceq $asset.Name })
        if ($match.Count -eq 0) { continue }
        $digest = if ($null -ne $match[0].PSObject.Properties['digest']) { [string] $match[0].digest } else { '' }
        if ([long] $match[0].size -ne $asset.Length -or $digest -cne "sha256:$(Get-TigerCliFileSha256 $asset.FullName)") {
            throw "The draft's $($asset.Name) is not byte-identical to the built set (size $($match[0].size), digest '$digest')."
        }
    }
    $missing = @($assets | Where-Object { $_.Name -cnotin $remoteNames })
}
$missingNames = @($missing | ForEach-Object Name)

Write-Host "Release plan for $tag at $CommitSha"
Write-Host "  tag:   $(if ($null -eq $existingTag) { 'create the annotated tag and push it' } else { 'exists for this record' })"
Write-Host "  draft: $(if ($null -eq $release) { "create '$title'" } else { 'exists and is compatible' })"
foreach ($asset in $assets) { Write-Host "  asset: $($asset.Name) $(if ($asset.Name -cin $missingNames) { 'upload' } else { 'already identical' })" }
if ($PlanOnly) { Write-Host 'PLAN ONLY: nothing was changed.'; exit 0 }

if ($null -eq $existingTag) {
    $message = Get-TigerCliTagMessage -Version $Version -RecordSha256 $ExpectedRecordSha256 -RunUrl $RunUrl
    # A hosted runner has no Git identity; the tag's is set for this process only.
    $identity = @{ GIT_COMMITTER_NAME = 'github-actions[bot]'; GIT_COMMITTER_EMAIL = '41898282+github-actions[bot]@users.noreply.github.com' }
    $saved = @{}
    foreach ($name in $identity.Keys) { $saved[$name] = [Environment]::GetEnvironmentVariable($name); [Environment]::SetEnvironmentVariable($name, $identity[$name]) }
    try {
        $local = Invoke-TigerCliGit $RepositoryRoot @('rev-parse', '--verify', '--quiet', "refs/tags/$tag")
        if ($local.ok) { throw "A local tag $tag exists in the checkout; it is not reused." }
        $created = Invoke-TigerCliGit $RepositoryRoot @('tag', '-a', $tag, $CommitSha, '-m', $message)
        if (-not $created.ok) { throw "Could not create $tag`: $($created.output)" }
    }
    finally { foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
    $pushed = Invoke-TigerCliGit $RepositoryRoot @('push', 'origin', "refs/tags/$tag")
    if (-not $pushed.ok) { throw "Could not push $tag`: $($pushed.output)" }
}

if ($null -eq $release) {
    $created = Invoke-TigerCliGitHubCli (@('release', 'create', $tag, '--repo', $facts.Repository, '--draft', '--verify-tag', '--title', $title, '--notes-file', $notesFile) + @($assets.FullName))
    if (-not $created.ok) { throw "Could not create the draft release: $($created.output)" }
}
else {
    foreach ($asset in $missing) {
        $uploaded = Invoke-TigerCliGitHubCli @('release', 'upload', $tag, $asset.FullName, '--repo', $facts.Repository)
        if (-not $uploaded.ok) { throw "Could not upload $($asset.Name): $($uploaded.output)" }
    }
}

$release = Get-TigerCliGitHubRelease -Tag $tag
$checks = @(
    New-TigerCliReleaseCheck -Id 'tag' -Status PASS -Observed "$tag names $CommitSha and release record $ExpectedRecordSha256."
    New-TigerCliReleaseCheck -Id 'draft' -Status $(if ($null -ne $release -and $release.draft) { 'PASS' } else { 'FAIL' }) -Observed "Draft '$title': $(if ($release) { $release.html_url } else { 'not found after creation' })"
) + @($assets | ForEach-Object { New-TigerCliReleaseCheck -Id "asset/$($_.Name)" -Status PASS -Observed "$($_.Length) bytes, sha256 $(Get-TigerCliFileSha256 $_.FullName)" })
if ($RunUrl) { $checks += New-TigerCliReleaseCheck -Id 'run' -Status PASS -Observed $RunUrl }
exit (Write-TigerCliReleaseReport -Title "Draft release $title" -Checks $checks -Next @(
        'The draft is not published. Release validation (Get-ReleaseArtifacts.ps1) proves it; then review and publish the draft.'
    ))
