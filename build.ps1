$projectFile = "$PSScriptRoot\csharp.csproj"
$outputDir   = "$PSScriptRoot\publish"

dotnet publish $projectFile `
    -c Release `
    -r win-x64 `
    -f net8.0-windows `
    --self-contained false `
    -p:PublishSingleFile=true `
    -o $outputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded: $outputDir\GpuPreference.exe"
} else {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}
