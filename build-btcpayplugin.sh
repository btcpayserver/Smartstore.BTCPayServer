#!/bin/bash

# Get the directory of the currently running script
StartPath=$(dirname "$0")

# Print the StartPath
echo "$StartPath"

# Build the project
dotnet build "$StartPath/src/Smartstore.Modules/Smartstore.BTCPayServer/Smartstore.BTCPayServer.csproj" -c Release

# Print the path to the PackagerCLI project
echo "$StartPath/tools/Smartstore.PackagerCLI/Smartstore.PackagerCLI.csproj"

# Run the PackagerCLI project
dotnet run --project "$StartPath/tools/Smartstore.PackagerCLI/Smartstore.PackagerCLI.csproj" "$StartPath/src/Smartstore.Web/Modules/Smartstore.BTCPayServer" "$StartPath/build/packages"
