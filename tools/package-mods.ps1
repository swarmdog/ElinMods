param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$ElinDir = $(if ($env:ELIN_DIR) { $env:ELIN_DIR } else { "D:\Steam\steamapps\common\Elin" }),

    [string]$OutputDir = "artifacts/packages",

    [switch]$Deploy,

    [switch]$NoBuild,

    [switch]$IncludePdb
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$SolutionPath = Join-Path $RepoRoot "ElinMods.sln"
$OutputPath = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    [System.IO.Path]::GetFullPath($OutputDir)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputDir))
}
$StagingRoot = Join-Path (Split-Path $OutputPath -Parent) "staging"

function Fail([string]$Message) {
    throw "[package-mods] $Message"
}

function Warn([string]$Message) {
    Write-Warning "[package-mods] $Message"
}

function Reset-Directory([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-RequiredFile([string]$Source, [string]$Destination) {
    if (!(Test-Path -LiteralPath $Source -PathType Leaf)) {
        Fail "Missing required file: $Source"
    }

    $destinationDir = Split-Path $Destination -Parent
    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Assert-PackageXml([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "Missing package.xml: $Path"
    }

    try {
        [xml]$xml = Get-Content -LiteralPath $Path -Raw
    } catch {
        Fail "package.xml is not valid XML: $Path"
    }

    foreach ($field in @("id", "title", "version")) {
        $value = $xml.Meta.$field
        if ([string]::IsNullOrWhiteSpace($value)) {
            Fail "package.xml is missing <$field>: $Path"
        }
    }
}

function Get-PackageMeta([string]$Path) {
    Assert-PackageXml $Path
    [xml]$xml = Get-Content -LiteralPath $Path -Raw
    return @{
        id = [string]$xml.Meta.id
        title = [string]$xml.Meta.title
        version = [string]$xml.Meta.version
    }
}

function Assert-PackageMetaMatches([string]$ExpectedPath, [string]$ActualPath, [string]$Label) {
    $expected = Get-PackageMeta $ExpectedPath
    $actual = Get-PackageMeta $ActualPath

    foreach ($field in @("id", "title", "version")) {
        if ($expected[$field] -ne $actual[$field]) {
            Fail "$Label metadata mismatch for <$field>: expected '$($expected[$field])', got '$($actual[$field])'"
        }
    }
}

function Get-FileSha256([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "Missing file for hashing: $Path"
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-FileHashMatches([string]$ExpectedPath, [string]$ActualPath, [string]$Label) {
    $expected = Get-FileSha256 $ExpectedPath
    $actual = Get-FileSha256 $ActualPath
    if ($expected -ne $actual) {
        Fail "$Label hash mismatch between '$ExpectedPath' and '$ActualPath'"
    }
}

function Assert-Preview([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "Missing preview.jpg: $Path"
    }

    $length = (Get-Item -LiteralPath $Path).Length
    if ($length -gt 1MB) {
        Warn "preview.jpg is larger than 1 MB: $Path"
    }
}

function Read-ZipEntryText([System.IO.Compression.ZipArchive]$Archive, [string]$EntryName) {
    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        return $null
    }

    $stream = $entry.Open()
    try {
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            return $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Get-XlsxSharedStrings([System.IO.Compression.ZipArchive]$Archive) {
    $text = Read-ZipEntryText $Archive "xl/sharedStrings.xml"
    if ($null -eq $text) {
        Fail "SourceCard.xlsx is missing xl/sharedStrings.xml."
    }

    [xml]$xml = $text
    $strings = New-Object System.Collections.Generic.List[string]
    foreach ($si in $xml.SelectNodes("//*[local-name()='sst']/*[local-name()='si']")) {
        $parts = New-Object System.Collections.Generic.List[string]
        foreach ($t in $si.SelectNodes(".//*[local-name()='t']")) {
            $parts.Add($t.InnerText) | Out-Null
        }
        $strings.Add(($parts -join "")) | Out-Null
    }
    return $strings
}

function Convert-XlsxCellValue($Cell, $SharedStrings) {
    $type = $Cell.GetAttribute("t")
    if ($type -eq "inlineStr") {
        Fail "SourceCard.xlsx contains inlineStr cells. Save it with shared strings before packaging."
    }

    $valueNode = $Cell.SelectSingleNode("*[local-name()='v']")
    if ($null -eq $valueNode) {
        return $null
    }

    $value = $valueNode.InnerText
    if ($type -eq "s") {
        return $SharedStrings[[int]$value]
    }

    return $value
}

function Get-XlsxColumnName([string]$CellReference) {
    if ($CellReference -notmatch "^([A-Z]+)") {
        return $null
    }
    return $Matches[1]
}

function Get-XlsxIds([string]$XlsxPath, [string[]]$SheetNames) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($XlsxPath)
    try {
        $sharedStrings = Get-XlsxSharedStrings $archive

        [xml]$workbook = Read-ZipEntryText $archive "xl/workbook.xml"
        [xml]$rels = Read-ZipEntryText $archive "xl/_rels/workbook.xml.rels"

        $relTargets = @{}
        foreach ($rel in $rels.SelectNodes("//*[local-name()='Relationship']")) {
            $relTargets[$rel.GetAttribute("Id")] = $rel.GetAttribute("Target")
        }

        $ids = New-Object System.Collections.Generic.List[string]
        foreach ($sheet in $workbook.SelectNodes("//*[local-name()='sheet']")) {
            $sheetName = $sheet.GetAttribute("name")
            if ($SheetNames -notcontains $sheetName) {
                continue
            }

            $relId = $sheet.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships")
            if (!$relTargets.ContainsKey($relId)) {
                Fail "Could not resolve workbook relationship '$relId' for sheet '$sheetName'."
            }

            $target = $relTargets[$relId].Replace("\", "/")
            if ($target.StartsWith("/")) {
                $entryName = $target.TrimStart("/")
            } else {
                $entryName = "xl/$target"
            }

            [xml]$sheetXml = Read-ZipEntryText $archive $entryName
            $idColumn = $null
            foreach ($row in $sheetXml.SelectNodes("//*[local-name()='sheetData']/*[local-name()='row']")) {
                $rowNumber = [int]$row.GetAttribute("r")
                $cellsByColumn = @{}
                foreach ($cell in $row.SelectNodes("*[local-name()='c']")) {
                    $column = Get-XlsxColumnName $cell.GetAttribute("r")
                    if ($null -ne $column) {
                        $cellsByColumn[$column] = Convert-XlsxCellValue $cell $sharedStrings
                    }
                }

                if ($rowNumber -eq 1) {
                    foreach ($key in $cellsByColumn.Keys) {
                        if ($cellsByColumn[$key] -eq "id") {
                            $idColumn = $key
                            break
                        }
                    }
                    if ($null -eq $idColumn) {
                        Fail "Could not find id column in SourceCard.xlsx sheet '$sheetName'."
                    }
                    continue
                }

                if ($rowNumber -ge 4 -and $null -ne $idColumn -and $cellsByColumn.ContainsKey($idColumn)) {
                    $id = $cellsByColumn[$idColumn]
                    if (![string]::IsNullOrWhiteSpace($id)) {
                        $ids.Add($id) | Out-Null
                    }
                }
            }
        }

        return $ids
    } finally {
        $archive.Dispose()
    }
}

function Assert-SourceCardWorkbook([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "Missing SourceCard.xlsx: $Path"
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        if ($null -eq $archive.GetEntry("xl/sharedStrings.xml")) {
            Fail "SourceCard.xlsx is missing xl/sharedStrings.xml."
        }

        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -like "xl/worksheets/*.xml") {
                $text = Read-ZipEntryText $archive $entry.FullName
                if ($text -match 't="inlineStr"|<is>') {
                    Fail "SourceCard.xlsx contains inlineStr cells in $($entry.FullName)."
                }
            }
        }
    } finally {
        $archive.Dispose()
    }
}

function Assert-WorkbookSharedStrings([string]$Path, [string]$Label) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "Missing $Label workbook: $Path"
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        if ($null -eq $archive.GetEntry("xl/sharedStrings.xml")) {
            Fail "$Label workbook is missing xl/sharedStrings.xml."
        }

        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -like "xl/worksheets/*.xml") {
                $text = Read-ZipEntryText $archive $entry.FullName
                if ($text -match 't="inlineStr"|<is>') {
                    Fail "$Label workbook contains inlineStr cells in $($entry.FullName)."
                }
            }
        }
    } finally {
        $archive.Dispose()
    }
}

