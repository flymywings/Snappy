param(
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts')
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
function Run-Dotnet([string[]]$Arguments) {
    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    Run-Dotnet @('restore', 'Snappy.slnx', '--configfile', 'NuGet.Config')
    Run-Dotnet @('test', 'Snappy.slnx', '-c', 'Release', '--no-restore')
    Run-Dotnet @('build', 'Snappy.slnx', '-c', 'Release', '--no-restore')
    Run-Dotnet @('restore', 'src/Snappy.Windows/Snappy.Windows.csproj', '-r', 'win-x64', '--configfile', 'NuGet.Config', '-p:SelfContained=true')
    $packageDirectory = Join-Path $OutputDirectory 'Snappy-win-x64'
    New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
    Run-Dotnet @('publish', 'src/Snappy.Windows/Snappy.Windows.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-o', $packageDirectory, '-p:DebugType=None', '-p:DebugSymbols=false')
    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath $file -Destination $packageDirectory -Force }
    # Locate the exact versions resolved into project.assets.json, rather than unrelated cached SDKs.
    $assets = Get-Content -Raw 'src/Snappy.Windows/obj/project.assets.json' | ConvertFrom-Json
    $noticeDirectory = Join-Path $packageDirectory 'runtime-notices'
    New-Item -ItemType Directory -Force -Path $noticeDirectory | Out-Null
    $copied = 0
    $runtimePacks = $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies
    foreach ($entry in $runtimePacks) {
        if ($entry.name -notmatch '^Microsoft\.(NETCore\.App|WindowsDesktop\.App)\.Runtime\.win-x64$') { continue }
        $version = $entry.version.Trim('[', ']').Split(',')[0].Trim()
        foreach ($root in $assets.packageFolders.PSObject.Properties.Name) {
            $pack = Join-Path $root (Join-Path $entry.name.ToLowerInvariant() $version)
            if (!(Test-Path -LiteralPath $pack)) { continue }
            $prefix = "$($entry.name)-$version"
            foreach ($file in Get-ChildItem -LiteralPath $pack -File | Where-Object { $_.Name -match '(?i)^(LICENSE|THIRD.PARTY.NOTICES)' }) {
                Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $noticeDirectory "$prefix-$($file.Name)") -Force
                $copied++
            }
        }
    }
    if ($copied -lt 2) { throw 'Runtime license notices were not found; refusing to package.' }
    $zip = Join-Path $OutputDirectory 'Snappy-0.5.0-win-x64.zip'
    Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $zip -Force
    Get-FileHash -LiteralPath $zip -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($zip))" } | Set-Content (Join-Path $OutputDirectory 'SHA256SUMS.txt')
    Write-Output "Portable package: $zip"
}
finally { Pop-Location }
