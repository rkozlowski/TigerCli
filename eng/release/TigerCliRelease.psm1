<#
    .SYNOPSIS
    TigerCli's release facts and the checks every release stage shares.

    .DESCRIPTION
    The Tiger release model (TigerAiCore `docs/release-model.md`) defines the
    lifecycle; this module holds what TigerCli releases and how its stages
    prove each other: the version source, the three NuGet packages with their
    symbol packages, the licence frozen with them, the release notes, the
    closed artifact record (`release-artifacts.json` schema 1 and
    `SHA256SUMS.txt`), the commit, promotion and tag gates, the package feeds,
    and the GitHub Release lookup. `docs/contributing/releases.md` describes
    the lifecycle as TigerCli runs it.

    Every function runs the same way on a developer machine and on a GitHub
    Actions runner. GitHub is reached through `gh` and `git`, the package
    feeds through HTTPS; the tests replace `gh`, `dotnet` and HTTP with
    Set-TigerCliReleaseGitHubCli, Set-TigerCliReleaseDotNet and
    Set-TigerCliReleaseHttp.
#>

Set-StrictMode -Version Latest

$script:Facts = [pscustomobject][ordered]@{
    Product = 'TigerCli'
    Repository = 'rkozlowski/TigerCli'
    DefaultBranch = 'main'
    DevelopmentBranch = 'dev'
    ReleaseWorkflowName = 'Release TigerCli'
    ReleaseWorkflowPath = '.github/workflows/publish-packages.yml'
    TagPrefix = 'v'
    ReleaseNotesDirectory = '.github/release-notes'
    VersionSource = 'Version.props'
    # Files whose `dotnet add package ItTiger.* --version <v>` lines state the current version.
    VersionReferenceFiles = @('README.md', 'ItTiger.Core/README.md', 'ItTiger.TigerCli/README.md', 'ItTiger.TigerCli.PngSink/README.md', 'docs/reference/png-sink.md')
    License = [pscustomobject]@{ name = 'LICENSE'; kind = 'License'; source = 'LICENSE' }
    # Dependency order: each package depends on the one before it at the release version.
    Packages = @(
        [pscustomobject]@{ Id = 'ItTiger.Core'; Project = 'ItTiger.Core/ItTiger.Core.csproj'; Icon = 'ItTiger-head-128.png'; DependsOn = '' }
        [pscustomobject]@{ Id = 'ItTiger.TigerCli'; Project = 'ItTiger.TigerCli/ItTiger.TigerCli.csproj'; Icon = 'TigerCli.png'; DependsOn = 'ItTiger.Core' }
        [pscustomobject]@{ Id = 'ItTiger.TigerCli.PngSink'; Project = 'ItTiger.TigerCli.PngSink/ItTiger.TigerCli.PngSink.csproj'; Icon = 'TigerCli.png'; DependsOn = 'ItTiger.TigerCli' }
    )
    # Publication order: the withdrawable GitHub Packages feed first, nuget.org (irreversible) last.
    Feeds = @(
        [pscustomobject]@{ Name = 'GitHub Packages'; Key = 'github'; PushSource = 'https://nuget.pkg.github.com/rkozlowski/index.json'; DownloadBase = 'https://nuget.pkg.github.com/rkozlowski/download'; Symbols = $false; TokenVariable = 'GITHUB_PACKAGES_TOKEN'; AuthenticatedRead = $true }
        [pscustomobject]@{ Name = 'nuget.org'; Key = 'nugetorg'; PushSource = 'https://api.nuget.org/v3/index.json'; DownloadBase = 'https://api.nuget.org/v3-flatcontainer'; Symbols = $true; TokenVariable = 'NUGET_API_KEY'; AuthenticatedRead = $false }
    )
    ProjectUrl = 'https://www.ittiger.net/projects/tigercli/'
    RepositoryUrl = 'https://github.com/rkozlowski/TigerCli'
    # A feed's copy of a package carries this extra entry: the repository signature nuget.org adds.
    SignatureEntry = '.signature.p7s'
}

$script:GitHubCli = $null
$script:DotNetCli = $null
$script:Http = $null

function Get-TigerCliReleaseFacts {
    <#
        .SYNOPSIS
        What TigerCli releases and where: the project's release data.
    #>
    [CmdletBinding()]
    param()
    $script:Facts
}

function Get-TigerCliReleaseAsset {
    <#
        .SYNOPSIS
        The payloads of one release, in record order: each package and its
        symbol package in dependency order, then the licence frozen with them.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Version)

    foreach ($package in $script:Facts.Packages) {
        [pscustomobject]@{ name = "$($package.Id).$Version.nupkg"; kind = 'NuGetPackage'; source = ''; packageId = $package.Id }
        [pscustomobject]@{ name = "$($package.Id).$Version.snupkg"; kind = 'NuGetSymbols'; source = ''; packageId = $package.Id }
    }
    [pscustomobject]@{ name = $script:Facts.License.name; kind = $script:Facts.License.kind; source = $script:Facts.License.source; packageId = '' }
}

function Get-TigerCliReleaseAssetName {
    <#
        .SYNOPSIS
        Every file a release carries: the payloads and the two records.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Version)
    @((Get-TigerCliReleaseAsset -Version $Version).name) + @('SHA256SUMS.txt', 'release-artifacts.json')
}

function Get-TigerCliReleaseTag {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Version)
    $script:Facts.TagPrefix + $Version
}

function Test-TigerCliReleaseVersion {
    <#
        .SYNOPSIS
        True for a release version: exactly <Major>.<Minor>.<Patch>.
    #>
    [CmdletBinding()]
    param([AllowEmptyString()] [string] $Version)
    $Version -cmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
}

function Get-TigerCliSourceVersion {
    <#
        .SYNOPSIS
        The versions Version.props records: Version, AssemblyVersion and FileVersion.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $RepositoryRoot)

    $path = Join-Path $RepositoryRoot $script:Facts.VersionSource
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "$($script:Facts.VersionSource) does not exist in '$RepositoryRoot'." }
    $xml = [xml] (Get-Content -LiteralPath $path -Raw)
    $values = @{}
    foreach ($name in 'Version', 'AssemblyVersion', 'FileVersion') {
        $node = $xml.SelectSingleNode("/Project/PropertyGroup/$name")
        $values[$name] = if ($null -ne $node) { $node.InnerText.Trim() } else { '' }
    }
    [pscustomobject]$values
}

function New-TigerCliReleaseCheck {
    <#
        .SYNOPSIS
        One gate result. PASS proceeds; BLOCKED means a human or external step
        has not happened yet (wait, then rerun); FAIL means the data is wrong;
        NOT RUN means the check could not run here, and its observation says
        where it runs instead. NOT RUN is never counted as PASS.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Id,
        [Parameter(Mandatory)] [ValidateSet('PASS', 'BLOCKED', 'FAIL', 'NOT RUN')] [string] $Status,
        [Parameter(Mandatory)] [string] $Observed,
        [string] $Remediation = ''
    )
    [pscustomobject][ordered]@{ id = $Id; status = $Status; observed = $Observed; remediation = $Remediation }
}

