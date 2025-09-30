# --- Function Definitions ---
# These must come first so they are available when the script calls them

function Get-NestedPropertyValue {
    param (
        [Parameter(Mandatory=$true)]$Object,
        [Parameter(Mandatory=$true)][string]$Path
    )
    $pathParts = $Path.Split('.')
    $currentObject = $Object
    foreach ($part in $pathParts) {
        if ($currentObject -eq $null) {
            return $null
        }
        $currentObject = $currentObject.$part
    }
    return $currentObject
}

function Set-NestedPropertyValue {
    param (
        [Parameter(Mandatory=$true)]$Object,
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)]$Value
    )
    $pathParts = $Path.Split('.')
    $currentObject = $Object
    
    # Traverse to the parent of the target property
    for ($i = 0; $i -lt $pathParts.Length - 1; $i++) {
        $currentObject = $currentObject.$($pathParts[$i])
    }
    
    # Set the value on the final property
    $currentObject.$($pathParts[-1]) = $Value
}

function Sync-AppsettingValue {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string]$WorkingDir,
        [Parameter(Mandatory=$true)]
        [string]$ApiKeyObjectPath,
        [int]$Depth = 10
    )
    
    Write-Host "Syncing value from appsettings.Development.json to appsettings.json..."
    
    try {
        # Define file paths
        $devFilePath = Join-Path -Path $WorkingDir -ChildPath "appsettings.Development.json"
        $mainFilePath = Join-Path -Path $WorkingDir -ChildPath "appsettings.json"
        
        # Read and convert JSON files
        $devSettings = Get-Content -Raw -Path $devFilePath | ConvertFrom-Json
        $mainSettings = Get-Content -Raw -Path $mainFilePath | ConvertFrom-Json
        
        # Get the value from the development settings
        $value = Get-NestedPropertyValue -Object $devSettings -Path $ApiKeyObjectPath
        
        # Check if the value was found
        if ($null -eq $value) {
            Write-Warning "Could not find value at path '$ApiKeyObjectPath' in appsettings.Development.json."
            throw
        }
        
        # Set the value in the main settings object
        Set-NestedPropertyValue -Object $mainSettings -Path $ApiKeyObjectPath -Value $value
        
        # Convert back to JSON and save
        $updatedJson = $mainSettings | ConvertTo-Json -Depth $Depth
        $updatedJson | Set-Content -Path $mainFilePath

        Remove-Item $devFilePath
        Write-Host "Successfully synced value for '$ApiKeyObjectPath'."
        
    } catch {
        Write-Error "An error occurred: $_"
        throw
    }
}


# --- Main Script ---

# CONFIGURATION
$WorkingDir = "$pwd\build-output"
$ProjectDir = Resolve-Path "$PWD\..\TollCents.Api"
$BuildCommand = "dotnet publish --configuration Release --runtime linux-x64 --self-contained true -o $WorkingDir"
$RemoteUser = "tollcents"
$RemoteHost = "tollcents.com"
$RemotePath = "/var/www/tollcents/api"
$ServiceName = "tollcents"
$ApiKeyObjectPath = "Integrations.GoogleMaps.ApiKey"
$StartingPWD = $pwd

# STEP 1: Build locally
Write-Host "🔧 Building project..."
if (Test-Path $WorkingDir) {
    Remove-Item $WorkingDir -Recurse
}
cd $ProjectDir
Invoke-Expression $BuildCommand
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed. Exiting." -ForegroundColor Red
    exit 1
}

cd $ProjectDir
Write-Host "Configure appsettings.json"
Sync-AppsettingValue -WorkingDir $WorkingDir -ApiKeyObjectPath $ApiKeyObjectPath

Write-Host "Starting SSH agent"
Start-Service ssh-agent

$ZipFileName = "build.tar"
$RemoteTempDir = "$($RemotePath)_temp_$(Get-Date -Format yyyyMMddHHmmss)"

Write-Host "🗜️ Compressing working directory to $ZipFileName..."
Push-Location $WorkingDir
tar -czf "$ZipFileName" ./*
Pop-Location

Write-Host "📦 Copying zipped build to server..."
ssh "${RemoteUser}@${RemoteHost}" "mkdir -p $RemoteTempDir"
scp "$WorkingDir\$ZipFileName" "${RemoteUser}@${RemoteHost}:${RemoteTempDir}/"

Write-Host "🚀 Executing atomic deployment on server..."
$remoteCommands = "cd $RemoteTempDir; tar -xzf $ZipFileName; rm $ZipFileName; rm -rf $RemotePath; cd ..; mv $RemoteTempDir $RemotePath; sudo systemctl restart $ServiceName"
ssh "${RemoteUser}@${RemoteHost}" $remoteCommands


Write-Host "🧹 Cleaning up local machine..."
Remove-Item $WorkingDir -Recurse
Stop-Service ssh-agent
cd $StartingPWD
Write-Host "✅ Deployment complete!"