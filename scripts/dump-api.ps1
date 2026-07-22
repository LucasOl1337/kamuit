$dll = "C:\Users\user\.nuget\packages\easywindowsterminalcontrol\1.0.38\lib\net6.0-windows7.0\EasyWindowsTerminalControl.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
foreach ($typeName in @("EasyWindowsTerminalControl.TermPTY", "EasyWindowsTerminalControl.EasyTerminalControl")) {
    $t = $asm.GetType($typeName)
    if (-not $t) { Write-Output "TIPO NAO ACHADO: $typeName"; continue }
    Write-Output "=== $typeName ==="
    Write-Output "Interfaces: $($t.GetInterfaces().Name -join ', ')"
    $t.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly) |
        ForEach-Object { "  M: $($_.Name)($($_.GetParameters().ForEach({$_.ParameterType.Name}) -join ', ')) -> $($_.ReturnType.Name)" }
    $t.GetProperties() | ForEach-Object { "  P: $($_.Name): $($_.PropertyType.Name)" }
    $t.GetEvents() | ForEach-Object { "  E: $($_.Name)" }
}
