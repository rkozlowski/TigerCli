<#
    .SYNOPSIS
    Focused tests for the release tooling in eng\release.

    .DESCRIPTION
    Everything runs against synthetic repositories under the temporary
    directory - local bare repositories stand in for origin - and against
    scripted stand-ins for gh, dotnet and the package feeds, so no test
    reaches GitHub or a feed, builds anything, or touches the real checkout.
    Git identity comes from the environment for the duration of the run,
    because a CI runner has none. The real package build and smoke test are
    proven by `Build-ReleaseArtifacts.ps1 -Rehearsal`, not here.

    Scenarios:

      gates        version, install-example references, the workflow's
                   prefilled version, release notes (missing, BOM, heading,
                   thin, placeholder, leak, good)
      workflow     the real release workflow calls only tracked, existing
                   eng/release scripts, ends each with its exit code, and uses
                   only the action versions proven by earlier releases
      record       release-artifacts.json and SHA256SUMS.txt: written over the
                   closed set; refused for a changed byte, a missing or foreign
                   file, another commit or version, or a record changed in
                   transit
      license      frozen as the committed blob whatever the checkout's line
                   endings, and refused when changed
      promotion    a history-preserving merge and a commit on dev pass; a
                   squash merge, a rebase or cherry-pick, and a merge that
                   brought in a main-only change fail; commit-on-main and the
                   tag lookup
      prerequisites Assert-ReleaseCommitReady.ps1 end to end: PASS after
                   promotion, BLOCKED before it, FAIL for an existing tag or a
                   version already on nuget.org
      identity     a feed's signed copy is the recorded package; another
                   build, a missing or an extra entry is not
      publish      Publish-Packages.ps1: the first run pushes every package in
                   dependency order, GitHub Packages first, nuget.org with
                   symbols, never --skip-duplicate; a rerun after partial
                   publication pushes only what is missing; another build on
                   a feed, an unreadable feed and a lagging feed push nothing
                   more and are FAIL, BLOCKED and BLOCKED; outside the workflow
                   it refuses
      draft        Publish-DraftRelease.ps1: tags as github-actions[bot] and
                   drafts with the nine files; a rerun uploads only what is
                   missing; a foreign asset, a published release and a tag at
                   another commit are refused
      validation   Get-ReleaseArtifacts.ps1: READY for the drafted set, FAIL
                   for a byte that is not GitHub's digest

    .EXAMPLE
    pwsh -File eng\release\Test-Release.ps1
#>
#Requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'TigerCliRelease.psm1') -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$script:failures = [Collections.Generic.List[string]]::new()
$script:checks = 0
$script:scenario = ''
$script:root = Join-Path ([IO.Path]::GetTempPath()) "tigercli-release-tests-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $script:root
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$version = '1.2.3'

$identity = @{ GIT_AUTHOR_NAME = 'Release Test'; GIT_AUTHOR_EMAIL = 'release-test@example.invalid'; GIT_COMMITTER_NAME = 'Release Test'; GIT_COMMITTER_EMAIL = 'release-test@example.invalid' }
$workflowVariables = @('GITHUB_ACTIONS', 'GITHUB_WORKFLOW', 'GITHUB_SHA', 'GITHUB_REF', 'GITHUB_STEP_SUMMARY', 'GITHUB_ACTOR', 'GITHUB_PACKAGES_TOKEN', 'NUGET_API_KEY')
$savedEnvironment = @{}
foreach ($name in @($identity.Keys) + $workflowVariables) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
foreach ($name in $identity.Keys) { [Environment]::SetEnvironmentVariable($name, $identity[$name]) }
foreach ($name in $workflowVariables) { [Environment]::SetEnvironmentVariable($name, $null) }

function Start-Scenario { param([string] $Name) $script:scenario = $Name; Write-Host "  $Name" }

function Assert-True {
    param([Parameter(Mandatory)] [bool] $Condition, [Parameter(Mandatory)] [string] $Message)
    $script:checks++
    if (-not $Condition) { $script:failures.Add("$($script:scenario): $Message"); Write-Host "    FAIL  $Message" }
}

function Assert-Throws {
    param([Parameter(Mandatory)] [scriptblock] $Action, [Parameter(Mandatory)] [string] $Pattern, [Parameter(Mandatory)] [string] $Message)
    $thrown = $null
    try { $null = & $Action } catch { $thrown = $_.Exception.Message }
    Assert-True ($null -ne $thrown -and $thrown -match $Pattern) "$Message (threw: '$thrown')"
}

function Invoke-Git {
    # Plain $args: git's own -a and -m must not prefix-match a PowerShell parameter.
    $Path = $args[0]
    $Rest = @($args | Select-Object -Skip 1 | ForEach-Object { "$_" })
    $output = & git -C $Path @Rest 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Rest -join ' ') failed in ${Path}: $output" }
    ($output | Out-String).Trim()
}

function New-Directory { param([string] $Name) $path = Join-Path $script:root $Name; $null = New-Item -ItemType Directory -Path $path -Force; $path }

function Write-File { param([string] $Root, [string] $Relative, [string] $Text) $path = Join-Path $Root $Relative; $null = New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force; [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false)) }

$goodNotes = @"
# TigerCli $version

TigerCli $version is a synthetic release used by the release tooling tests. It describes enough
user-facing change to pass the release notes gate, which wants real prose rather than a stub.

## Highlights