function Assert-FlatTextureDirectory([string]$TextureDir) {
    if (!(Test-Path -LiteralPath $TextureDir -PathType Container)) {
        Fail "Missing texture directory: $TextureDir"
    }

    $nested = Get-ChildItem -LiteralPath $TextureDir -Recurse -File -Filter "*.png" |
        Where-Object { $_.DirectoryName -ne (Get-Item -LiteralPath $TextureDir).FullName }

    if ($nested) {
        $paths = ($nested | ForEach-Object { $_.FullName }) -join ", "
        Fail "SkyreaderGuild textures must be flat under Texture/. Nested PNGs found: $paths"
    }
}

function Assert-SkyreaderTextures([string]$SourceCardPath, [string]$SourceTextureDir, [string]$StagedTextureDir) {
    Assert-FlatTextureDirectory $SourceTextureDir
    Assert-FlatTextureDirectory $StagedTextureDir

    $excludedIds = @("srg_guild_entrance", "srg_guild_exit")
    $requiredIds = Get-XlsxIds $SourceCardPath @("Thing", "Chara") |
        Where-Object { $_ -like "srg_*" -and $excludedIds -notcontains $_ } |
        Sort-Object -Unique

    $sourceTextureIds = Get-ChildItem -LiteralPath $SourceTextureDir -File -Filter "*.png" |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) }
    $stagedTextureIds = Get-ChildItem -LiteralPath $StagedTextureDir -File -Filter "*.png" |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) }

    $missingFromSource = @($requiredIds | Where-Object { $sourceTextureIds -notcontains $_ })
    if ($missingFromSource.Count -gt 0) {
        Fail "SkyreaderGuild SourceCard ids are missing flat source textures: $($missingFromSource -join ', ')"
    }

    $missingFromStage = @($requiredIds | Where-Object { $stagedTextureIds -notcontains $_ })
    if ($missingFromStage.Count -gt 0) {
        Fail "SkyreaderGuild package is missing flat staged textures: $($missingFromStage -join ', ')"
    }
}

