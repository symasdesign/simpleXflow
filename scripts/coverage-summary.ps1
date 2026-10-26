[CmdletBinding()]
param([Parameter(Mandatory)][string] $ResultsDirectory)

$ErrorActionPreference = 'Stop'
$reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Recurse -Filter coverage.cobertura.xml)
if ($reports.Count -eq 0) { throw "No Cobertura reports found in $ResultsDirectory." }

# Merge line hits across test assemblies; the same production method can be exercised at several levels.
$assemblies = @{}
foreach ($report in $reports) {
    [xml] $coverage = Get-Content -LiteralPath $report.FullName -Raw
    $sourceRoots = @($coverage.coverage.sources.source | ForEach-Object { [string] $_ })
    foreach ($package in $coverage.coverage.packages.package) {
        $assemblyName = [string] $package.name
        if (-not $assemblies.ContainsKey($assemblyName)) { $assemblies[$assemblyName] = @{} }
        $lines = $assemblies[$assemblyName]
        foreach ($class in $package.classes.class) {
            $sourceFile = [string] $class.filename
            if (-not [System.IO.Path]::IsPathRooted($sourceFile)) {
                # Coverlet chooses different common source roots for different test projects.
                $candidates = @($sourceRoots | ForEach-Object {
                    [System.IO.Path]::GetFullPath((Join-Path $_ $sourceFile))
                })
                $sourceFile = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
                if (-not $sourceFile) { $sourceFile = $candidates | Select-Object -First 1 }
                if (-not $sourceFile) { throw "Cannot resolve source path in $($report.FullName)." }
            }
            foreach ($line in $class.lines.line) {
                $key = "$($sourceFile.Replace('\', '/')):$($line.number)"
                $lines[$key] = $lines[$key] -or ([long] $line.hits -gt 0)
            }
        }
    }
}

$summary = @(
    '# .NET line coverage',
    '',
    'Combined line hits from all test projects. Browser JavaScript coverage is not included.',
    '',
    '| Assembly | Covered lines | Total lines | Coverage |',
    '| --- | ---: | ---: | ---: |'
)
foreach ($assemblyName in ($assemblies.Keys | Sort-Object)) {
    $lines = $assemblies[$assemblyName]
    $covered = @($lines.Values | Where-Object { $_ }).Count
    $percentage = if ($lines.Count -gt 0) { 100.0 * $covered / $lines.Count } else { 0 }
    $formatted = $percentage.ToString('F2', [System.Globalization.CultureInfo]::InvariantCulture)
    $summary += "| $assemblyName | $covered | $($lines.Count) | $formatted% |"
}
$summary | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'coverage-summary.md') -Encoding utf8
$summary | Write-Output