- Added a synthetic capability that the tests describe in a full sentence so the gate counts it.
- Improved another synthetic capability with a second, equally descriptive sentence of text.

## Packages

- ItTiger.Core, ItTiger.TigerCli and ItTiger.TigerCli.PngSink $version.
"@

function Write-ReleaseTree {
    param([string] $Root, [string] $Version)
    Write-File $Root 'Version.props' "<Project><PropertyGroup><Version>$Version</Version><AssemblyVersion>$Version.0</AssemblyVersion><FileVersion>$Version.0</FileVersion></PropertyGroup></Project>"
    foreach ($relative in (Get-TigerCliReleaseFacts).VersionReferenceFiles) { Write-File $Root $relative "Install:`n`n    dotnet add package ItTiger.TigerCli --version $Version`n" }
    Write-File $Root '.github/workflows/publish-packages.yml' "name: Release TigerCli`non:`n  workflow_dispatch:`n    inputs:`n      version:`n        required: true`n        default: `"$Version`"`n"
    Write-File $Root ".github/release-notes/$Version.md" $goodNotes
    Write-File $Root 'LICENSE' "MIT License`n`nCopyright (c) IT Tiger`n"
}

function New-Repository {
    <# A bare origin and a clone with the release tree committed on main, and dev pushed from it. #>
    param([string] $Name)
    $bare = New-Directory "$Name.git"
    Invoke-Git $bare init --quiet --bare --initial-branch=main | Out-Null
    $clone = Join-Path $script:root $Name
    & git clone --quiet $bare $clone 2>&1 | Out-Null
    Invoke-Git $clone config core.autocrlf false | Out-Null
    Write-ReleaseTree $clone $version
    Invoke-Git $clone add -A | Out-Null
    Invoke-Git $clone commit --quiet -m 'base' | Out-Null
    Invoke-Git $clone push --quiet origin main | Out-Null
    Invoke-Git $clone branch dev | Out-Null
    Invoke-Git $clone push --quiet origin dev | Out-Null
    [pscustomobject]@{ bare = $bare; clone = $clone }
}

function Add-Commit { param([string] $Clone, [string] $File, [string] $Text) Write-File $Clone $File $Text; Invoke-Git $Clone add -A | Out-Null; Invoke-Git $Clone commit --quiet -m "change $File" | Out-Null; Invoke-Git $Clone rev-parse HEAD }

function New-Zip {
    param([string] $Path, [hashtable] $Entries)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Create)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($name in @($Entries.Keys | Sort-Object)) {
                $writer = [IO.StreamWriter]::new($zip.CreateEntry($name).Open())
                try { $writer.Write($Entries[$name]) } finally { $writer.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $stream.Dispose() }
}

function New-FakePackageSet {
    <# The six package files of a release, stamped with the commit; -Build makes another build of them. #>
    param([string] $Directory, [string] $Version, [string] $Commit, [string] $Build = 'first')
    $null = New-Item -ItemType Directory -Path $Directory -Force
    foreach ($package in (Get-TigerCliReleaseFacts).Packages) {
        $nuspec = "<?xml version=`"1.0`"?><package xmlns=`"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd`"><metadata><id>$($package.Id)</id><version>$Version</version><repository type=`"git`" url=`"https://github.com/rkozlowski/TigerCli`" commit=`"$Commit`" /></metadata></package>"
        New-Zip (Join-Path $Directory "$($package.Id).$Version.nupkg") @{ "$($package.Id).nuspec" = $nuspec; "lib/net10.0/$($package.Id).dll" = "assembly of the $Build build" }
        New-Zip (Join-Path $Directory "$($package.Id).$Version.snupkg") @{ "$($package.Id).nuspec" = $nuspec; "lib/net10.0/$($package.Id).pdb" = "symbols of the $Build build" }
    }
}

function Add-Signature {
    <# A feed's copy: the package with a repository signature entry added, as nuget.org serves it. #>
    param([string] $Source, [string] $Destination)
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    $zip = [IO.Compression.ZipFile]::Open($Destination, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $writer = [IO.StreamWriter]::new($zip.CreateEntry('.signature.p7s').Open())
        try { $writer.Write('signature') } finally { $writer.Dispose() }
    }
    finally { $zip.Dispose() }
}

function New-ReleaseSet {
    <# A closed release set for the repository's HEAD: fake packages, the committed LICENSE, the record. #>
    param([string] $Clone, [string] $Directory, [string] $Build = 'first')
    $commit = Invoke-Git $Clone rev-parse HEAD
    New-FakePackageSet -Directory $Directory -Version $version -Commit $commit -Build $Build
    $null = Copy-TigerCliReleaseLicense -RepositoryRoot $Clone -Directory $Directory -CommitSha $commit
    $sha = Write-TigerCliReleaseRecord -Directory $Directory -Version $version -CommitSha $commit
    [pscustomobject]@{ directory = $Directory; commit = $commit; record = $sha }
}

# ----- stand-ins for the feeds, dotnet and gh -----

$global:TcrFeed = @{}          # download URL -> file the feed serves
$global:TcrHttpStatus = @{}    # download URL -> forced status code
$global:TcrPushes = [Collections.Generic.List[string]]::new()
$global:TcrPushConflicts = [Collections.Generic.List[string]]::new()
$global:TcrReleases = [Collections.Generic.List[object]]::new()
$global:TcrAssetFiles = @{}
$global:TcrFeedStore = New-Directory 'feed-store'

