$ErrorActionPreference = "Stop"

$revision = @{ $true = $env:APPVEYOR_BUILD_NUMBER; $false = 1 }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$revision = "{0:D4}" -f [convert]::ToInt32($revision, 10)

dotnet restore

Write-Output "Building StackifyLib"
dotnet build .\Src\StackifyLib -c Release
if ($lastexitcode -ne 0)
{
	throw "dotnet build failed"
}

Write-Output "Building StackifyLib"
dotnet build .\Src\StackifyLib -c Release
if ($lastexitcode -ne 0)
{
	throw "dotnet build failed"
}

Write-Output "Building NLog.Targets.Stackify"
dotnet build .\Src\NLog.Targets.Stackify -c Release
if ($lastexitcode -ne 0)
{
	throw "dotnet build failed"
}

# Write-Output "Building StackifyLib.ELMAH"
# dotnet build .\Src\StackifyLib.ELMAH -c Release

Write-Output "Building StackifyLib.log4net"
dotnet build .\Src\StackifyLib.log4net -c Release
if ($lastexitcode -ne 0)
{
	throw "dotnet build failed"
}

# Write-Output "Building StackifyLib.log4net.Sitecore"
# dotnet build .\Src\StackifyLib.log4net.Sitecore -c Release

# Write-Output "Building StackifyLib.log4net.Tests"
# dotnet build .\Src\StackifyLib.log4net.Tests -c Release

# Write-Output "Building StackifyLib.log4net.v1_2_10"
# dotnet build .\Src\StackifyLib.log4net.v1_2_10 -c Release

# Write-Output "Building StackifyLib.nlog"
# dotnet build .\Src\StackifyLib.nlog -c Release

# Write-Output "Building StackifyLib.NoWeb"
# dotnet build .\Src\StackifyLib.NoWeb -c Release

Write-Output "Building StackifyLib.StackifyTraceListener"
dotnet build .\Src\StackifyLib.StackifyTraceListener -c Release
if ($lastexitcode -ne 0)
{
	throw "dotnet build failed"
}

Write-Output "Testing StackifyLib"
dotnet restore .\test\StackifyLibTests\StackifyLibTests.csproj
dotnet test .\test\StackifyLibTests\StackifyLibTests.csproj
if ($lastexitcode -ne 0)
{
	throw "dotnet test failed"
}

Write-Output "APPVEYOR_REPO_TAG: $env:APPVEYOR_REPO_TAG"
Write-Output "VERSION-SUFFIX: alpha1-$revision"

If($env:APPVEYOR_REPO_TAG -eq $true) {
    Write-Output "RUNNING dotnet pack .\Src\StackifyLib -c Release -o .\artifacts "
    dotnet pack .\Src\StackifyLib -c Release -o .\artifacts
	if ($lastexitcode -ne 0)
	{
		throw "dotnet pack failed"
	}
    dotnet pack .\Src\StackifyLib.log4net -c Release -o .\artifacts 
	if ($lastexitcode -ne 0)
	{
		throw "dotnet pack failed"
	}
}
Else {  # publish pre-release if it does not have a tag
    Write-Output "RUNNING dotnet pack .\Src\StackifyLib -c Release -o .\artifacts --version-suffix=beta1-$revision"
    dotnet pack .\Src\StackifyLib -c Release -o .\artifacts --version-suffix=beta1-$revision
	if ($lastexitcode -ne 0)
	{
		throw "dotnet pack failed"
	}
    dotnet pack .\Src\StackifyLib.log4net -c Release -o .\artifacts --version-suffix=beta1-$revision
	if ($lastexitcode -ne 0)
	{
		throw "dotnet pack failed"
	}
}