function Write-TigerCliReleaseReport {
    <#
        .SYNOPSIS
        Prints a gate's checks, appends them to the workflow summary when there
        is one, and returns the exit code: 0 PASS, 2 BLOCKED, 1 FAIL. NOT RUN
        checks are named in the verdict ("PASS, n NOT RUN"), never hidden.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Title,
        [Parameter(Mandatory)] [object[]] $Checks,
        [string[]] $Next = @(),
        [string] $StepSummaryPath = $env:GITHUB_STEP_SUMMARY
    )

    $status = if (@($Checks | Where-Object status -CEQ 'FAIL').Count) { 'FAIL' }
    elseif (@($Checks | Where-Object status -CEQ 'BLOCKED').Count) { 'BLOCKED' }
    else { 'PASS' }
    $notRun = @($Checks | Where-Object status -CEQ 'NOT RUN').Count
    $verdict = if ($status -ceq 'PASS' -and $notRun) { "PASS, $notRun NOT RUN" } else { $status }

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("$Title - $verdict")
    foreach ($check in $Checks) {
        $lines.Add(('  {0,-8} {1}: {2}' -f $check.status, $check.id, $check.observed))
        if ($check.status -cne 'PASS' -and $check.remediation) { $lines.Add("           -> $($check.remediation)") }
    }
    if ($status -ceq 'PASS') { foreach ($line in $Next) { $lines.Add("  NEXT     $line") } }
    foreach ($line in $lines) { Write-Host $line }

    if (-not [string]::IsNullOrWhiteSpace($StepSummaryPath)) {
        $markdown = @("### $Title - $verdict", '', '```text') + $lines.ToArray() + @('```', '')
        $markdown | Out-File -LiteralPath $StepSummaryPath -Encoding utf8 -Append
    }
    switch ($status) { 'PASS' { 0 } 'BLOCKED' { 2 } default { 1 } }
}