function Reset-Stand-Ins {
    $global:TcrFeed = @{}; $global:TcrHttpStatus = @{}; $global:TcrPushes.Clear(); $global:TcrPushConflicts.Clear(); $global:TcrReleases.Clear(); $global:TcrAssetFiles = @{}
}

Set-TigerCliReleaseHttp {
    param($Uri, $OutFile, $Headers)
    if ($global:TcrHttpStatus.ContainsKey($Uri)) { return $global:TcrHttpStatus[$Uri] }
    if ($global:TcrFeed.ContainsKey($Uri)) { Copy-Item -LiteralPath $global:TcrFeed[$Uri] -Destination $OutFile; return 200 }
    if ($global:TcrAssetFiles.ContainsKey($Uri)) { Copy-Item -LiteralPath $global:TcrAssetFiles[$Uri] -Destination $OutFile; return 200 }
    404
}

Set-TigerCliReleaseDotNet {
    $arguments = @($args | ForEach-Object { "$_" })
    if ($arguments[0] -ceq 'nuget' -and $arguments[1] -ceq 'push') {
        $file = $arguments[2]
        $source = $arguments[$arguments.IndexOf('--source') + 1]
        $feedRecord = @((Get-TigerCliReleaseFacts).Feeds | Where-Object PushSource -CEQ $source)[0]
        $global:TcrPushes.Add("$($feedRecord.Key) $(Split-Path -Leaf $file)$(if ($arguments -contains '--no-symbols') { ' --no-symbols' })$(if ($arguments -contains '--skip-duplicate') { ' --skip-duplicate' })")
        if ($global:TcrPushConflicts -contains "$($feedRecord.Key) $(Split-Path -Leaf $file)") { $global:LASTEXITCODE = 1; return 'error: Response status code does not indicate success: 409 (Conflict).' }
        if ($file -like '*.nupkg') {
            $name = Split-Path -Leaf $file
            $id = $name.Substring(0, $name.Length - ".$version.nupkg".Length)
            $stored = Join-Path $global:TcrFeedStore "$($feedRecord.Key)-$name"
            if ($feedRecord.Key -ceq 'nugetorg') { Add-Signature $file $stored } else { Copy-Item -LiteralPath $file -Destination $stored -Force }
            $global:TcrFeed[(Get-TigerCliFeedPackageUrl -Feed $feedRecord -PackageId $id -Version $version)] = $stored
        }
        $global:LASTEXITCODE = 0
        return 'Your package was pushed.'
    }
    $global:LASTEXITCODE = 0
    ''
}

Set-TigerCliReleaseGitHubCli {
    $arguments = @($args | ForEach-Object { "$_" })
    $global:LASTEXITCODE = 0
    if ($arguments[0] -ceq 'auth' -and $arguments[1] -ceq 'token') { return 'stand-in-token' }
    if ($arguments[0] -ceq 'api' -and $arguments[1] -like 'repos/*/releases*') { return (ConvertTo-Json -InputObject @($global:TcrReleases) -Depth 6) }
    if ($arguments[0] -ceq 'release' -and $arguments[1] -ceq 'create') {
        $files = @($arguments | Select-Object -Skip ($arguments.IndexOf('--notes-file') + 2))
        $release = [pscustomobject]@{ tag_name = $arguments[2]; name = $arguments[$arguments.IndexOf('--title') + 1]; draft = $true; html_url = "https://github.invalid/$($arguments[2])"; assets = [Collections.Generic.List[object]]::new() }
        foreach ($file in $files) { Add-ReleaseAsset $release $file }
        $global:TcrReleases.Add($release)
        return $release.html_url
    }
    if ($arguments[0] -ceq 'release' -and $arguments[1] -ceq 'upload') {
        Add-ReleaseAsset @($global:TcrReleases | Where-Object tag_name -CEQ $arguments[2])[0] $arguments[3]
        return ''
    }
    $global:LASTEXITCODE = 1
    "unexpected gh $($arguments -join ' ')"
}

function Add-ReleaseAsset {
    param($Release, [string] $File)
    $name = Split-Path -Leaf $File
    $stored = Join-Path $global:TcrFeedStore "asset-$($Release.tag_name)-$name"
    Copy-Item -LiteralPath $File -Destination $stored -Force
    $url = "https://api.github.invalid/assets/$($Release.tag_name)/$name"
    $global:TcrAssetFiles[$url] = $stored
    $Release.assets.Add([pscustomobject]@{ name = $name; size = (Get-Item -LiteralPath $stored).Length; digest = "sha256:$(Get-TigerCliFileSha256 $stored)"; url = $url })
}

function Enter-Workflow { param([string] $Commit) $env:GITHUB_ACTIONS = 'true'; $env:GITHUB_WORKFLOW = 'Release TigerCli'; $env:GITHUB_SHA = $Commit; $env:GITHUB_REF = 'refs/heads/main'; $env:GITHUB_PACKAGES_TOKEN = 'gh-token'; $env:NUGET_API_KEY = 'nuget-key' }
function Exit-Workflow { foreach ($name in $workflowVariables) { [Environment]::SetEnvironmentVariable($name, $null) } }

function Invoke-Script {
    <# Runs a release script in this session, so the stand-ins apply. Returns its exit code. #>
    param([string] $Name, [hashtable] $Parameters)
    $global:LASTEXITCODE = 0
    try { $null = & (Join-Path $PSScriptRoot $Name) @Parameters *>$null }
    catch { Write-Host "    ($Name threw: $($_.Exception.Message))"; return -1 }
    $global:LASTEXITCODE
}

