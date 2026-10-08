param([string]$ArtifactsPath = '.data/backend-validation')
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactsPath))
if (-not $outputRoot.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La salida de validación debe permanecer dentro del repositorio.'
}
Push-Location $repositoryRoot
try {
    dotnet build UniversityParking.sln --artifacts-path $outputRoot
    if ($LASTEXITCODE -ne 0) { throw 'La compilación falló; no se ejecutaron pruebas con binarios anteriores.' }
    dotnet test UniversityParking.sln --no-build --artifacts-path $outputRoot --logger 'trx;LogFilePrefix=backend' --results-directory (Join-Path $outputRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'La validación automática del backend falló.' }
}
finally { Pop-Location }
