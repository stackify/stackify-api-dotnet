function Remove-SignCode {
    param (
        $ASMFile
    )
    Set-Content -Path $ASMFile -Value (Get-Content -Path $ASMFile | Select-String -Pattern AssemblyKeyFileAttribute -NotMatch )
}

$files = @(Get-ChildItem -Path . -Directory -Filter Stackify*)

foreach ($file in $files) {
    $asmInfo = Get-ChildItem -Path $file/Properties/AssemblyInfo.cs
    Remove-SignCode -ASMFile $asmInfo
}