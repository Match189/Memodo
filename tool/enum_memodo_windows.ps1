Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr h, StringBuilder t, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
"@
$list = New-Object System.Collections.Generic.List[string]
$cb = [W+EnumProc] {
    param($h, $l)
    $procId = 0
    [W]::GetWindowThreadProcessId($h, [ref]$procId) | Out-Null
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($proc -and $proc.ProcessName -eq "memodo") {
        $len = [W]::GetWindowTextLength($h)
        if ($len -gt 0) {
            $sb = New-Object System.Text.StringBuilder ($len + 1)
            [W]::GetWindowText($h, $sb, $len + 1) | Out-Null
            $list.Add("pid=$procId title='$($sb.ToString())'")
        }
    }
    return $true
}
[W]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$list | Sort-Object
