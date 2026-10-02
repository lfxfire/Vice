# Builds the Vice installer (ClickOnce) into the publish folder set in Vice.csproj, then moves
# Vice.csproj on to the next version for next time, the way Visual Studio's "Publish Now" does.
#
#   powershell -ExecutionPolicy Bypass -File Publish.ps1
#
# Install Vice by running setup.exe in the publish folder. An installed Vice looks in that folder
# for updates each time it starts, so to update it, publish again.
param(
    # Defaults to PublishUrl in Vice.csproj. Installed copies update from here, so keep it the same
    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Vice.csproj'
$text = [IO.File]::ReadAllText($project)

if (-not $PublishDir) {
    $PublishDir = [regex]::Match($text, '<PublishUrl>(.*?)</PublishUrl>').Groups[1].Value
}
if (-not $PublishDir) { throw 'Pass -PublishDir, or set PublishUrl in Vice.csproj' }

$folder = $PublishDir.TrimEnd('\', '/')

# MSBuild builds the installer here first, then the installer files are copied to the publish folder
$staging = Join-Path $PSScriptRoot 'obj\Publish'

# MSBuild needs trailing separators. A forward slash avoids "\" escaping the closing quote
$msbuildFolder = $folder + '/'
$msbuildStaging = $staging + '/'

# MSBuild comes with Visual Studio
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path $vswhere) {
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuild) { throw 'MSBuild not found. Install Visual Studio with the .NET desktop development workload' }

# The version is ApplicationVersion from Vice.csproj, moved past any revision that's already
# published, because an installed copy ignores a new build that reuses a version number
$match = [regex]::Match($text, '<ApplicationVersion>(\d+\.\d+\.\d+)\.(\d+)</ApplicationVersion>')
if (-not $match.Success) { throw 'ApplicationVersion in Vice.csproj should look like 1.2.3.4' }

$base = $match.Groups[1].Value
$revision = [int]$match.Groups[2].Value
while (Test-Path (Join-Path $folder ('Application Files\Vice_' + $base.Replace('.', '_') + '_' + $revision))) {
    $revision++
}
$version = "$base.$revision"

Write-Host "Publishing Vice $version to $folder"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
& $msbuild $project /t:Publish /p:Configuration=Release "/p:PublishDir=$msbuildStaging" "/p:PublishUrl=$msbuildFolder" "/p:ApplicationVersion=$version" "/p:ApplicationRevision=$revision" /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed (exit code $LASTEXITCODE)" }

# Copies what Visual Studio would: this version's files first, then setup.exe and Vice.application,
# which send installs and updates to them
$versionFolder = 'Application Files\Vice_' + $version.Replace('.', '_')
New-Item -ItemType Directory -Force (Join-Path $folder 'Application Files') | Out-Null
Copy-Item -Recurse (Join-Path $staging $versionFolder) (Join-Path $folder 'Application Files')
Copy-Item (Join-Path $staging 'setup.exe'), (Join-Path $staging 'Vice.application') $folder -Force

# Moves Vice.csproj on to the next revision, so the next publish, from here or Visual Studio, is newer
$next = $revision + 1
$text = $text.Replace($match.Value, "<ApplicationVersion>$base.$next</ApplicationVersion>")
$text = [regex]::Replace($text, '<ApplicationRevision>\d+</ApplicationRevision>', "<ApplicationRevision>$next</ApplicationRevision>")
$bom = [IO.File]::ReadAllBytes($project)[0] -eq 0xEF
[IO.File]::WriteAllText($project, $text, (New-Object Text.UTF8Encoding($bom)))

Write-Host ''
Write-Host "Published Vice $version. Install or update it by running:"
Write-Host "  $folder\setup.exe"
