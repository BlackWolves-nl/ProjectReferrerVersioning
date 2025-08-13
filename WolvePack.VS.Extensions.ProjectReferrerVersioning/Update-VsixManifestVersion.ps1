param(
    [string]$ManifestPath = "source.extension.vsixmanifest",
    [string]$CsprojPath = "WolvePack.VS.Extensions.ProjectReferrerVersioning.csproj",
    [string]$PackagePath = "WolvePackVSExtensionsProjectReferrerVersioningPackage.cs"
)

# Read the version from the .csproj file
[xml]$csproj = Get-Content $CsprojPath
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $version) {
    Write-Error "Could not find <Version> in $CsprojPath"
    exit 1
}

# Update the VSIX manifest only if the version is different
[xml]$manifest = Get-Content $ManifestPath
$identity = $manifest.PackageManifest.Metadata.Identity
if ($identity.Version -ne $version.ToString().Trim()) {
    $identity.Version = $version.ToString().Trim()
    $manifest.Save($ManifestPath)
    Write-Host "Updated $ManifestPath to version $version"
} else {
    Write-Host "$ManifestPath already at version $version"
}

# Update InstalledProductRegistration attribute in the package source file
$packageFile = Get-Content $PackagePath -Raw
# Improved pattern: captures quoted strings and allows for whitespace and optional spaces before/after commas
$pattern = '\[InstalledProductRegistration\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*\)\s*\]'

$updatedPackageFile = [System.Text.RegularExpressions.Regex]::Replace(
    $packageFile,
    $pattern,
    {
        param($match)
        $oldVersion = $match.Groups[3].Value
        if ($oldVersion -eq $version) {
            # No change needed, return original match
            return $match.Value
        } else {
            return "[InstalledProductRegistration(`"$($match.Groups[1].Value)`", `"$($match.Groups[2].Value)`", `"$version`")]"
        }
    },
    [System.Text.RegularExpressions.RegexOptions]::Multiline
)

if ($updatedPackageFile -ne $packageFile) {
    [System.IO.File]::WriteAllText($PackagePath, $updatedPackageFile)
    Write-Host "Updated $PackagePath InstalledProductRegistration to version $version"
} elseif ($packageFile -match $pattern) {
    Write-Host "InstalledProductRegistration already at version $version in $PackagePath"
} else {
    Write-Warning "No InstalledProductRegistration attribute found in $PackagePath"
}