function Test-TigerCliSourceVersionMatches {
    <#
        .SYNOPSIS
        Checks that the requested version is a release version and is exactly
        what Version.props records. Returns a check.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $Version
    )
    $source = Get-TigerCliSourceVersion -RepositoryRoot $RepositoryRoot
    $observed = "Version.props records Version '$($source.Version)', AssemblyVersion '$($source.AssemblyVersion)', FileVersion '$($source.FileVersion)'."
    if ((Test-TigerCliReleaseVersion $Version) -and $source.Version -ceq $Version -and
        $source.AssemblyVersion -ceq "$Version.0" -and $source.FileVersion -ceq "$Version.0") {
        return New-TigerCliReleaseCheck -Id 'version' -Status PASS -Observed $observed
    }
    New-TigerCliReleaseCheck -Id 'version' -Status FAIL -Observed "Requested '$Version'. $observed" `
        -Remediation 'Request the exact <Major>.<Minor>.<Patch> version the release commit records, with AssemblyVersion and FileVersion <version>.0.'
}

function Test-TigerCliReleaseNotes {
    <#
        .SYNOPSIS
        Checks `.github/release-notes/<version>.md`, the release's checked-in
        user-facing notes. Returns a check.

        .DESCRIPTION
        The draft GitHub Release is created from this file, so the gate that
        runs before any build refuses a missing, empty, placeholder or leaky
        one. The first heading names the version, so a copied file cannot
        carry the previous release's title.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Version
    )

    $product = $script:Facts.Product
    $relative = "$($script:Facts.ReleaseNotesDirectory)/$Version.md"
    $path = Join-Path $RepositoryRoot $relative
    $repair = "Write ${relative}: a '# $product $Version' heading and at least two '##' sections of real, user-facing content."
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return New-TigerCliReleaseCheck -Id 'notes' -Status FAIL -Observed "$relative does not exist." -Remediation $repair
    }
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return New-TigerCliReleaseCheck -Id 'notes' -Status FAIL -Observed "$relative starts with a byte-order mark." -Remediation 'Save it as UTF-8 without a BOM.'
    }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $firstHeading = [regex]::Match($text, '(?m)^#\s+(?<title>.+?)\s*$')
    if (-not $firstHeading.Success -or $firstHeading.Groups['title'].Value -cne "$product $Version") {
        return New-TigerCliReleaseCheck -Id 'notes' -Status FAIL -Observed "$relative does not open with '# $product $Version'." -Remediation $repair
    }
    $sections = [regex]::Matches($text, '(?m)^##\s+\S').Count
    $prose = ($text -replace '(?m)^\s*#.*$', '' -replace '\[([^\]]*)\]\([^)]*\)', '$1' -replace '\s+', ' ').Trim()
    if ($sections -lt 2 -or $prose.Length -lt 200) {
        return New-TigerCliReleaseCheck -Id 'notes' -Status FAIL -Observed "$relative has $sections sections and $($prose.Length) characters of prose." -Remediation $repair
    }
    $placeholder = [regex]::Match($text, '(?i)\b(TODO|TBD|FIXME|lorem ipsum)\b|<(describe|summari[sz]e|fill|add)[^>]*>|xxx+')
    if ($placeholder.Success) {
        return New-TigerCliReleaseCheck -Id 'notes' -Status FAIL -Observed "$relative still has placeholder text '$($placeholder.Value)'." -Remediation $repair
    }
    $leak = [regex]::Match($text, '(?i)[A-Z]:\\(Users|Projects)\\|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY-----')
    if ($leak.Success) {
        return New-TigerCliReleaseCheck -Id 'notes' -Status FAIL -Observed "$relative contains a local path or a secret: '$($leak.Value)'." -Remediation 'Remove it.'
    }
    New-TigerCliReleaseCheck -Id 'notes' -Status PASS -Observed "${relative}: $sections sections, $($prose.Length) characters."
}

function Test-TigerCliVersionReference {
    <#
        .SYNOPSIS
        Checks the current-version install examples: every
        `dotnet add package ItTiger.* --version <v>` line in the files the
        project names states the release version. Returns a check.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Version
    )
    $problems = [Collections.Generic.List[string]]::new()
    $count = 0
    foreach ($relative in $script:Facts.VersionReferenceFiles) {
        $path = Join-Path $RepositoryRoot $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $problems.Add("$relative does not exist"); continue }
        $found = @([regex]::Matches((Get-Content -LiteralPath $path -Raw), 'dotnet add package (?<id>ItTiger\.[\w.]+) --version (?<version>\S+)'))
        if ($found.Count -eq 0) { $problems.Add("$relative has no install example"); continue }
        foreach ($match in $found) {
            $count++
            if ($match.Groups['version'].Value -cne $Version) { $problems.Add("$relative installs $($match.Groups['id'].Value) $($match.Groups['version'].Value)") }
        }
    }
    if ($problems.Count) {
        return New-TigerCliReleaseCheck -Id 'references' -Status FAIL -Observed ($problems -join '; ') -Remediation "Update every current-version install example to $Version."
    }
    New-TigerCliReleaseCheck -Id 'references' -Status PASS -Observed "$count install examples in $($script:Facts.VersionReferenceFiles.Count) files state $Version."
}

function Test-TigerCliWorkflowDefaultVersion {
    <#
        .SYNOPSIS
        Checks the version the release workflow's form is prefilled with: the
        `default` of its `workflow_dispatch` `version` input. Returns a check.

        .DESCRIPTION
        The Architect starts a release by confirming the version the form
        already shows, so the release commit's workflow must offer exactly the
        version Version.props records. Only the key
        `on` > `workflow_dispatch` > `inputs` > `version` > `default` counts,
        found by indentation; no YAML parser is involved.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Version
    )

    $relative = $script:Facts.ReleaseWorkflowPath
    $path = Join-Path $RepositoryRoot $relative
    $repair = "Set the default of the version input under workflow_dispatch in $relative to '$Version'."
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return New-TigerCliReleaseCheck -Id 'workflow' -Status FAIL -Observed "$relative does not exist." -Remediation $repair
    }
    $keys = [Collections.Generic.List[object]]::new()
    $offered = $null
    foreach ($line in Get-Content -LiteralPath $path) {
        $entry = [regex]::Match($line, '^(?<indent> *)(?<key>[A-Za-z_][\w-]*):(\s+(?<value>.*?))?\s*$')
        if (-not $entry.Success) { continue }
        $indent = $entry.Groups['indent'].Length
        while ($keys.Count -and $keys[$keys.Count - 1].indent -ge $indent) { $keys.RemoveAt($keys.Count - 1) }
        $keys.Add([pscustomobject]@{ indent = $indent; key = $entry.Groups['key'].Value })
        if (($keys.key -join '/') -ceq 'on/workflow_dispatch/inputs/version/default') {
            $offered = [regex]::Match($entry.Groups['value'].Value, '^(''(?<v>[^'']*)''|"(?<v>[^"]*)"|(?<v>[^\s#]+))').Groups['v'].Value
        }
    }
    if ($offered -ceq $Version) {
        return New-TigerCliReleaseCheck -Id 'workflow' -Status PASS -Observed "$relative offers version $Version."
    }
    New-TigerCliReleaseCheck -Id 'workflow' -Status FAIL -Observed "$relative offers $(if ($null -eq $offered) { 'no default version' } else { "version '$offered'" }), not $Version." -Remediation $repair
}

function Get-TigerCliFileSha256 {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Path)
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-TigerCliReleaseRecord {
    <#
        .SYNOPSIS
        Closes a release directory: writes release-artifacts.json and
        SHA256SUMS.txt over exactly the release's payload files, and returns
        the SHA-256 of release-artifacts.json.

        .DESCRIPTION
        The record is the Tiger release model's schema 1: the version, the
        commit the payloads were built from, and each payload's name, kind,
        length and SHA-256. A directory holding anything else, or missing a
        payload, is refused, so the record always describes a closed set.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha
    )

    $assets = @(Get-TigerCliReleaseAsset -Version $Version)
    $present = @(Get-ChildItem -LiteralPath $Directory -File | ForEach-Object Name)
    $missing = @($assets.name | Where-Object { $_ -cnotin $present })
    $extra = @($present | Where-Object { $_ -cnotin $assets.name })
    if ($missing.Count -or $extra.Count) {
        throw "The release directory is not the closed payload set. Missing: $($missing -join ', '); unexpected: $($extra -join ', ')."
    }

    $artifacts = @(foreach ($asset in $assets) {
            $path = Join-Path $Directory $asset.name
            [ordered]@{
                name = $asset.name
                kind = $asset.kind
                length = (Get-Item -LiteralPath $path).Length
                sha256 = Get-TigerCliFileSha256 -Path $path
            }
        })
    $recordPath = Join-Path $Directory 'release-artifacts.json'
    [ordered]@{
        schemaVersion = 1
        releaseVersion = $Version
        sourceCommit = $CommitSha.ToLowerInvariant()
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        artifacts = $artifacts
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $recordPath -Encoding utf8NoBOM
    @($artifacts | ForEach-Object { "$($_.sha256)  $($_.name)" }) |
        Set-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS.txt') -Encoding utf8NoBOM
    Get-TigerCliFileSha256 -Path $recordPath
}

function Assert-TigerCliReleaseRecord {
    <#
        .SYNOPSIS
        Proves a directory holds exactly the bytes its release record names,
        for the expected version and commit, and returns the record.

        .DESCRIPTION
        -ExpectedRecordSha256 is the transfer check: the build reports the
        record's own hash and every later stage that receives the directory
        repeats it, so a record changed in transit cannot vouch for itself.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Version,
        [ValidatePattern('^([0-9a-fA-F]{40})?$')] [string] $CommitSha = '',
        [ValidatePattern('^([0-9a-fA-F]{64})?$')] [string] $ExpectedRecordSha256 = ''
    )

    $recordPath = Join-Path $Directory 'release-artifacts.json'
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw "$recordPath does not exist." }
    if ($ExpectedRecordSha256) {
        $actual = Get-TigerCliFileSha256 -Path $recordPath
        if ($actual -cne $ExpectedRecordSha256.ToLowerInvariant()) {
            throw "release-artifacts.json hashes to $actual, not the recorded $($ExpectedRecordSha256.ToLowerInvariant()); it changed in transit."
        }
    }
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    if ($record.schemaVersion -ne 1 -or [string] $record.releaseVersion -cne $Version) {
        throw "release-artifacts.json describes schema $($record.schemaVersion), version '$($record.releaseVersion)', not schema 1, version $Version."
    }
    if ([string] $record.sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'release-artifacts.json names no source commit.' }
    if ($CommitSha -and [string] $record.sourceCommit -cne $CommitSha.ToLowerInvariant()) {
        throw "release-artifacts.json names commit $($record.sourceCommit), not $($CommitSha.ToLowerInvariant())."
    }

    $assets = @(Get-TigerCliReleaseAsset -Version $Version)
    $entries = @($record.artifacts)
    $recorded = @($entries | ForEach-Object { "$($_.name)|$($_.kind)" })
    $expected = @($assets | ForEach-Object { "$($_.name)|$($_.kind)" })
    if (($recorded -join ';') -cne ($expected -join ';')) {
        throw "release-artifacts.json records [$($recorded -join ', ')], not the release set [$($expected -join ', ')]."
    }

    $present = @(Get-ChildItem -LiteralPath $Directory -File | ForEach-Object Name)
    $allowed = @(Get-TigerCliReleaseAssetName -Version $Version)
    $missing = @($allowed | Where-Object { $_ -cnotin $present })
    $extra = @($present | Where-Object { $_ -cnotin $allowed })
    if ($missing.Count -or $extra.Count) {
        throw "The release directory is not the closed set. Missing: $($missing -join ', '); unexpected: $($extra -join ', ')."
    }
    foreach ($entry in $entries) {
        $path = Join-Path $Directory $entry.name
        if ((Get-Item -LiteralPath $path).Length -ne [long] $entry.length -or (Get-TigerCliFileSha256 -Path $path) -cne [string] $entry.sha256) {
            throw "$($entry.name) is not the bytes release-artifacts.json records."
        }
    }
    $sums = @($entries | ForEach-Object { "$($_.sha256)  $($_.name)" }) -join "`n"
    $actualSums = ((Get-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS.txt') -Raw) -replace "`r`n", "`n").TrimEnd("`n")
    if ($actualSums -cne $sums) { throw 'SHA256SUMS.txt does not match release-artifacts.json.' }
    $record
}

function Export-TigerCliGitBlob {
    <#
        .SYNOPSIS
        Writes a Git blob's exact bytes to a file: `git cat-file blob`, with no
        text conversion on the way, which a PowerShell pipeline would apply.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Object,
        [Parameter(Mandatory)] [string] $Destination
    )
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    foreach ($argument in @('-C', $RepositoryRoot, 'cat-file', 'blob', $Object)) { $start.ArgumentList.Add($argument) }
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    if (Test-Path -LiteralPath $Destination) { throw "$Destination already exists." }
    $process = [Diagnostics.Process]::Start($start)
    $written = $false
    try {
        $errorText = $process.StandardError.ReadToEndAsync()
        $file = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew)
        try { $process.StandardOutput.BaseStream.CopyTo($file) } finally { $file.Dispose() }
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "git cat-file blob $Object failed ($($process.ExitCode)): $($errorText.Result.Trim())" }
        $written = $true
    }
    finally {
        # A failed export leaves no partial file and no running git behind.
        if (-not $written) {
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        }
        $process.Dispose()
    }
}

