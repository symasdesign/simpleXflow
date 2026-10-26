[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipBrowser,
    [switch] $InstallBrowsers,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot

function Invoke-Checked {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repository
$previousConfiguration = $env:SIMPLEXFLOW_TEST_CONFIGURATION
try {
    if (-not $NoRestore) {
        Invoke-Checked 'dotnet' @('restore', 'simpleXflow.slnx')
    }
    Invoke-Checked 'dotnet' @('build', 'simpleXflow.slnx', '--configuration', $Configuration, '--no-restore')
    $runId = Get-Date -Format 'yyyyMMdd-HHmmss-ffff'
    $results = "artifacts/test-results/$runId"
    Invoke-Checked 'dotnet' @('test', 'simpleXflow.slnx', '--configuration', $Configuration,
        '--no-build', '--logger', 'trx', '--results-directory', $results,
        '--collect', 'XPlat Code Coverage', '--settings', 'tests/coverage.runsettings')
    & (Join-Path $PSScriptRoot 'coverage-summary.ps1') -ResultsDirectory $results

    if (-not $SkipBrowser) {
        $npmExecutable = if ($env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }
        $npxExecutable = if ($env:OS -eq 'Windows_NT') { 'npx.cmd' } else { 'npx' }
        if (-not $NoRestore) {
            Invoke-Checked $npmExecutable @('ci')
        }
        if ($InstallBrowsers) {
            Invoke-Checked $npxExecutable @('playwright', 'install', '--with-deps', 'chromium')
        }
        $env:SIMPLEXFLOW_TEST_CONFIGURATION = $Configuration
        Invoke-Checked $npmExecutable @('run', 'test:all')
    }
    Write-Host "Tests passed. TRX and Cobertura: $results"
    if (-not $SkipBrowser) { Write-Host 'Browser reports: artifacts/browser/bridge-report and artifacts/browser/e2e-report' }
}
finally {
    $env:SIMPLEXFLOW_TEST_CONFIGURATION = $previousConfiguration
    Pop-Location
}
