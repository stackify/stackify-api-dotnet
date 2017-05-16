$revision = @{ $true = $env:APPVEYOR_BUILD_NUMBER; $false = 1 }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$revision = "{0:D4}" -f [convert]::ToInt32($revision, 10)

dotnet restore .\Src

dotnet build .\Src\StackifyLib -c Release
dotnet build .\Src\StackifyLib.AspNetCore -c Release
dotnet build .\Src\StackifyLib.CoreLogger -c Release
#dotnet build .\Src\StackifyLib.ELMAH -c Release
dotnet build .\Src\StackifyLib.log4net -c Release
#dotnet build .\Src\StackifyLib.log4net.Sitecore -c Release
#dotnet build .\Src\StackifyLib.log4net.Tests -c Release
#dotnet build .\Src\StackifyLib.log4net.v1_2_10 -c Release
#dotnet build .\Src\StackifyLib.nlog -c Release
#dotnet build .\Src\StackifyLib.NoWeb -c Release
dotnet build .\Src\StackifyLib.StackifyTraceListener -c Release

Write-Output "APPVEYOR_REPO_TAG: $env:APPVEYOR_REPO_TAG"
Write-Output "VERSION-SUFFIX: alpha1-$revision"

If($env:APPVEYOR_REPO_TAG -eq $true) {
    Write-Output "RUNNING dotnet pack .\Src\StackifyLib -c Release -o .\artifacts "
    dotnet pack .\Src\StackifyLib -c Release -o .\artifacts 
}
Else { 
    Write-Output "RUNNING dotnet pack .\Src\StackifyLib -c Release -o .\artifacts --version-suffix=alpha1-$revision"
    dotnet pack .\Src\StackifyLib -c Release -o .\artifacts --version-suffix=beta1-$revision 
}