function Assert-PrefixedTextures([string]$PackageLabel, [string]$Prefix, [string]$SourceCardPath, [string]$SourceTextureDir, [string]$StagedTextureDir) {
    Assert-FlatTextureDirectory $SourceTextureDir
    Assert-FlatTextureDirectory $StagedTextureDir

    $requiredIds = Get-XlsxIds $SourceCardPath @("Thing", "Chara") |
        Where-Object { $_ -like "$Prefix*" } |
        Sort-Object -Unique

    $sourceTextureIds = Get-ChildItem -LiteralPath $SourceTextureDir -File -Filter "*.png" |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) }
    $stagedTextureIds = Get-ChildItem -LiteralPath $StagedTextureDir -File -Filter "*.png" |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) }

    $missingFromSource = @($requiredIds | Where-Object { $sourceTextureIds -notcontains $_ })
    if ($missingFromSource.Count -gt 0) {
        Fail "$PackageLabel SourceCard ids are missing flat source textures: $($missingFromSource -join ', ')"
    }

    $missingFromStage = @($requiredIds | Where-Object { $stagedTextureIds -notcontains $_ })
    if ($missingFromStage.Count -gt 0) {
        Fail "$PackageLabel package is missing flat staged textures: $($missingFromStage -join ', ')"
    }
}

function Get-RelativeZipPath([string]$BasePath, [string]$Path) {
    $baseFull = [System.IO.Path]::GetFullPath($BasePath)
    if (!$baseFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFull += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($baseFull)
    $pathUri = [System.Uri]::new([System.IO.Path]::GetFullPath($Path))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString())
}

function New-ZipWithRoot([string]$SourceFolder, [string]$ZipPath) {
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    $parent = Split-Path $SourceFolder -Parent
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $SourceFolder -Recurse -File) {
            $entryName = Get-RelativeZipPath $parent $file.FullName
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    } finally {
        $zip.Dispose()
    }
}

