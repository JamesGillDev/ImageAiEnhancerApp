param(
    [string]$Runtime = "win-x64",
    [ValidateSet("true", "false")]
    [string]$SelfContained = "true"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$projectPath = Join-Path $repoRoot "src\ImageAiEnhancerApp.App\ImageAiEnhancerApp.App.csproj"
$propsPath = Join-Path $repoRoot "Directory.Build.props"

[xml]$props = Get-Content $propsPath
$version = $props.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version not found in $propsPath."
}

$publishDir = Join-Path $repoRoot ("artifacts\publish\{0}\v{1}" -f $Runtime, $version)
$zipPath = Join-Path $repoRoot ("artifacts\ImageAiEnhancerApp-{0}-v{1}.zip" -f $Runtime, $version)
$exePath = Join-Path $publishDir "ImageAiEnhancerApp.exe"

Write-Host "Publishing ImageAiEnhancerApp v$version to $publishDir"
dotnet publish $projectPath -c Release -r $Runtime --self-contained $SelfContained -o $publishDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Published EXE: $exePath"
Write-Host "Release ZIP:   $zipPath"