function Copy-TigerCliReleaseLicense {
    <#
        .SYNOPSIS
        Freezes the release's licence into its release directory: the exact
        bytes Git holds for LICENSE at the release commit. Returns its blob
        and SHA-256.

        .DESCRIPTION
        The committed blob does not depend on how a checkout converts line
        endings, so anyone can reproduce it (`git cat-file blob
        <commit>:LICENSE`). -WorkingTree is the rehearsal's: before the release
        commit exists, the licence is the blob Git would commit for the
        working-tree file (`git hash-object -w`).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha,
        [switch] $WorkingTree
    )
    $license = $script:Facts.License
    if ($WorkingTree) {
        if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot $license.source) -PathType Leaf)) { throw "The working tree has no $($license.source)." }
        $blob = Invoke-TigerCliGit $RepositoryRoot @('hash-object', '-w', '--', $license.source)
        if (-not $blob.ok) { throw "git hash-object $($license.source) failed: $($blob.output)" }
    }
    else {
        $blob = Invoke-TigerCliGit $RepositoryRoot @('rev-parse', '--verify', '--quiet', "$($CommitSha.ToLowerInvariant()):$($license.source)")
        if (-not $blob.ok) { throw "Commit $CommitSha has no $($license.source), the release's $($license.kind)." }
    }
    $path = Join-Path $Directory $license.name
    Export-TigerCliGitBlob -RepositoryRoot $RepositoryRoot -Object $blob.output -Destination $path
    [pscustomobject][ordered]@{ name = $license.name; blob = $blob.output; sha256 = Get-TigerCliFileSha256 -Path $path }
}

function Assert-TigerCliReleaseLicense {
    <#
        .SYNOPSIS
        Proves a release directory's LICENSE is the exact bytes the commit
        holds for it in Git.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha
    )
    $scratch = Join-Path ([IO.Path]::GetTempPath()) "tigercli-license-$([guid]::NewGuid().ToString('N'))"
    $null = New-Item -ItemType Directory -Path $scratch
    try {
        $expected = Copy-TigerCliReleaseLicense -RepositoryRoot $RepositoryRoot -Directory $scratch -CommitSha $CommitSha
        $path = Join-Path $Directory $expected.name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "The release set has no $($expected.name)." }
        $actual = Get-TigerCliFileSha256 -Path $path
        if ($actual -cne $expected.sha256) { throw "$($expected.name) hashes to $actual, not $($expected.sha256), the bytes commit $CommitSha holds for it." }
        $expected
    }
    finally { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
}

function Get-TigerCliPackageEntry {
    <#
        .SYNOPSIS
        A package's entries as name -> SHA-256 of the uncompressed content,
        without the repository signature a feed adds.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $entries = [ordered]@{}
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        foreach ($entry in @($zip.Entries | Sort-Object FullName)) {
            if ($entry.FullName -ceq $script:Facts.SignatureEntry) { continue }
            $stream = $entry.Open()
            try { $hash = [Security.Cryptography.SHA256]::HashData($stream) } finally { $stream.Dispose() }
            $entries[$entry.FullName] = [Convert]::ToHexString($hash).ToLowerInvariant()
        }
    }
    finally { $zip.Dispose() }
    $entries
}

function Compare-TigerCliPackageContent {
    <#
        .SYNOPSIS
        Compares a feed's copy of a package with the recorded package. Returns
        an empty string when they are the same package, or what differs.

        .DESCRIPTION
        nuget.org serves a repository-signed copy: the recorded package's
        entries, byte for byte, plus `.signature.p7s`, so its file hash never
        equals the record's. The channel copy is identified by its content
        instead: every entry but the signature must be present with the same
        bytes, and nothing else. A rebuild of the same commit does not pass:
        its assemblies carry their own build time (Version.props stamps it
        into InformationalVersion), so the content differs.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Expected,
        [Parameter(Mandatory)] [string] $Actual
    )
    $wanted = Get-TigerCliPackageEntry -Path $Expected
    $found = Get-TigerCliPackageEntry -Path $Actual
    $problems = [Collections.Generic.List[string]]::new()
    foreach ($name in $wanted.Keys) {
        if (-not $found.Contains($name)) { $problems.Add("missing $name") }
        elseif ($found[$name] -cne $wanted[$name]) { $problems.Add("different $name") }
    }
    foreach ($name in $found.Keys) { if (-not $wanted.Contains($name)) { $problems.Add("unexpected $name") } }
    $problems -join ', '
}