function Assert-ZipSingleRoot([string]$ZipPath, [string]$ExpectedRoot) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $roots = @($zip.Entries |
            Where-Object { $_.FullName -and !$_.FullName.EndsWith("/") } |
            ForEach-Object { $_.FullName.Split("/")[0] } |
            Sort-Object -Unique)

        if ($roots.Count -ne 1 -or $roots[0] -ne $ExpectedRoot) {
            Fail "$ZipPath must contain exactly one top-level folder named '$ExpectedRoot'. Found: $($roots -join ', ')"
        }
    } finally {
        $zip.Dispose()
    }
}

function Build-Mods {
    if ($NoBuild) {
        Write-Host "Skipping build because -NoBuild was set."
        return
    }

    if (!(Test-Path -LiteralPath $SolutionPath -PathType Leaf)) {
        Fail "Missing solution file: $SolutionPath"
    }

    & dotnet build $SolutionPath -c $Configuration -m:1 "/p:ElinDir=$ElinDir"
    if ($LASTEXITCODE -ne 0) {
        Fail "dotnet build failed."
    }
}

function Copy-ScriptModPackage([string]$ProjectDir, [string]$PackageName, [string]$AssemblyName) {
    $sourceDir = Join-Path $RepoRoot $ProjectDir
    $packageDir = Join-Path $StagingRoot $PackageName
    $assemblyDir = Join-Path $packageDir "Assemblies"
    New-Item -ItemType Directory -Path $assemblyDir -Force | Out-Null

    Copy-RequiredFile (Join-Path $sourceDir "package.xml") (Join-Path $packageDir "package.xml")
    Copy-RequiredFile (Join-Path $sourceDir "preview.jpg") (Join-Path $packageDir "preview.jpg")
    Copy-RequiredFile (Join-Path $sourceDir "bin\$Configuration\$AssemblyName.dll") (Join-Path $assemblyDir "$AssemblyName.dll")

    if ($IncludePdb) {
        $pdbPath = Join-Path $sourceDir "bin\$Configuration\$AssemblyName.pdb"
        if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
            Copy-Item -LiteralPath $pdbPath -Destination (Join-Path $assemblyDir "$AssemblyName.pdb") -Force
        }
    }

    Assert-PackageXml (Join-Path $packageDir "package.xml")
    Assert-Preview (Join-Path $packageDir "preview.jpg")
}

function Copy-SkyreaderPackage {
    $sourceDir = Join-Path $RepoRoot "SkyreaderGuild"
    $serverSourceDir = Join-Path $RepoRoot "SkyreaderGuildServer"
    $packageDir = Join-Path $StagingRoot "SkyreaderGuild"
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

    Copy-RequiredFile (Join-Path $sourceDir "package.xml") (Join-Path $packageDir "package.xml")
    Copy-RequiredFile (Join-Path $sourceDir "preview.jpg") (Join-Path $packageDir "preview.jpg")
    Copy-RequiredFile (Join-Path $sourceDir "bin\$Configuration\SkyreaderGuild.dll") (Join-Path $packageDir "SkyreaderGuild.dll")

    if ($IncludePdb) {
        $pdbPath = Join-Path $sourceDir "bin\$Configuration\SkyreaderGuild.pdb"
        if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
            Copy-Item -LiteralPath $pdbPath -Destination (Join-Path $packageDir "SkyreaderGuild.pdb") -Force
        }
    }

    $sourceCardSource = Join-Path $sourceDir "LangMod\EN\SourceCard.xlsx"
    $sourceCardDest = Join-Path $packageDir "LangMod\EN\SourceCard.xlsx"
    Copy-RequiredFile $sourceCardSource $sourceCardDest
    Assert-SourceCardWorkbook $sourceCardDest

    $textureSource = Join-Path $sourceDir "Texture"
    $textureDest = Join-Path $packageDir "Texture"
    New-Item -ItemType Directory -Path $textureDest -Force | Out-Null
    Assert-FlatTextureDirectory $textureSource
    Get-ChildItem -LiteralPath $textureSource -File -Filter "*.png" |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $textureDest $_.Name) -Force }

    Assert-SkyreaderTextures $sourceCardDest $textureSource $textureDest

    $serverDest = Join-Path $packageDir "Server\SkyreaderGuildServer"
    New-Item -ItemType Directory -Path $serverDest -Force | Out-Null
    Copy-RequiredFile (Join-Path $serverSourceDir "pyproject.toml") (Join-Path $serverDest "pyproject.toml")
    Copy-RequiredFile (Join-Path $serverSourceDir "README.md") (Join-Path $serverDest "README.md")
    $serverSrcRoot = Join-Path $serverSourceDir "src"
    $serverSrcDest = Join-Path $serverDest "src"
    New-Item -ItemType Directory -Path $serverSrcDest -Force | Out-Null
    Get-ChildItem -LiteralPath $serverSrcRoot -Recurse -File |
        Where-Object { $_.FullName -notmatch "\\__pycache__\\" } |
        ForEach-Object {
            $relative = Get-RelativeZipPath $serverSrcRoot $_.FullName
            $destination = Join-Path $serverSrcDest $relative
            $destinationDir = Split-Path $destination -Parent
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }

    Assert-PackageXml (Join-Path $packageDir "package.xml")
    Assert-Preview (Join-Path $packageDir "preview.jpg")
}

