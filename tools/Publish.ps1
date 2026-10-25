# Creates a portable Windows x64 package. Run with PowerShell 7 on Windows.
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $projectRoot 'src/ChatMixOverlay.csproj'
[xml]$projectXml = Get-Content -LiteralPath $projectPath
$releaseVersion = [string]$projectXml.Project.PropertyGroup.Version
$outputDirectory = Join-Path $projectRoot 'dist/portable'
$archivePath = Join-Path $projectRoot "dist/ChatMixOverlay-$releaseVersion-win-x64.zip"

dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:UseSharedCompilation=false -p:DebugType=None -p:DebugSymbols=false -o $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Exit the running overlay before rebuilding its executable.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $outputDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $outputDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $outputDirectory
$documentsDirectory = Join-Path $outputDirectory 'docs'
New-Item -ItemType Directory -Force -Path $documentsDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/technical-notes.md') -Destination $documentsDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/images') -Destination $documentsDirectory -Recurse -Force
$packageFiles = @((Join-Path $outputDirectory 'ChatMixOverlay.exe'), (Join-Path $outputDirectory 'README.md'), (Join-Path $outputDirectory 'CHANGELOG.md'), (Join-Path $outputDirectory 'LICENSE'), $documentsDirectory)
Compress-Archive -Path $packageFiles -DestinationPath $archivePath -Force
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$archiveHash  $([IO.Path]::GetFileName($archivePath))" | Set-Content -LiteralPath "$archivePath.sha256" -Encoding ascii
Write-Host "Created $archivePath"
