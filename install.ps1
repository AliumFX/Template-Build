<#
.SYNOPSIS
This is a Powershell script install Alium build toolchain.
.DESCRIPTION
This Powershell script will download and install an appropriate 
version of .NET Core based on the project requirements 
(typically global.json)
.PARAMETER DotNetCoreVersion
The build script to execute.
#>

[CmdletBinding()]
Param(
    [string]$DotNetSdkVersion
)

$HasDotNetCore = $FALSE;
$HasGlobalJson = $FALSE;
$Install = $FALSE;

# Firstly, we need to determine if .NET Core is installed at all
if ((dir (Get-Command dotnet).Path.Replace('dotnet.exe', 'sdk')).Name) {
    Write-Host "A version of .NET Core is installed";
    $HasDotNetCore = $TRUE;
} else {
    Write-Host "No version of .NET Core is installed";
    $Install = $TRUE;
}

if ($HasDotNetCore) {
    if (!($DotNetSdkVersion)) {
        $HasGlobalJson = (Test-Path ./global.json);
        if ($HasGlobalJson) {
            Write-Host "Found ./global.json";

            $globalJson = (ConvertFrom-Json (Get-Content ./global.json -Raw));
            $DotNetSdkVersion = $globalJson.sdk.version;

            if ($DotNetSdkVersion) {
                Write-Host "Required SDK version: $DotNetSdkVersion";

                Rename-Item ./global.json ./global.json.bak;
                $DotNetSdkVersionInstalled = (dotnet --version);
                Rename-Item ./global.json.bak ./global.json;
                Write-Host "Installed SDK version: $DotNetSdkVersionInstalled";

                $Install = ($DotNetSdkVersionInstalled -lt $DotNetSdkVersion);
            } else {
                Write-Host "Required SDK version: Not specified";
                $Install = $FALSE;
            }
        } else {
            Write-Host "Could not find ./global.json";
            $Install = $FALSE;
        }
    } else {
        # Using a specific version provided by CLI
        $Install = $TRUE;
    }
}

$InstallPath = (Join-Path (Resolve-Path ".") ".dotnetsdk");

if (Test-Path $InstallPath) {
    if (Test-Path (Join-Path $InstallPath "dotnet.exe")) {
        Write-Host "Will use pre-existing SDK installed to: $InsallPath";
        $Install = $FALSE;

        # Set the DOTNET_INSTALL_DIR envvar to our local path
        $env:DOTNET_INSTALL_DIR = $InstallPath;
        # Update the PATH
        $env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"
    }
}

if ($Install) {
    Write-Host "Will install .NET Core SDK $DotNetSdkVersion";

    $InstallPath = (Join-Path (Resolve-Path ".") ".dotnetsdk");

    $RemoteUrl = "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/$DotNetSdkVersion/dotnet-sdk-$DotNetSdkVersion-win-x64.zip";
    Write-Host "Installing .NET Core from $RemoteUrl";

    New-Item -Type Directory $InstallPath -Force | Out-Null;
    (New-Object System.Net.WebClient).DownloadFile($RemoteUrl, "dotnet.zip");

    Write-Host "Extracting to $InstallPath";
    Add-Type -AssemblyName System.IO.Compression.FileSystem;
    [System.IO.Compression.ZipFile]::ExtractToDirectory("dotnet.zip", $InstallPath);

    Write-Host "Installed .NET Core $DotNetSdkVersion";

    # Set the DOTNET_INSTALL_DIR envvar to our local path
    $env:DOTNET_INSTALL_DIR = $InstallPath;
    # Update the PATH
    $env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"
}