function Read-TigerCliNuspec {
    <#
        .SYNOPSIS
        A package's nuspec as XML.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.FullName -like '*.nuspec' })
        if ($entries.Count -ne 1) { throw "$Path has $($entries.Count) root nuspec files." }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { [xml] $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

function Test-TigerCliPackage {
    <#
        .SYNOPSIS
        Inspects one built package and its symbol package. Returns a list of
        problems; empty when the package is what the release needs.

        .DESCRIPTION
        Identity, version, the release commit in the repository metadata,
        licence, project URL, README and icon at the root, the net10.0
        assembly and its XML documentation, the dependency on the previous
        package at exactly this version, a portable PDB in the symbol
        package, and for PngSink its assets, font licences and embedded fonts.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $PackageId,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha
    )
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $package = @($script:Facts.Packages | Where-Object Id -CEQ $PackageId)[0]
    $problems = [Collections.Generic.List[string]]::new()
    $path = Join-Path $Directory "$PackageId.$Version.nupkg"
    $symbols = Join-Path $Directory "$PackageId.$Version.snupkg"
    foreach ($file in $path, $symbols) { if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { $problems.Add("missing $(Split-Path -Leaf $file)") } }
    if ($problems.Count) { return @($problems) }

    $metadata = (Read-TigerCliNuspec -Path $path).package.metadata
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $names = @($zip.Entries | ForEach-Object FullName)
        if ([string] $metadata.id -cne $PackageId) { $problems.Add("id '$($metadata.id)'") }
        if ([string] $metadata.version -cne $Version) { $problems.Add("version '$($metadata.version)'") }
        $repository = $metadata.repository
        if ($null -eq $repository -or $repository.GetAttribute('type') -cne 'git' -or $repository.GetAttribute('url') -cne $script:Facts.RepositoryUrl) {
            $problems.Add('repository metadata is not the TigerCli git repository')
        }
        elseif ($repository.GetAttribute('commit') -cne $CommitSha.ToLowerInvariant()) {
            $problems.Add("repository commit '$($repository.GetAttribute('commit'))', not the release commit")
        }
        if ($null -eq $metadata.license -or $metadata.license.GetAttribute('type') -cne 'expression' -or [string] $metadata.license.InnerText -cne 'MIT') { $problems.Add('licence is not the MIT expression') }
        if ([string] $metadata.projectUrl -cne $script:Facts.ProjectUrl) { $problems.Add("project URL '$($metadata.projectUrl)'") }
        if ([string]::IsNullOrWhiteSpace([string] $metadata.authors) -or [string]::IsNullOrWhiteSpace([string] $metadata.description)) { $problems.Add('authors or description missing') }
        if ([string] $metadata.readme -cne 'README.md' -or 'README.md' -cnotin $names) { $problems.Add('README.md missing at the package root') }
        if ([string] $metadata.icon -cne $package.Icon -or $package.Icon -cnotin $names) { $problems.Add("icon $($package.Icon) missing at the package root") }
        foreach ($entry in "lib/net10.0/$PackageId.dll", "lib/net10.0/$PackageId.xml") { if ($entry -cnotin $names) { $problems.Add("missing $entry") } }

        $dependencies = @($metadata.SelectNodes("//*[local-name()='dependency']") | Where-Object { $_.GetAttribute('id') -like 'ItTiger.*' })
        if ($package.DependsOn) {
            $match = @($dependencies | Where-Object { $_.GetAttribute('id') -ceq $package.DependsOn })
            if ($match.Count -ne 1 -or $match[0].GetAttribute('version') -cne $Version) { $problems.Add("dependency on $($package.DependsOn) is not exactly $Version") }
            if ($dependencies.Count -ne 1) { $problems.Add("$($dependencies.Count) ItTiger dependencies, expected 1") }
        }
        elseif ($dependencies.Count) { $problems.Add("unexpected ItTiger dependencies") }

        if ($PackageId -ceq 'ItTiger.TigerCli.PngSink') {
            foreach ($entry in 'assets/tc_term_ico.png', 'assets/tc_window_symbols.png', 'fonts/OFL.txt', 'fonts/CASCADIA-LICENSE.txt', 'fonts/SOURCE.md') {
                if ($entry -cnotin $names) { $problems.Add("missing $entry") }
            }
            $stream = [IO.MemoryStream]::new()
            try {
                $assemblyStream = $zip.GetEntry("lib/net10.0/$PackageId.dll").Open()
                try { $assemblyStream.CopyTo($stream) } finally { $assemblyStream.Dispose() }
                # Read the resource table without loading the assembly into this process.
                $peReader = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($stream.ToArray()))
                try {
                    $mdReader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)
                    $resources = @($mdReader.ManifestResources | ForEach-Object { $mdReader.GetString($mdReader.GetManifestResource($_).Name) })
                }
                finally { $peReader.Dispose() }
            }
            finally { $stream.Dispose() }
            foreach ($font in 'CascadiaMono-Bold', 'CascadiaMono-BoldItalic', 'CascadiaMono-Italic', 'CascadiaMono-Regular', 'NotoSans-Bold', 'NotoSans-BoldItalic', 'NotoSans-Italic', 'NotoSans-Regular', 'NotoSansSymbols2-Regular') {
                if ("ItTiger.TigerCli.PngSink.Fonts.$font.ttf" -cnotin $resources) { $problems.Add("font $font.ttf not embedded") }
            }
        }
    }
    finally { $zip.Dispose() }

    $symbolZip = [IO.Compression.ZipFile]::OpenRead($symbols)
    try {
        if (-not @($symbolZip.Entries | Where-Object FullName -CEQ "lib/net10.0/$PackageId.pdb").Count) { $problems.Add("symbol package has no lib/net10.0/$PackageId.pdb") }
        $symbolMetadata = (Read-TigerCliNuspec -Path $symbols).package.metadata
        if ([string] $symbolMetadata.id -cne $PackageId -or [string] $symbolMetadata.version -cne $Version) { $problems.Add('symbol package identity differs') }
    }
    finally { $symbolZip.Dispose() }
    @($problems)
}