try {
    Write-Host 'Release tooling tests'

    # ----- gates -----
    Start-Scenario 'gates'
    $tree = New-Directory 'gates'
    Write-ReleaseTree $tree $version
    Assert-True ((Test-TigerCliSourceVersionMatches $tree $version).status -ceq 'PASS') 'Version.props at the version passes'
    Assert-True ((Test-TigerCliSourceVersionMatches $tree '1.2.4').status -ceq 'FAIL') 'another version fails'
    Assert-True ((Test-TigerCliSourceVersionMatches $tree '1.2').status -ceq 'FAIL') 'a two-part version fails'
    Write-File $tree 'Version.props' "<Project><PropertyGroup><Version>$version</Version><AssemblyVersion>1.0.0.0</AssemblyVersion><FileVersion>$version.0</FileVersion></PropertyGroup></Project>"
    Assert-True ((Test-TigerCliSourceVersionMatches $tree $version).status -ceq 'FAIL') 'a stale AssemblyVersion fails'
    Assert-True ((Test-TigerCliVersionReference $tree $version).status -ceq 'PASS') 'install examples at the version pass'
    Write-File $tree 'docs/reference/png-sink.md' "dotnet add package ItTiger.TigerCli.PngSink --version 1.2.2`n"
    Assert-True ((Test-TigerCliVersionReference $tree $version).status -ceq 'FAIL') 'a stale install example fails'
    Write-File $tree 'docs/reference/png-sink.md' "no example here`n"
    Assert-True ((Test-TigerCliVersionReference $tree $version).status -ceq 'FAIL') 'a file without its install example fails'
    Assert-True ((Test-TigerCliWorkflowDefaultVersion $tree $version).status -ceq 'PASS') 'the workflow offering the version passes'
    Assert-True ((Test-TigerCliWorkflowDefaultVersion $tree '9.9.9').status -ceq 'FAIL') 'the workflow offering another version fails'
    Write-File $tree '.github/workflows/publish-packages.yml' "on:`n  workflow_dispatch:`n    inputs:`n      other:`n        default: `"$version`"`n"
    Assert-True ((Test-TigerCliWorkflowDefaultVersion $tree $version).status -ceq 'FAIL') "another input's default does not count"
    Assert-True ((Test-TigerCliWorkflowDefaultVersion $repoRoot ([string] (Get-TigerCliSourceVersion $repoRoot).Version)).status -ceq 'PASS') "this repository's workflow offers its Version.props version"
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'PASS') 'good notes pass'
    Write-File $tree ".github/release-notes/$version.md" ($goodNotes -replace "`r?`n", "`r`n")
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'PASS') 'good notes with CRLF line endings (a Windows checkout of *.md) pass'
    Assert-True ((Test-TigerCliReleaseNotes $tree '7.7.7').status -ceq 'FAIL') 'missing notes fail'
    $notesPath = Join-Path $tree ".github/release-notes/$version.md"
    [IO.File]::WriteAllBytes($notesPath, [byte[]](0xEF, 0xBB, 0xBF) + [Text.Encoding]::UTF8.GetBytes($goodNotes))
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'FAIL') 'notes with a BOM fail'
    Write-File $tree ".github/release-notes/$version.md" ($goodNotes -replace "# TigerCli $version", '# TigerCli 0.0.1')
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'FAIL') "notes headed with another version fail"
    Write-File $tree ".github/release-notes/$version.md" "# TigerCli $version`n`n## One`n`nshort`n"
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'FAIL') 'thin notes fail'
    Write-File $tree ".github/release-notes/$version.md" ($goodNotes + "`nTODO: finish`n")
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'FAIL') 'placeholder notes fail'
    Write-File $tree ".github/release-notes/$version.md" ($goodNotes + "`nBuilt in C:\Users\someone\src.`n")
    Assert-True ((Test-TigerCliReleaseNotes $tree $version).status -ceq 'FAIL') 'notes leaking a local path fail'

    # ----- workflow -----
    Start-Scenario 'workflow'
    $workflow = Get-Content -LiteralPath (Join-Path $repoRoot '.github/workflows/publish-packages.yml') -Raw
    $called = @([regex]::Matches($workflow, '\./eng/release/(?<name>[\w-]+\.ps1)') | ForEach-Object { $_.Groups['name'].Value } | Select-Object -Unique)
    Assert-True ($called.Count -eq 4) "the workflow calls four release scripts ($($called -join ', '))"
    foreach ($name in $called) {
        Assert-True (Test-Path -LiteralPath (Join-Path $PSScriptRoot $name) -PathType Leaf) "$name exists"
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -File)) {
        $ignored = & git -C $repoRoot check-ignore --quiet -- "eng/release/$($file.Name)" 2>$null; $wasIgnored = $LASTEXITCODE -eq 0; $global:LASTEXITCODE = 0
        Assert-True (-not $wasIgnored) "eng/release/$($file.Name) is not ignored by .gitignore"
    }
    $scriptRuns = @([regex]::Matches($workflow, '(?m)^\s+\./eng/release/\S+\.ps1.*\r?\n\s+exit \$LASTEXITCODE\s*$')).Count
    Assert-True ($scriptRuns -eq 4) "each script call ends with exit `$LASTEXITCODE ($scriptRuns of 4)"
    $proven = @('actions/checkout@v6', 'actions/setup-dotnet@v5', 'actions/upload-artifact@v7', 'actions/download-artifact@v7', 'NuGet/login@v1')
    $uses = @([regex]::Matches($workflow, 'uses:\s*(?<action>\S+)') | ForEach-Object { $_.Groups['action'].Value } | Select-Object -Unique)
    foreach ($action in $uses) { Assert-True ($action -cin $proven) "action $action is one proven by an earlier release run" }
    Assert-True ($workflow -match '(?ms)^  publish:.*?environment: release') 'the publishing job runs in the release environment'
    Assert-True ($workflow -notmatch 'skip-duplicate') 'nothing publishes with --skip-duplicate'
    Assert-True ($workflow -notmatch '\$\{\{\s*inputs\.version\s*\}\}[^\n]*\.ps1') 'the version never reaches a script through expression interpolation'

    # ----- record -----
    Start-Scenario 'record'
    $repo = New-Repository 'record'
    $set = New-ReleaseSet -Clone $repo.clone -Directory (New-Directory 'record-set')
    $record = Assert-TigerCliReleaseRecord -Directory $set.directory -Version $version -CommitSha $set.commit -ExpectedRecordSha256 $set.record
    Assert-True (@($record.artifacts).Count -eq 7) 'the record names six package files and the licence'
    Assert-True ((@($record.artifacts | ForEach-Object kind) -join ',') -ceq 'NuGetPackage,NuGetSymbols,NuGetPackage,NuGetSymbols,NuGetPackage,NuGetSymbols,License') 'the kinds are in dependency order, the licence last'
    Assert-Throws { Assert-TigerCliReleaseRecord -Directory $set.directory -Version $version -ExpectedRecordSha256 ('0' * 64) } 'in transit' 'a record that changed in transit is refused'
    Assert-Throws { Assert-TigerCliReleaseRecord -Directory $set.directory -Version '1.2.4' } 'not schema 1, version 1.2.4' 'another version is refused'
    Assert-Throws { Assert-TigerCliReleaseRecord -Directory $set.directory -Version $version -CommitSha ('a' * 40) } 'names commit' 'another commit is refused'
    $extra = Join-Path $set.directory 'notes.txt'; Set-Content -LiteralPath $extra -Value 'x'
    Assert-Throws { Assert-TigerCliReleaseRecord -Directory $set.directory -Version $version } 'unexpected: notes.txt' 'a foreign file is refused'
    Remove-Item -LiteralPath $extra
    $snupkg = Join-Path $set.directory "ItTiger.Core.$version.snupkg"; $saved = Join-Path $script:root 'saved.snupkg'; Move-Item -LiteralPath $snupkg -Destination $saved
    Assert-Throws { Assert-TigerCliReleaseRecord -Directory $set.directory -Version $version } "Missing: ItTiger.Core.$version.snupkg" 'a missing file is refused'
    Move-Item -LiteralPath $saved -Destination $snupkg
    [IO.File]::AppendAllText((Join-Path $set.directory 'LICENSE'), ' ')
    Assert-Throws { Assert-TigerCliReleaseRecord -Directory $set.directory -Version $version } 'LICENSE is not the bytes' 'a changed byte is refused'
    Assert-Throws { Write-TigerCliReleaseRecord -Directory (New-Directory 'empty-set') -Version $version -CommitSha $set.commit } 'not the closed payload set' 'an incomplete set is not recorded'

    # ----- license -----
    Start-Scenario 'license'
    $crlf = Join-Path $script:root 'license-crlf'
    & git clone --quiet --config core.autocrlf=true $repo.bare $crlf 2>&1 | Out-Null
    Assert-True ([IO.File]::ReadAllText((Join-Path $crlf 'LICENSE')).Contains("`r`n")) 'the CRLF checkout has CRLF line endings'
    $frozen = New-Directory 'license-frozen'
    $license = Copy-TigerCliReleaseLicense -RepositoryRoot $crlf -Directory $frozen -CommitSha $set.commit
    Assert-True (-not [IO.File]::ReadAllText((Join-Path $frozen 'LICENSE')).Contains("`r")) 'the frozen licence is the LF blob, not the checkout'
    Assert-True ($license.blob -ceq (Invoke-Git $repo.clone rev-parse "$($set.commit):LICENSE")) 'the frozen licence names the committed blob'
    $null = Assert-TigerCliReleaseLicense -RepositoryRoot $crlf -Directory $frozen -CommitSha $set.commit
    [IO.File]::AppendAllText((Join-Path $frozen 'LICENSE'), 'x')
    Assert-Throws { Assert-TigerCliReleaseLicense -RepositoryRoot $crlf -Directory $frozen -CommitSha $set.commit } 'not .*the bytes commit' 'a changed licence is refused'

    # ----- promotion -----
    Start-Scenario 'promotion'
    $repo = New-Repository 'promotion'
    $c = $repo.clone
    $base = Invoke-Git $c rev-parse HEAD
    Invoke-Git $c checkout --quiet dev | Out-Null
    $devTip = Add-Commit $c 'feature.txt' 'feature'
    Invoke-Git $c push --quiet origin dev | Out-Null
    Assert-True ((Test-TigerCliPromotion $c $devTip).status -ceq 'PASS') 'a commit on dev itself passes'
    Assert-True ((Test-TigerCliCommitOnMain $c $devTip).status -ceq 'BLOCKED') 'before promotion the commit is not on main: BLOCKED'
    Invoke-Git $c checkout --quiet main | Out-Null
    Invoke-Git $c merge --quiet --no-ff dev -m 'Promote dev' | Out-Null
    $merge = Invoke-Git $c rev-parse HEAD
    Invoke-Git $c push --quiet origin main | Out-Null
    Assert-True ((Test-TigerCliPromotion $c $merge).status -ceq 'PASS') 'a history-preserving merge of dev passes'
    Assert-True ((Test-TigerCliCommitOnMain $c $merge).status -ceq 'PASS') 'the merge is on main'
    Invoke-Git $c checkout --quiet -B squash $base | Out-Null
    Invoke-Git $c merge --quiet --squash dev | Out-Null
    Invoke-Git $c commit --quiet -m 'Squash dev' | Out-Null
    Assert-True ((Test-TigerCliPromotion $c (Invoke-Git $c rev-parse HEAD)).status -ceq 'FAIL') 'a squash merge fails'
    Invoke-Git $c checkout --quiet -B rebased $base | Out-Null
    Invoke-Git $c cherry-pick --quiet $devTip | Out-Null
    Assert-True ((Test-TigerCliPromotion $c (Invoke-Git $c rev-parse HEAD)).status -ceq 'FAIL') 'a rebased or cherry-picked copy of dev fails'
    Invoke-Git $c checkout --quiet -B hotfix $base | Out-Null
    $null = Add-Commit $c 'hotfix.txt' 'main only'
    Invoke-Git $c merge --quiet --no-ff dev -m 'Promote dev over a main-only change' | Out-Null
    Assert-True ((Test-TigerCliPromotion $c (Invoke-Git $c rev-parse HEAD)).status -ceq 'FAIL') 'a merge whose tree has a change dev never accepted fails'
    Assert-True ($null -eq (Get-TigerCliRemoteTagCommit $c 'v9.9.9')) 'an absent tag is absent'
    Invoke-Git $c tag -a v9.9.9 $merge -m 'annotated' | Out-Null; Invoke-Git $c push --quiet origin v9.9.9 | Out-Null
    Assert-True ((Get-TigerCliRemoteTagCommit $c 'v9.9.9') -ceq $merge) 'an annotated tag resolves to its commit'
    $tagInfo = Get-TigerCliRemoteTag $c 'v9.9.9'
    Assert-True ($tagInfo.annotated -and $tagInfo.tagger -ceq 'Release Test' -and $tagInfo.message -ceq 'annotated') 'the remote tag reports annotation, tagger and message'

    # ----- prerequisites -----
    Start-Scenario 'prerequisites'
    Reset-Stand-Ins
    $repo = New-Repository 'prerequisites'
    $c = $repo.clone
    Invoke-Git $c checkout --quiet dev | Out-Null
    $devTip = Add-Commit $c 'feature.txt' 'feature'
    Invoke-Git $c push --quiet origin dev | Out-Null
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c }) -eq 2) 'before promotion: BLOCKED (exit 2)'
    Invoke-Git $c checkout --quiet main | Out-Null
    Invoke-Git $c merge --quiet --no-ff dev -m 'Promote dev' | Out-Null
    $merge = Invoke-Git $c rev-parse HEAD
    Invoke-Git $c push --quiet origin main | Out-Null
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c }) -eq 0) 'the promoted commit: PASS (exit 0)'
    Enter-Workflow $merge
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c; CommitSha = $merge }) -eq 0) 'in the workflow, dispatched on main: PASS'
    $env:GITHUB_REF = 'refs/heads/dev'
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c; CommitSha = $merge }) -eq 1) 'in the workflow, dispatched on dev: FAIL'
    Exit-Workflow
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = '1.2.4'; RepositoryRoot = $c }) -eq 1) 'another version: FAIL'
    $nugetOrg = @((Get-TigerCliReleaseFacts).Feeds | Where-Object Key -CEQ 'nugetorg')[0]
    $global:TcrFeed[(Get-TigerCliFeedPackageUrl -Feed $nugetOrg -PackageId 'ItTiger.Core' -Version $version)] = (Join-Path $c 'LICENSE')
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c }) -eq 1) 'a version already on nuget.org: FAIL'
    Reset-Stand-Ins
    $global:TcrHttpStatus[(Get-TigerCliFeedPackageUrl -Feed $nugetOrg -PackageId 'ItTiger.TigerCli' -Version $version)] = 503
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c }) -eq 2) 'an unreadable nuget.org: BLOCKED'
    Reset-Stand-Ins
    Invoke-Git $c tag -a "v$version" $merge -m 'existing' | Out-Null; Invoke-Git $c push --quiet origin "v$version" | Out-Null
    Assert-True ((Invoke-Script 'Assert-ReleaseCommitReady.ps1' @{ Version = $version; RepositoryRoot = $c }) -eq 1) 'an existing tag: FAIL'

    # ----- identity -----
    Start-Scenario 'identity'
    $first = New-Directory 'identity-first'; $second = New-Directory 'identity-second'
    New-FakePackageSet -Directory $first -Version $version -Commit ('a' * 40)
    New-FakePackageSet -Directory $second -Version $version -Commit ('a' * 40) -Build 'second'
    $recorded = Join-Path $first "ItTiger.Core.$version.nupkg"
    $signed = Join-Path $script:root 'signed.nupkg'; Add-Signature $recorded $signed
    Assert-True ((Compare-TigerCliPackageContent $recorded $signed) -ceq '') "a feed's signed copy is the recorded package"
    Assert-True ((Compare-TigerCliPackageContent $recorded (Join-Path $second "ItTiger.Core.$version.nupkg")) -match 'different lib/net10.0/ItTiger.Core.dll') 'another build is not'
    $extraEntry = Join-Path $script:root 'extra.nupkg'; Copy-Item $signed $extraEntry
    $zip = [IO.Compression.ZipFile]::Open($extraEntry, [IO.Compression.ZipArchiveMode]::Update); try { $null = $zip.CreateEntry('content/extra.txt') } finally { $zip.Dispose() }
    Assert-True ((Compare-TigerCliPackageContent $recorded $extraEntry) -match 'unexpected content/extra.txt') 'an extra entry is not'
    $missingEntry = Join-Path $script:root 'missing.nupkg'; Copy-Item $recorded $missingEntry
    $zip = [IO.Compression.ZipFile]::Open($missingEntry, [IO.Compression.ZipArchiveMode]::Update); try { $zip.GetEntry("lib/net10.0/ItTiger.Core.dll").Delete() } finally { $zip.Dispose() }
    Assert-True ((Compare-TigerCliPackageContent $recorded $missingEntry) -match 'missing lib/net10.0/ItTiger.Core.dll') 'a missing entry is not'

    # ----- publish -----
    Start-Scenario 'publish'
    Reset-Stand-Ins
    $repo = New-Repository 'publish'
    $set = New-ReleaseSet -Clone $repo.clone -Directory (New-Directory 'publish-set')
    $publish = @{ Version = $version; ArtifactDirectory = $set.directory; CommitSha = $set.commit; ExpectedRecordSha256 = $set.record }
    Assert-Throws { & (Join-Path $PSScriptRoot 'Publish-Packages.ps1') @publish *>$null } 'Only the .Release TigerCli. workflow run' 'outside the workflow it refuses to push'
    Assert-True ($global:TcrPushes.Count -eq 0) 'nothing was pushed outside the workflow'
    Enter-Workflow $set.commit
    Assert-True ((Invoke-Script 'Publish-Packages.ps1' $publish) -eq 0) 'the first run publishes: PASS'
    $expected = @(
        "github ItTiger.Core.$version.nupkg --no-symbols", "github ItTiger.TigerCli.$version.nupkg --no-symbols", "github ItTiger.TigerCli.PngSink.$version.nupkg --no-symbols",
        "nugetorg ItTiger.Core.$version.nupkg --no-symbols", "nugetorg ItTiger.Core.$version.snupkg",
        "nugetorg ItTiger.TigerCli.$version.nupkg --no-symbols", "nugetorg ItTiger.TigerCli.$version.snupkg",
        "nugetorg ItTiger.TigerCli.PngSink.$version.nupkg --no-symbols", "nugetorg ItTiger.TigerCli.PngSink.$version.snupkg")
    Assert-True (($global:TcrPushes -join '|') -ceq ($expected -join '|')) "GitHub Packages first, then nuget.org with symbols, in dependency order, never --skip-duplicate (pushed: $($global:TcrPushes -join '; '))"

    # A rerun of the same run after nuget.org took only Core: GitHub Packages and Core are verified, not pushed.
    $kept = @{}
    foreach ($url in @($global:TcrFeed.Keys)) { if ($url -like '*nuget.pkg.github.com*' -or $url -like '*/ittiger.core/*') { $kept[$url] = $global:TcrFeed[$url] } }
    $global:TcrFeed = $kept
    $global:TcrPushes.Clear(); $global:TcrPushConflicts.Add("nugetorg ItTiger.Core.$version.snupkg")
    Assert-True ((Invoke-Script 'Publish-Packages.ps1' $publish) -eq 0) 'a rerun after partial publication completes it: PASS'
    $expected = @("nugetorg ItTiger.Core.$version.snupkg",
        "nugetorg ItTiger.TigerCli.$version.nupkg --no-symbols", "nugetorg ItTiger.TigerCli.$version.snupkg",
        "nugetorg ItTiger.TigerCli.PngSink.$version.nupkg --no-symbols", "nugetorg ItTiger.TigerCli.PngSink.$version.snupkg")
    Assert-True (($global:TcrPushes -join '|') -ceq ($expected -join '|')) "only what is missing is pushed; symbols already there are accepted (pushed: $($global:TcrPushes -join '; '))"

    # Another build already on a feed: nothing more is pushed anywhere.
    $other = New-Directory 'publish-other'
    New-FakePackageSet -Directory $other -Version $version -Commit $set.commit -Build 'other'
    $global:TcrFeed[(Get-TigerCliFeedPackageUrl -Feed $nugetOrg -PackageId 'ItTiger.TigerCli.PngSink' -Version $version)] = (Join-Path $other "ItTiger.TigerCli.PngSink.$version.nupkg")
    $global:TcrPushes.Clear()
    Assert-True ((Invoke-Script 'Publish-Packages.ps1' $publish) -eq 1) 'another build of the version on a feed: FAIL'
    Assert-True ($global:TcrPushes.Count -eq 0) 'and nothing is pushed'

    Reset-Stand-Ins
    $github = @((Get-TigerCliReleaseFacts).Feeds | Where-Object Key -CEQ 'github')[0]
    $global:TcrHttpStatus[(Get-TigerCliFeedPackageUrl -Feed $github -PackageId 'ItTiger.TigerCli' -Version $version)] = 401
    Assert-True ((Invoke-Script 'Publish-Packages.ps1' $publish) -eq 2) 'an unreadable feed: BLOCKED'
    Assert-True ($global:TcrPushes.Count -eq 0) 'and nothing is pushed'

    Reset-Stand-Ins
    $global:TcrPushConflicts.Add("nugetorg ItTiger.TigerCli.$version.nupkg")
    Assert-True ((Invoke-Script 'Publish-Packages.ps1' $publish) -eq 2) 'a feed that refuses an absent version (indexing lag): BLOCKED'
    Assert-True (@($global:TcrPushes | Where-Object { $_ -like 'nugetorg ItTiger.TigerCli.PngSink*' -or $_ -like "nugetorg ItTiger.TigerCli.$version.snupkg" }).Count -eq 0) 'and nothing after it is pushed'
    Exit-Workflow

    # ----- draft -----
    Start-Scenario 'draft'
    Reset-Stand-Ins
    $repo = New-Repository 'draft'
    $set = New-ReleaseSet -Clone $repo.clone -Directory (New-Directory 'draft-set')
    $draft = @{ Version = $version; ArtifactDirectory = $set.directory; CommitSha = $set.commit; ExpectedRecordSha256 = $set.record; RepositoryRoot = $repo.clone; RunUrl = 'https://github.invalid/run/1' }
    Assert-Throws { & (Join-Path $PSScriptRoot 'Publish-DraftRelease.ps1') @draft *>$null } 'Only the .Release TigerCli. workflow run' 'outside the workflow it refuses to tag'
    Enter-Workflow $set.commit
    Assert-True ((Invoke-Script 'Publish-DraftRelease.ps1' $draft) -eq 0) 'the first run tags and drafts: PASS'
    $tagInfo = Get-TigerCliRemoteTag $repo.clone "v$version"
    Assert-True ($null -ne $tagInfo -and $tagInfo.annotated -and $tagInfo.tagger -ceq 'github-actions[bot]' -and $tagInfo.commit -ceq $set.commit) 'the tag is annotated, by github-actions[bot], at the commit'
    Assert-True ($tagInfo.message -match "(?m)^release-artifacts\.json sha256 $($set.record)$" -and $tagInfo.message -match 'built by https://github.invalid/run/1') 'the tag names the record and the run'
    Assert-True ($global:TcrReleases.Count -eq 1 -and $global:TcrReleases[0].draft -and $global:TcrReleases[0].name -ceq "TigerCli $version" -and $global:TcrReleases[0].assets.Count -eq 9) 'one draft with the nine files'
    $global:TcrReleases[0].assets.RemoveAt(8)
    Assert-True ((Invoke-Script 'Publish-DraftRelease.ps1' $draft) -eq 0) 'a rerun over a compatible draft: PASS'
    Assert-True ($global:TcrReleases.Count -eq 1 -and $global:TcrReleases[0].assets.Count -eq 9) 'it uploads only the missing file'
    $global:TcrReleases[0].assets.Add([pscustomobject]@{ name = 'extra.zip'; size = 1; digest = 'sha256:00'; url = 'x' })
    Assert-Throws { & (Join-Path $PSScriptRoot 'Publish-DraftRelease.ps1') @draft *>$null } 'not this release' 'a foreign asset is refused'
    $global:TcrReleases[0].assets.RemoveAt(9)
    $global:TcrReleases[0].draft = $false
    Assert-Throws { & (Join-Path $PSScriptRoot 'Publish-DraftRelease.ps1') @draft *>$null } 'is not the draft' 'a published release is refused'
    $global:TcrReleases[0].draft = $true
    Exit-Workflow

    # ----- validation -----
    Start-Scenario 'validation'
    $global:TcrFeed = @{}
    foreach ($feedRecord in (Get-TigerCliReleaseFacts).Feeds) {
        foreach ($package in (Get-TigerCliReleaseFacts).Packages) {
            $stored = Join-Path $global:TcrFeedStore "validation-$($feedRecord.Key)-$($package.Id).nupkg"
            Add-Signature (Join-Path $set.directory "$($package.Id).$version.nupkg") $stored
            $global:TcrFeed[(Get-TigerCliFeedPackageUrl -Feed $feedRecord -PackageId $package.Id -Version $version)] = $stored
        }
    }
    $validation = @{ Version = $version; RepositoryRoot = $repo.clone; OutputDirectory = (Join-Path $script:root 'validation-out') }
    Assert-True ((Invoke-Script 'Get-ReleaseArtifacts.ps1' $validation) -eq 0) 'the drafted set with the feeds serving it: READY'
    $global:TcrReleases[0].assets[0].digest = 'sha256:' + ('0' * 64)
    Assert-True ((Invoke-Script 'Get-ReleaseArtifacts.ps1' $validation) -eq 1) "a byte that is not GitHub's digest: FAIL"
}
finally {
    Set-TigerCliReleaseHttp $null
    Set-TigerCliReleaseDotNet $null
    Set-TigerCliReleaseGitHubCli $null
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
    Remove-Variable -Name TcrFeed, TcrHttpStatus, TcrPushes, TcrPushConflicts, TcrReleases, TcrAssetFiles, TcrFeedStore -Scope Global -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $script:root -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
if ($script:failures.Count) {
    Write-Host "Release tooling tests: $($script:failures.Count) of $($script:checks) checks FAILED"
    foreach ($failure in $script:failures) { Write-Host "  FAIL $failure" }
    exit 1
}
Write-Host "Release tooling tests: all $($script:checks) checks passed"
exit 0