function Copy-UnderworldPackage {
    $sourceDir = Join-Path $RepoRoot "ElinUnderworldSimulator"
    $serverSourceDir = Join-Path $sourceDir "Server\UnderworldServer"
    $packageDir = Join-Path $StagingRoot "ElinUnderworldSimulator"
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

    Copy-RequiredFile (Join-Path $sourceDir "package.xml") (Join-Path $packageDir "package.xml")
    Copy-RequiredFile (Join-Path $sourceDir "preview.jpg") (Join-Path $packageDir "preview.jpg")
    Copy-RequiredFile (Join-Path $sourceDir "bin\$Configuration\ElinUnderworldSimulator.dll") (Join-Path $packageDir "ElinUnderworldSimulator.dll")

    if ($IncludePdb) {
        $pdbPath = Join-Path $sourceDir "bin\$Configuration\ElinUnderworldSimulator.pdb"
        if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
            Copy-Item -LiteralPath $pdbPath -Destination (Join-Path $packageDir "ElinUnderworldSimulator.pdb") -Force
        }
    }

    $sourceCardSource = Join-Path $sourceDir "LangMod\EN\SourceCard.xlsx"
    $sourceCardDest = Join-Path $packageDir "LangMod\EN\SourceCard.xlsx"
    Copy-RequiredFile $sourceCardSource $sourceCardDest
    Assert-SourceCardWorkbook $sourceCardDest

    $sourceBlockSource = Join-Path $sourceDir "LangMod\EN\SourceBlock.xlsx"
    $sourceBlockDest = Join-Path $packageDir "LangMod\EN\SourceBlock.xlsx"
    Copy-RequiredFile $sourceBlockSource $sourceBlockDest
    Assert-WorkbookSharedStrings $sourceBlockDest "SourceBlock"

    $sourceGameSource = Join-Path $sourceDir "LangMod\EN\SourceGame.xlsx"
    $sourceGameDest = Join-Path $packageDir "LangMod\EN\SourceGame.xlsx"
    Copy-RequiredFile $sourceGameSource $sourceGameDest
    Assert-WorkbookSharedStrings $sourceGameDest "SourceGame"

    $textureSource = Join-Path $sourceDir "Texture"
    $textureDest = Join-Path $packageDir "Texture"
    New-Item -ItemType Directory -Path $textureDest -Force | Out-Null
    Assert-FlatTextureDirectory $textureSource
    Get-ChildItem -LiteralPath $textureSource -File -Filter "*.png" |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $textureDest $_.Name) -Force }

    Assert-PrefixedTextures "ElinUnderworldSimulator" "uw_" $sourceCardDest $textureSource $textureDest

    $serverDest = Join-Path $packageDir "Server\UnderworldServer"
    New-Item -ItemType Directory -Path $serverDest -Force | Out-Null
    Copy-RequiredFile (Join-Path $serverSourceDir "pyproject.toml") (Join-Path $serverDest "pyproject.toml")
    Copy-RequiredFile (Join-Path $serverSourceDir "README.md") (Join-Path $serverDest "README.md")
    $serverSrcRoot = Join-Path $serverSourceDir "src"
    $serverSrcDest = Join-Path $serverDest "src"
    New-Item -ItemType Directory -Path $serverSrcDest -Force | Out-Null
    Get-ChildItem -LiteralPath $serverSrcRoot -Recurse -File |
        Where-Object { $_.FullName -notmatch "\\__pycache__\\" } |
        ForEach-Object {
            $relative = Get-RelativeZipPath $serverSrcRoot $_.FullName
            $destination = Join-Path $serverSrcDest $relative
            $destinationDir = Split-Path $destination -Parent
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }

    Assert-PackageMetaMatches (Join-Path $sourceDir "package.xml") (Join-Path $packageDir "package.xml") "Staged ElinUnderworldSimulator"
    Assert-FileHashMatches (Join-Path $sourceDir "bin\$Configuration\ElinUnderworldSimulator.dll") (Join-Path $packageDir "ElinUnderworldSimulator.dll") "Staged ElinUnderworldSimulator.dll"
    Assert-PackageXml (Join-Path $packageDir "package.xml")
    Assert-Preview (Join-Path $packageDir "preview.jpg")
}

