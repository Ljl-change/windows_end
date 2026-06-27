param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("capture", "validate")]
    [string]$Action,
    [int]$TabIndex = 0,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not ("UiNative" -as [type])) {
    Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class UiNative {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint="SendMessage")] public static extern IntPtr SendMessageRect(IntPtr hWnd, uint msg, IntPtr wParam, ref RECT rect);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
"@
}

function Get-WindowTextValue([IntPtr]$Handle) {
    $builder = New-Object Text.StringBuilder 512
    [UiNative]::GetWindowText($Handle, $builder, $builder.Capacity) | Out-Null
    return $builder.ToString()
}

function Get-ClassNameValue([IntPtr]$Handle) {
    $builder = New-Object Text.StringBuilder 256
    [UiNative]::GetClassName($Handle, $builder, $builder.Capacity) | Out-Null
    return $builder.ToString()
}

function Get-ChildHandles([IntPtr]$Parent) {
    $result = New-Object Collections.Generic.List[IntPtr]
    $callback = [UiNative+EnumWindowsProc]{
        param([IntPtr]$handle, [IntPtr]$state)
        $result.Add($handle)
        return $true
    }
    [UiNative]::EnumChildWindows($Parent, $callback, [IntPtr]::Zero) | Out-Null
    return $result
}

function Select-Tab([IntPtr]$MainWindow, [int]$Index) {
    $tab = Get-ChildHandles $MainWindow |
        Where-Object { (Get-ClassNameValue $_) -like "*SysTabControl32*" } |
        Select-Object -First 1
    if (-not $tab) { throw "TabControl handle not found" }

    $rect = New-Object UiNative+RECT
    $ok = [UiNative]::SendMessageRect($tab, 0x130A, [IntPtr]$Index, [ref]$rect)
    if ($ok -eq [IntPtr]::Zero) { throw "Cannot obtain tab rectangle for index $Index" }
    $x = [int](($rect.Left + $rect.Right) / 2)
    $y = [int](($rect.Top + $rect.Bottom) / 2)
    $point = [IntPtr](($y -shl 16) -bor ($x -band 0xffff))
    [UiNative]::PostMessage($tab, 0x0201, [IntPtr]1, $point) | Out-Null
    [UiNative]::PostMessage($tab, 0x0202, [IntPtr]0, $point) | Out-Null
    Start-Sleep -Milliseconds 700
}

function Save-WindowImage([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object UiNative+RECT
    [UiNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object Drawing.Bitmap $width, $height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    try {
        if (-not [UiNative]::PrintWindow($Handle, $hdc, 2)) { throw "PrintWindow failed" }
    }
    finally {
        $graphics.ReleaseHdc($hdc)
    }
    $directory = Split-Path -Parent $Path
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Find-Dialog([uint32]$ProcessId) {
    $found = [IntPtr]::Zero
    $callback = [UiNative+EnumWindowsProc]{
        param([IntPtr]$handle, [IntPtr]$state)
        $pid = 0
        [UiNative]::GetWindowThreadProcessId($handle, [ref]$pid) | Out-Null
        if ($pid -eq $ProcessId -and (Get-ClassNameValue $handle) -eq "#32770") {
            $script:found = $handle
            return $false
        }
        return $true
    }
    [UiNative]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    return $script:found
}

$testRun = Join-Path $PSScriptRoot "..\WindowsFormsApp_3\bin\TestRun"
$exe = (Resolve-Path (Join-Path $testRun "ImageBatchSystem.exe")).Path
$process = Start-Process -FilePath $exe -WorkingDirectory $testRun -WindowStyle Normal -PassThru

try {
    $null = $process.WaitForInputIdle(10000)
    Start-Sleep -Seconds 2
    $process.Refresh()
    if ($process.MainWindowHandle -eq 0) { throw "Main window not created" }

    if ($Action -eq "capture") {
        Select-Tab $process.MainWindowHandle $TabIndex
        Save-WindowImage $process.MainWindowHandle $OutputPath
        Write-Output "PASS|UI_CAPTURE|tab=$TabIndex|path=$OutputPath"
    }
    else {
        Select-Tab $process.MainWindowHandle 1
        $submit = Get-ChildHandles $process.MainWindowHandle |
            Where-Object { (Get-WindowTextValue $_) -eq "提交工单" } |
            Select-Object -First 1
        if (-not $submit) { throw "Submit button not found" }

        [UiNative]::SendMessage($submit, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 500
        $dialog = Find-Dialog $process.Id
        if ($dialog -eq [IntPtr]::Zero) { throw "Title validation dialog not found" }
        Save-WindowImage $dialog (Join-Path $OutputPath "TC-02-title-required.png")
        [UiNative]::SendMessage($dialog, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null

        $edit = Get-ChildHandles $process.MainWindowHandle |
            Where-Object { [UiNative]::IsWindowVisible($_) -and (Get-ClassNameValue $_) -like "*EDIT*" } |
            Select-Object -First 1
        if (-not $edit) { throw "Title textbox not found" }
        $titlePointer = [Runtime.InteropServices.Marshal]::StringToHGlobalUni("TDD_输入校验")
        try { [UiNative]::SendMessage($edit, 0x000C, [IntPtr]::Zero, $titlePointer) | Out-Null }
        finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($titlePointer) }

        [UiNative]::SendMessage($submit, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 500
        $dialog = Find-Dialog $process.Id
        if ($dialog -eq [IntPtr]::Zero) { throw "Image validation dialog not found" }
        Save-WindowImage $dialog (Join-Path $OutputPath "TC-02-image-required.png")
        [UiNative]::SendMessage($dialog, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null

        Write-Output "PASS|VALIDATION|titleRequired=true|imageRequired=true"
    }
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