function Invoke-TigerCliPackageSmoke {
    <#
        .SYNOPSIS
        Proves a directory's packages are usable from a NuGet feed: each
        restores with every ItTiger.* dependency from that directory, a
        minimal command app runs, shell completion works from the packaged
        library, and PngSink renders a PNG. Returns the checks; throws on a
        failure.

        .DESCRIPTION
        The local feed is the only source for ItTiger.*; third-party
        dependencies come from nuget.org. Restores use a private
        NUGET_PACKAGES folder, so neither an earlier candidate in the user's
        global packages folder nor this run's packages leak between runs.
        Builds run without build servers, so nothing is left running.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $PackageDirectory,
        [Parameter(Mandatory)] [string] $Version,
        [string] $WorkRoot = (Join-Path ([IO.Path]::GetTempPath()) "tigercli-smoke-$([guid]::NewGuid().ToString('N'))")
    )
    $feed = Join-Path $WorkRoot 'feed'
    $null = New-Item -ItemType Directory -Path $feed -Force
    foreach ($package in $script:Facts.Packages) { Copy-Item -LiteralPath (Join-Path $PackageDirectory "$($package.Id).$Version.nupkg") -Destination $feed }
    $config = Join-Path $WorkRoot 'NuGet.Config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-release" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-release">
      <package pattern="ItTiger.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $config -Encoding utf8NoBOM

    $programs = [ordered]@{
        CoreSmoke = @{ Package = 'ItTiger.Core'; Expect = @('ItTiger.Core'); Source = 'System.Console.WriteLine(typeof(ItTiger.Core.TigerTextAttribute).Name);' }
        TigerCliSmoke = @{ Package = 'ItTiger.TigerCli'; Expect = @('ItTiger.TigerCli', 'ItTiger.Core'); Source = @'
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

return await TigerCliApp.CreateBuilder()
    .AddDefaultCommand(static () =>
    {
        TigerConsole.MarkupLine("[Success]TigerCli package smoke test[/]");
        return TigerCliExitKind.Success;
    })
    .Build()
    .RunAsync(args);
'@ }
        CompletionSmoke = @{ Package = 'ItTiger.TigerCli'; Expect = @('ItTiger.TigerCli', 'ItTiger.Core'); Source = @'
using ItTiger.TigerCli.Commands;
using ItTiger.TigerCli.Terminal;

return await TigerCliApp.CreateBuilder()
    .SetApplicationName("completion-smoke")
    .UseShellCompletion()
    .AddCommand("deploy", static () =>
    {
        TigerConsole.MarkupLine("deployed");
        return TigerCliExitKind.Success;
    }, "Deploys a target.")
    .Build()
    .RunAsync(args);
'@ }
        PngSinkSmoke = @{ Package = 'ItTiger.TigerCli.PngSink'; Expect = @('ItTiger.TigerCli.PngSink', 'ItTiger.TigerCli', 'ItTiger.Core'); Source = @'
using ItTiger.TigerCli.PngSink;
using ItTiger.TigerCli.Rendering;

var grid = new CliGrid(1, 1);
grid.Set(0, 0, "TigerCli PNG smoke test");
PngRenderer.RenderGridToFile(grid, args[0], new PngSinkOptions { Columns = 30, Rows = 2 });
'@ }
    }

    $savedPackages = $env:NUGET_PACKAGES
    $env:NUGET_PACKAGES = Join-Path $WorkRoot 'packages-cache'
    $checks = [Collections.Generic.List[object]]::new()
    try {
        function Invoke-Step([string] $What, [string[]] $Arguments) {
            $result = Invoke-TigerCliDotNet $Arguments
            if (-not $result.ok) { throw "$What failed ($($result.exitCode)):`n$($result.output)" }
            $result.output
        }
        $built = @{}
        foreach ($name in $programs.Keys) {
            $program = $programs[$name]
            $project = Join-Path $WorkRoot "$name/$name.csproj"
            $null = Invoke-Step "dotnet new ($name)" @('new', 'console', '-n', $name, '-o', (Join-Path $WorkRoot $name), '-f', 'net10.0', '--no-restore')
            $null = Invoke-Step "dotnet add package ($name)" @('add', $project, 'package', $program.Package, '--version', $Version, '--no-restore')
            Set-Content -LiteralPath (Join-Path $WorkRoot "$name/Program.cs") -Value $program.Source -Encoding utf8NoBOM
            $null = Invoke-Step "dotnet restore ($name)" @('restore', $project, '--configfile', $config, '--force', '--no-http-cache', '--disable-build-servers')
            $assets = Get-Content -LiteralPath (Join-Path $WorkRoot "$name/obj/project.assets.json") -Raw | ConvertFrom-Json
            $libraries = @($assets.libraries.PSObject.Properties.Name)
            foreach ($id in $program.Expect) {
                if ("$id/$Version" -cnotin $libraries) { throw "$name did not resolve $id $Version from the release packages." }
                $sha512 = [string] $assets.libraries."$id/$Version".sha512
                $local = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes((Join-Path $feed "$id.$Version.nupkg"))))
                if ($sha512 -cne $local) { throw "$name resolved an $id $Version that is not the release package." }
            }
            $null = Invoke-Step "dotnet build ($name)" @('build', $project, '-c', 'Release', '--no-restore', '--disable-build-servers', '--nologo', '-v', 'q')
            $built[$name] = Join-Path $WorkRoot "$name/bin/Release/net10.0/$name.dll"
            $checks.Add((New-TigerCliReleaseCheck -Id "smoke/$name" -Status PASS -Observed "restored $($program.Expect -join ', ') $Version from the release packages and built."))
        }

        $output = Invoke-Step 'TigerCliSmoke run' @($built.TigerCliSmoke, '--non-interactive')
        if ($output -notmatch 'TigerCli package smoke test') { throw "TigerCliSmoke printed '$output'." }
        $rejected = Invoke-TigerCliDotNet @($built.TigerCliSmoke, '--completion', 'pwsh')
        if ($rejected.ok) { throw 'An app without UseShellCompletion() accepted --completion.' }
        foreach ($shell in 'bash', 'zsh', 'fish', 'pwsh') {
            $script = Invoke-Step "CompletionSmoke --completion $shell" @($built.CompletionSmoke, '--completion', $shell)
            if ($script -notmatch 'completion-smoke' -or $script -notmatch '--tigercli-complete') { throw "CompletionSmoke --completion $shell printed no activation script." }
        }
        $answer = (Invoke-Step 'CompletionSmoke request' @($built.CompletionSmoke, '--tigercli-complete', 'bash', '_dep')) -replace "`r", ''
        if ($answer.TrimEnd("`n") -cne "deploy`n:none") { throw "CompletionSmoke answered '$answer'." }
        $png = Join-Path $WorkRoot 'smoke.png'
        $null = Invoke-Step 'PngSinkSmoke run' @($built.PngSinkSmoke, $png)
        $bytes = if (Test-Path -LiteralPath $png -PathType Leaf) { [IO.File]::ReadAllBytes($png) } else { @() }
        if ($bytes.Length -lt 8 -or $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47) { throw 'PngSinkSmoke did not render a PNG.' }
        $checks.Add((New-TigerCliReleaseCheck -Id 'smoke/run' -Status PASS -Observed "the command app ran, an app without completion rejected --completion, completion generated all four scripts and answered a request, and PngSink rendered a $($bytes.Length)-byte PNG."))
    }
    finally {
        $env:NUGET_PACKAGES = $savedPackages
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    $checks.ToArray()
}

function ConvertTo-TigerCliCommandText {
    [CmdletBinding()]
    param([AllowNull()] [object[]] $Output, [bool] $Failed)
    $lines = @($Output | Where-Object { $Failed -or $_ -isnot [Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    $lines -join "`n"
}

function Set-TigerCliReleaseGitHubCli {
    <#
        .SYNOPSIS
        Replaces `gh` for this session: a script block taking gh's arguments and
        returning its standard output, with $global:LASTEXITCODE set. Tests use
        it; $null restores the real gh.
    #>
    [CmdletBinding()]
    param([AllowNull()] [scriptblock] $Command)
    $script:GitHubCli = $Command
}

function Invoke-TigerCliGitHubCli {
    <#
        .SYNOPSIS
        Runs gh. Returns ok, exitCode and output (standard error added when gh failed).
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string[]] $Arguments)
    $previous = $PSNativeCommandUseErrorActionPreference
    try {
        $PSNativeCommandUseErrorActionPreference = $false
        if ($null -ne $script:GitHubCli) { $output = & $script:GitHubCli @Arguments }
        else {
            if ($null -eq (Get-Command gh -CommandType Application -ErrorAction SilentlyContinue)) {
                return [pscustomobject]@{ ok = $false; exitCode = -1; output = 'gh is not installed.' }
            }
            $output = & gh @Arguments 2>&1
        }
        $code = $global:LASTEXITCODE
        [pscustomobject]@{ ok = ($code -eq 0); exitCode = $code; output = (ConvertTo-TigerCliCommandText -Output $output -Failed ($code -ne 0)) }
    }
    finally {
        $PSNativeCommandUseErrorActionPreference = $previous
        $global:LASTEXITCODE = 0
    }
}

function Invoke-TigerCliGitHubApi {
    <#
        .SYNOPSIS
        GET a GitHub REST path through gh. Returns ok, data and error.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Path)
    $result = Invoke-TigerCliGitHubCli @('api', $Path)
    if (-not $result.ok) { return [pscustomobject]@{ ok = $false; data = $null; error = $result.output } }
    [pscustomobject]@{ ok = $true; data = ($result.output | ConvertFrom-Json); error = '' }
}

function Invoke-TigerCliGit {
    <#
        .SYNOPSIS
        Runs git in the repository; returns ok, exitCode and trimmed output.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string[]] $Arguments
    )
    $previous = $PSNativeCommandUseErrorActionPreference
    try {
        $PSNativeCommandUseErrorActionPreference = $false
        $output = & git -C $RepositoryRoot @Arguments 2>&1
        $code = $global:LASTEXITCODE
        [pscustomobject]@{ ok = ($code -eq 0); exitCode = $code; output = (ConvertTo-TigerCliCommandText -Output $output -Failed ($code -ne 0)).Trim() }
    }
    finally {
        $PSNativeCommandUseErrorActionPreference = $previous
        $global:LASTEXITCODE = 0
    }
}

function Set-TigerCliReleaseDotNet {
    <#
        .SYNOPSIS
        Replaces `dotnet` for this session, as Set-TigerCliReleaseGitHubCli
        replaces gh. Tests use it; $null restores the real dotnet.
    #>
    [CmdletBinding()]
    param([AllowNull()] [scriptblock] $Command)
    $script:DotNetCli = $Command
}