function Assert-NoForbiddenFiles {
    $forbidden = Get-ChildItem -LiteralPath $StagingRoot -Recurse -File |
        Where-Object {
            $_.Extension -in @(".cs", ".csproj", ".db", ".pyc") -or
            $_.Name -eq "SourceLocalization.json" -or
            $_.FullName -match "\\(bin|obj|worklog|Screenshots|reports|tests|docs|__pycache__|\.venv)\\"
        }

    if ($forbidden) {
        $paths = ($forbidden | ForEach-Object { $_.FullName }) -join ", "
        Fail "Forbidden files were staged for packaging: $paths"
    }
}

function New-AllZip([string]$ZipPath) {
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $StagingRoot -Recurse -File) {
            $entryName = Get-RelativeZipPath $StagingRoot $file.FullName
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    } finally {
        $zip.Dispose()
    }
}

function Deploy-Packages {
    $packageDir = Join-Path $ElinDir "Package"
    if (!(Test-Path -LiteralPath $packageDir -PathType Container)) {
        Fail "Elin package directory does not exist: $packageDir"
    }

    foreach ($package in @("FastStartMod", "PartyWage", "SkyreaderGuild", "ElinUnderworldSimulator")) {
        $source = Join-Path $StagingRoot $package
        $dest = Join-Path $packageDir $package
        if (Test-Path -LiteralPath $dest) {
            Remove-Item -LiteralPath $dest -Recurse -Force
        }
        Copy-Item -LiteralPath $source -Destination $dest -Recurse -Force
        Write-Host "Deployed $package to $dest"

        if ($package -eq "ElinUnderworldSimulator") {
            Assert-PackageMetaMatches (Join-Path $RepoRoot "ElinUnderworldSimulator\package.xml") (Join-Path $dest "package.xml") "Deployed ElinUnderworldSimulator"
            Assert-FileHashMatches (Join-Path $RepoRoot "ElinUnderworldSimulator\bin\$Configuration\ElinUnderworldSimulator.dll") (Join-Path $dest "ElinUnderworldSimulator.dll") "Deployed ElinUnderworldSimulator.dll"
        }
    }
}

Write-Host "Packaging Elin mods from $RepoRoot"
Write-Host "Configuration: $Configuration"
Write-Host "ElinDir: $ElinDir"
Write-Host "Output: $OutputPath"

Build-Mods

Reset-Directory $StagingRoot
Reset-Directory $OutputPath

Copy-ScriptModPackage "FastStart" "FastStartMod" "FastStartMod"
Copy-ScriptModPackage "PartyWage" "PartyWage" "PartyWage"
Copy-SkyreaderPackage
Copy-UnderworldPackage
Assert-NoForbiddenFiles

$packages = @{
    "FastStartMod" = "FastStartMod.zip"
    "PartyWage" = "PartyWage.zip"
    "SkyreaderGuild" = "SkyreaderGuild.zip"
    "ElinUnderworldSimulator" = "ElinUnderworldSimulator.zip"
}

foreach ($packageName in $packages.Keys) {
    $zipPath = Join-Path $OutputPath $packages[$packageName]
    New-ZipWithRoot (Join-Path $StagingRoot $packageName) $zipPath
    Assert-ZipSingleRoot $zipPath $packageName
    Write-Host "Created $zipPath"
}

$allZip = Join-Path $OutputPath "ElinMods-all.zip"
New-AllZip $allZip
Write-Host "Created $allZip"

if ($Deploy) {
    Deploy-Packages
}

Write-Host "Packaging complete."
