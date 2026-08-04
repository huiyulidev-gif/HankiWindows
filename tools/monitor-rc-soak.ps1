param(
    [Parameter(Mandatory = $true)]
    [int] $TargetPid,
    [Parameter(Mandatory = $true)]
    [string] $CsvPath,
    [int] $Samples = 60
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class RcGuiResources {
    [DllImport("user32.dll")]
    public static extern uint GetGuiResources(IntPtr hProcess, uint flags);
}
"@

'timestamp,pid,working_set_bytes,private_bytes,cpu_total_seconds,handles,threads,gdi_objects,user_objects' |
    Set-Content -Encoding utf8 -LiteralPath $CsvPath

for ($index = 0; $index -lt $Samples; $index++) {
    $process = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
    if (-not $process) {
        break
    }

    try {
        $gdi = [RcGuiResources]::GetGuiResources($process.Handle, 0)
        $user = [RcGuiResources]::GetGuiResources($process.Handle, 1)
        $cpu = $process.TotalProcessorTime.TotalSeconds
        $line = '{0},{1},{2},{3},{4},{5},{6},{7},{8}' -f `
            (Get-Date).ToString('o'),
            $process.Id,
            $process.WorkingSet64,
            $process.PrivateMemorySize64,
            ([math]::Round($cpu, 3)),
            $process.HandleCount,
            $process.Threads.Count,
            $gdi,
            $user
        Add-Content -Encoding utf8 -LiteralPath $CsvPath -Value $line
    }
    catch {
        # A process can exit between the snapshot and GetGuiResources; the next sample
        # still records the terminal state through process absence.
    }

    Start-Sleep -Seconds 60
}
