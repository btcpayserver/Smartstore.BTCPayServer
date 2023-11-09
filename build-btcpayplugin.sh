#!/bin/bash

# Get the absolute directory path of the currently running script
StartPath=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)

# Print the StartPath
echo "$StartPath"

# Build the project
dotnet build "$StartPath/src/Smartstore.Modules/Smartstore.BTCPayServer/Smartstore.BTCPayServer.csproj" -c Release

# Print the path to the PackagerCLI project
echo "$StartPath/tools/Smartstore.PackagerCLI/Smartstore.PackagerCLI.csproj"

# Run the PackagerCLI project
dotnet run --project "$StartPath/tools/Smartstore.PackagerCLI/Smartstore.PackagerCLI.csproj" "$StartPath/src/Smartstore.Web/Modules/Smartstore.BTCPayServer" "$StartPath/build/packages"
