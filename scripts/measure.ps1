<#
.SYNOPSIS
Memory, CPU time and context switches of a running app and every process it
spawned, sampled over a window of time.

.DESCRIPTION
Counting the whole process tree is what makes the comparison fair: a WebView2
app spends most of its memory in msedgewebview2.exe children, not in its own
exe. Context switches count wakeups, which CPU time rounds away: an idle dock
should show zero of both.

.EXAMPLE
./scripts/measure.ps1 -Name Karpo -Seconds 60

.EXAMPLE
./scripts/measure.ps1 -Name "Aero Dock" -Seconds 60
#>
param(
    [Parameter(Mandatory)] [string] $Name,
    [int] $Seconds = 30
)

$ErrorActionPreference = 'Stop'

$all = @(Get-CimInstance Win32_Process)
$roots = @($all | Where-Object { $_.Name -ieq "$Name.exe" })
if ($roots.Count -eq 0) { throw "No running process named $Name.exe" }

$ids = [System.Collections.Generic.HashSet[uint32]]::new()
$queue = [System.Collections.Generic.Queue[uint32]]::new()
foreach ($root in $roots) {
    [void]$ids.Add($root.ProcessId)
    $queue.Enqueue($root.ProcessId)
}
while ($queue.Count -gt 0) {
    $parent = $queue.Dequeue()
    foreach ($child in $all | Where-Object { $_.ParentProcessId -eq $parent }) {
        if ($ids.Add($child.ProcessId)) { $queue.Enqueue($child.ProcessId) }
    }
}

function Measure-Tree {
    $cpu = @{}
    foreach ($id in $ids) {
        $process = Get-Process -Id $id -ErrorAction SilentlyContinue
        if ($process) { $cpu[$id] = $process.TotalProcessorTime.TotalMilliseconds }
    }
    # The raw perf class counts every switch since the thread started, and its
    # name is not localised, unlike the counter paths Get-Counter wants.
    # Keyed per thread: summing per process goes negative when a pool thread
    # exits between the two samples and takes its count with it.
    $switches = @{}
    $filter = ($ids | ForEach-Object { "IDProcess = $_" }) -join ' OR '
    try {
        foreach ($thread in Get-CimInstance Win32_PerfRawData_PerfProc_Thread -Filter $filter) {
            if ($thread.Name -like '*_Total*') { continue }
            $switches["$($thread.IDProcess)/$($thread.IDThread)"] = [uint64]$thread.ContextSwitchesPersec
        }
    } catch {
        $switches = $null
    }
    @{ Cpu = $cpu; Switches = $switches }
}

# Switches in the window for one process: threads that exited in between are
# lost, threads that started count from zero.
function Get-Switched($id) {
    if ($null -eq $before.Switches -or $null -eq $after.Switches) { return 'n/a' }
    $total = [int64]0
    foreach ($key in $after.Switches.Keys) {
        if (-not $key.StartsWith("$id/")) { continue }
        $start = if ($before.Switches.ContainsKey($key)) { $before.Switches[$key] } else { 0 }
        $total += [int64]$after.Switches[$key] - [int64]$start
    }
    $total
}

$before = Measure-Tree
Start-Sleep -Seconds $Seconds
$after = Measure-Tree

$rows = foreach ($id in $ids) {
    $process = Get-Process -Id $id -ErrorAction SilentlyContinue
    if (-not $process -or -not $before.Cpu.ContainsKey($id)) { continue }
    $switched = Get-Switched $id
    [pscustomobject]@{
        Process          = $process.ProcessName
        Id               = $id
        'Private MB'     = [math]::Round($process.PrivateMemorySize64 / 1MB, 1)
        'Working set MB' = [math]::Round($process.WorkingSet64 / 1MB, 1)
        'CPU ms'         = [math]::Round($after.Cpu[$id] - $before.Cpu[$id], 1)
        'Switches'       = $switched
    }
}

$rows | Sort-Object 'Private MB' -Descending | Format-Table -AutoSize | Out-String -Width 200
$switchTotal = if (@($rows | Where-Object { $_.Switches -is [string] }).Count -gt 0) { 'n/a' } else { ($rows | Measure-Object Switches -Sum).Sum }
'{0} process(es) over {1} s: {2:N1} MB private, {3:N1} MB working set, {4:N1} ms CPU, {5} context switches' -f `
    @($rows).Count, $Seconds,
    ($rows | Measure-Object 'Private MB' -Sum).Sum,
    ($rows | Measure-Object 'Working set MB' -Sum).Sum,
    ($rows | Measure-Object 'CPU ms' -Sum).Sum,
    $switchTotal