function Invoke-TigerCliDotNet {
    <#
        .SYNOPSIS
        Runs dotnet. Returns ok, exitCode and output. Arguments are never
        echoed, because a push carries an API key.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string[]] $Arguments)
    $previous = $PSNativeCommandUseErrorActionPreference
    try {
        $PSNativeCommandUseErrorActionPreference = $false
        if ($null -ne $script:DotNetCli) { $output = & $script:DotNetCli @Arguments }
        else {
            if ($null -eq (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
                return [pscustomobject]@{ ok = $false; exitCode = -1; output = 'dotnet is not installed.' }
            }
            $output = & dotnet @Arguments 2>&1
        }
        $code = $global:LASTEXITCODE
        [pscustomobject]@{ ok = ($code -eq 0); exitCode = $code; output = (ConvertTo-TigerCliCommandText -Output $output -Failed $true) }
    }
    finally {
        $PSNativeCommandUseErrorActionPreference = $previous
        $global:LASTEXITCODE = 0
    }
}

function Set-TigerCliReleaseHttp {
    <#
        .SYNOPSIS
        Replaces HTTP downloads for this session: a script block taking -Uri,
        -OutFile and -Headers and returning the status code, having written the
        file on 200. Tests use it; $null restores real HTTPS.
    #>
    [CmdletBinding()]
    param([AllowNull()] [scriptblock] $Command)
    $script:Http = $Command
}

function Invoke-TigerCliDownload {
    <#
        .SYNOPSIS
        GETs a URL into a file, straight to disk. Returns the HTTP status code;
        the file exists only when it is 200. A network failure throws. Every
        request carries a User-Agent, which GitHub's API requires (403 without).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Uri,
        [Parameter(Mandatory)] [string] $OutFile,
        [hashtable] $Headers = @{}
    )
    if ($null -ne $script:Http) { return [int] (& $script:Http -Uri $Uri -OutFile $OutFile -Headers $Headers) }
    $client = [Net.Http.HttpClient]::new()
    try {
        $client.Timeout = [TimeSpan]::FromMinutes(5)
        $client.DefaultRequestHeaders.UserAgent.ParseAdd('TigerCli-release-tooling')
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $Uri)
        foreach ($name in $Headers.Keys) { $null = $request.Headers.TryAddWithoutValidation($name, [string] $Headers[$name]) }
        $response = $client.Send($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead)
        try {
            $code = [int] $response.StatusCode
            if ($code -eq 200) {
                $file = [IO.File]::Open($OutFile, [IO.FileMode]::CreateNew)
                try { $response.Content.ReadAsStream().CopyTo($file) } finally { $file.Dispose() }
            }
            $code
        }
        finally { $response.Dispose() }
    }
    finally { $client.Dispose() }
}

function Get-TigerCliFeedPackageUrl {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Feed,
        [Parameter(Mandatory)] [string] $PackageId,
        [Parameter(Mandatory)] [string] $Version
    )
    $id = $PackageId.ToLowerInvariant()
    "$($Feed.DownloadBase)/$id/$Version/$id.$Version.nupkg"
}

function Get-TigerCliFeedPackageState {
    <#
        .SYNOPSIS
        What a feed holds for one recorded package: absent, identical (the
        same package: Compare-TigerCliPackageContent), different, or unknown
        when the feed cannot be read. Returns state and observed.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Feed,
        [Parameter(Mandatory)] [string] $PackageId,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $ExpectedPackage,
        [string] $Token = ''
    )
    $headers = @{}
    if ($Feed.AuthenticatedRead) {
        if (-not $Token) { return [pscustomobject]@{ state = 'unknown'; observed = "$($Feed.Name) needs a token to read; none is set." } }
        $user = if ($env:GITHUB_ACTOR) { $env:GITHUB_ACTOR } else { ($script:Facts.Repository -split '/')[0] }
        $headers['Authorization'] = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${user}:$Token"))
    }
    $url = Get-TigerCliFeedPackageUrl -Feed $Feed -PackageId $PackageId -Version $Version
    $file = Join-Path ([IO.Path]::GetTempPath()) "tigercli-feed-$([guid]::NewGuid().ToString('N')).nupkg"
    try {
        try { $code = Invoke-TigerCliDownload -Uri $url -OutFile $file -Headers $headers }
        catch { return [pscustomobject]@{ state = 'unknown'; observed = "$($Feed.Name) could not be reached: $($_.Exception.Message)" } }
        if ($code -eq 404) { return [pscustomobject]@{ state = 'absent'; observed = "$($Feed.Name) has no $PackageId $Version." } }
        if ($code -ne 200) { return [pscustomobject]@{ state = 'unknown'; observed = "$($Feed.Name) answered HTTP $code for $PackageId $Version." } }
        if (-not $ExpectedPackage) { return [pscustomobject]@{ state = 'present'; observed = "$($Feed.Name) has $PackageId $Version." } }
        $difference = Compare-TigerCliPackageContent -Expected $ExpectedPackage -Actual $file
        if ($difference) { return [pscustomobject]@{ state = 'different'; observed = "$($Feed.Name) has a different $PackageId $Version ($difference)." } }
        [pscustomobject]@{ state = 'identical'; observed = "$($Feed.Name) has this release's $PackageId $Version." }
    }
    finally { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
}

function Test-TigerCliCommitOnMain {
    <#
        .SYNOPSIS
        Proves the commit is reachable from origin/main, after fetching it.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha
    )
    $branch = $script:Facts.DefaultBranch
    $null = Invoke-TigerCliGit $RepositoryRoot @('fetch', '--quiet', 'origin', "+refs/heads/${branch}:refs/remotes/origin/${branch}")
    $tip = Invoke-TigerCliGit $RepositoryRoot @('rev-parse', '--verify', '--quiet', "refs/remotes/origin/$branch")
    if (-not $tip.ok) {
        return New-TigerCliReleaseCheck -Id 'commit/on-main' -Status BLOCKED -Observed "origin/$branch is unknown here." -Remediation "Fetch origin/$branch and rerun."
    }
    $ancestor = Invoke-TigerCliGit $RepositoryRoot @('merge-base', '--is-ancestor', $CommitSha, $tip.output)
    if ($ancestor.ok) {
        return New-TigerCliReleaseCheck -Id 'commit/on-main' -Status PASS -Observed "$CommitSha is reachable from origin/$branch ($($tip.output))."
    }
    New-TigerCliReleaseCheck -Id 'commit/on-main' -Status BLOCKED -Observed "$CommitSha is not reachable from origin/$branch ($($tip.output))." -Remediation "Promote dev to $branch first (a merge commit)."
}

