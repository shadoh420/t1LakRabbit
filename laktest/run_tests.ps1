# Runs the LakRabbit tests on a hidden dedicated server.
# Shows the results of this run. The exit code is 0 only if all checks pass.
# Put the laktest and lakrabbit folders in the Tribes 1.11 folder first.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File run_tests.ps1 [-Mission Peekaboo]
param([string]$Mission = 'Peekaboo', [int]$TimeoutSec = 150)
$dir = Split-Path -Parent $PSScriptRoot
$log = Join-Path $dir 'console.log'
$start = 0
if (Test-Path $log) { $start = (Get-Item $log).Length }

$p = Start-Process -FilePath "$dir\Tribes.exe" -ArgumentList "-mod lakrabbit -mod laktest -dedicated $Mission" -WorkingDirectory $dir -WindowStyle Hidden -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) {
    Stop-Process -Id $p.Id -Force
    Write-Output "LAKTEST TIMEOUT: server killed after $TimeoutSec s"
}

$fs = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
$fs.Seek($start, 'Begin') | Out-Null
$text = (New-Object IO.StreamReader($fs)).ReadToEnd()
$fs.Close()
$lines = $text -split "`r?`n"
# Show the test results and the lines that can show a script problem.
$lines | Where-Object { $_ -match 'LAKTEST|LakRabbit hit|rror|nknown command|Syntax|unable to find|not found|::.*: Unknown' }
$done = $lines | Where-Object { $_ -match 'LAKTEST DONE pass=\d+ fail=0' }
if ($done) { exit 0 } else { exit 1 }