function Test-TigerCliPromotion {
    <#
        .SYNOPSIS
        Proves the release commit is the accepted dev candidate, promoted with
        its history: the commit itself is on origin/dev, or one of its parents
        is on origin/dev and has exactly the commit's tree. Returns a check.

        .DESCRIPTION
        A history-preserving merge of dev into main produces a merge commit
        whose dev parent was accepted on dev and whose tree is that parent's
        when main carried nothing dev lacked. A squash or rebase merge leaves
        no parent on dev with that tree, and a promotion that brought in a
        commit only main had changes the tree; both fail, because the commit
        is not the candidate preparation accepted.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string] $CommitSha
    )
    $branch = $script:Facts.DevelopmentBranch
    $null = Invoke-TigerCliGit $RepositoryRoot @('fetch', '--quiet', 'origin', "+refs/heads/${branch}:refs/remotes/origin/${branch}")
    $tip = Invoke-TigerCliGit $RepositoryRoot @('rev-parse', '--verify', '--quiet', "refs/remotes/origin/$branch")
    if (-not $tip.ok) {
        return New-TigerCliReleaseCheck -Id 'commit/promotion' -Status BLOCKED -Observed "origin/$branch is unknown here." -Remediation "Fetch origin/$branch and rerun."
    }
    $onDev = { param($sha) (Invoke-TigerCliGit $RepositoryRoot @('merge-base', '--is-ancestor', $sha, $tip.output)).ok }
    if (& $onDev $CommitSha) {
        return New-TigerCliReleaseCheck -Id 'commit/promotion' -Status PASS -Observed "$CommitSha is on origin/$branch itself."
    }
    $tree = (Invoke-TigerCliGit $RepositoryRoot @('rev-parse', "$CommitSha^{tree}")).output
    $parents = @(((Invoke-TigerCliGit $RepositoryRoot @('rev-list', '--parents', '-n', '1', $CommitSha)).output -split '\s+') | Select-Object -Skip 1)
    foreach ($parent in $parents) {
        if ((& $onDev $parent) -and (Invoke-TigerCliGit $RepositoryRoot @('rev-parse', "$parent^{tree}")).output -ceq $tree) {
            return New-TigerCliReleaseCheck -Id 'commit/promotion' -Status PASS -Observed "$CommitSha merges origin/$branch's $parent and has exactly its tree."
        }
    }
    New-TigerCliReleaseCheck -Id 'commit/promotion' -Status FAIL -Observed "$CommitSha is not a history-preserving promotion of an accepted origin/$branch commit with an identical tree." `
        -Remediation "Promote with a merge commit that keeps dev's commits (no squash or rebase merge). If main carried changes dev lacked, merge main into dev, accept the result there, and promote again."
}

function Get-TigerCliRemoteTagCommit {
    <#
        .SYNOPSIS
        The commit a release tag names on origin, or $null when origin has none.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Tag
    )
    $result = Invoke-TigerCliGit $RepositoryRoot @('ls-remote', '--tags', 'origin', "refs/tags/$Tag", "refs/tags/$Tag^{}")
    if (-not $result.ok) { throw "Could not list origin's tags: $($result.output)" }
    $refs = @{}
    foreach ($line in ($result.output -split "`n" | Where-Object { $_ })) {
        $sha, $ref = $line -split "`t", 2
        $refs[$ref.Trim()] = $sha.Trim().ToLowerInvariant()
    }
    if ($refs.ContainsKey("refs/tags/$Tag^{}")) { return $refs["refs/tags/$Tag^{}"] }
    if ($refs.ContainsKey("refs/tags/$Tag")) { return $refs["refs/tags/$Tag"] }
    $null
}

function Get-TigerCliRemoteTag {
    <#
        .SYNOPSIS
        Origin's release tag as it is: commit, annotated, tagger and message;
        $null when origin has no such tag. Fetches into FETCH_HEAD only.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Tag
    )
    if ($null -eq (Get-TigerCliRemoteTagCommit -RepositoryRoot $RepositoryRoot -Tag $Tag)) { return $null }
    $fetched = Invoke-TigerCliGit $RepositoryRoot @('fetch', '--quiet', '--no-tags', 'origin', "refs/tags/$Tag")
    if (-not $fetched.ok) { throw "Could not fetch origin's $Tag`: $($fetched.output)" }
    $object = (Invoke-TigerCliGit $RepositoryRoot @('rev-parse', 'FETCH_HEAD')).output
    $commit = (Invoke-TigerCliGit $RepositoryRoot @('rev-parse', "$object^{commit}")).output.ToLowerInvariant()
    $annotated = (Invoke-TigerCliGit $RepositoryRoot @('cat-file', '-t', $object)).output -ceq 'tag'
    $tagger = ''
    $message = ''
    if ($annotated) {
        $text = (Invoke-TigerCliGit $RepositoryRoot @('cat-file', '-p', $object)).output -replace "`r`n", "`n"
        $header, $body = $text -split "`n`n", 2
        $taggerLine = @($header -split "`n" | Where-Object { $_ -like 'tagger *' })
        if ($taggerLine.Count) { $tagger = ($taggerLine[0] -replace '^tagger\s+', '' -replace '\s*<.*$', '') }
        $message = if ($null -ne $body) { $body.Trim() } else { '' }
    }
    [pscustomobject][ordered]@{ tag = $Tag; commit = $commit; annotated = $annotated; tagger = $tagger; message = $message }
}

function Get-TigerCliTagMessage {
    <#
        .SYNOPSIS
        The annotated release tag's message: the title, the SHA-256 of the
        release record the tag was made for, and the run that built it.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{64}$')] [string] $RecordSha256,
        [string] $RunUrl = ''
    )
    $lines = @("$($script:Facts.Product) $Version", '', "release-artifacts.json sha256 $($RecordSha256.ToLowerInvariant())")
    if ($RunUrl) { $lines += "built by $RunUrl" }
    $lines -join "`n"
}

function Get-TigerCliGitHubRelease {
    <#
        .SYNOPSIS
        The GitHub Release for a tag, draft or published, or $null. The by-tag
        endpoint does not return drafts, so this lists releases.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Tag)
    $response = Invoke-TigerCliGitHubApi "repos/$($script:Facts.Repository)/releases?per_page=100"
    if (-not $response.ok) { throw "Could not list the GitHub Releases: $($response.error)" }
    $matching = @($response.data | Where-Object { [string] $_.tag_name -ceq $Tag })
    if ($matching.Count -gt 1) { throw "More than one GitHub Release names tag $Tag." }
    if ($matching.Count -eq 0) { return $null }
    $matching[0]
}

function Assert-TigerCliReleaseProvenance {
    <#
        .SYNOPSIS
        Proves a retrieved release set is the one the release workflow built
        and tagged, and returns its record: origin's v<version> is an annotated
        tag by github-actions[bot] at the record's commit, naming this record's
        SHA-256.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Version
    )
    $record = Assert-TigerCliReleaseRecord -Directory $Directory -Version $Version
    $recordSha256 = Get-TigerCliFileSha256 (Join-Path $Directory 'release-artifacts.json')
    $tagName = Get-TigerCliReleaseTag -Version $Version
    $tag = Get-TigerCliRemoteTag -RepositoryRoot $RepositoryRoot -Tag $tagName
    if ($null -eq $tag) { throw "origin has no tag $tagName." }
    if (-not $tag.annotated -or $tag.tagger -cne 'github-actions[bot]') {
        throw "origin's $tagName is not the release workflow's annotated tag (annotated: $($tag.annotated), tagger: '$($tag.tagger)')."
    }
    if ($tag.commit -cne [string] $record.sourceCommit) { throw "release-artifacts.json names $($record.sourceCommit); origin's $tagName names $($tag.commit)." }
    if ($tag.message -notmatch "(?m)^release-artifacts\.json sha256 $recordSha256$") {
        throw "origin's $tagName was made for another release record; this one hashes to $recordSha256."
    }
    $record
}

function Test-TigerCliReleaseWorkflowRun {
    <#
        .SYNOPSIS
        True only inside the release workflow's run for this commit on main.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $CommitSha)
    $env:GITHUB_ACTIONS -ceq 'true' -and $env:GITHUB_WORKFLOW -ceq $script:Facts.ReleaseWorkflowName -and
    "$env:GITHUB_SHA".ToLowerInvariant() -ceq $CommitSha.ToLowerInvariant() -and $env:GITHUB_REF -ceq "refs/heads/$($script:Facts.DefaultBranch)"
}
