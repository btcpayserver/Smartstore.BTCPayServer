$StartPath = Split-Path -Parent $PSCommandPath
echo "$StartPath"
dotnet build  $StartPath/src/Smartstore.Modules/Smartstore.BTCPayServer/Smartstore.BTCPayServer.csproj -c Release
echo "$StartPath/tools/Smartstore.PackagerCLI/Smartstore.PackagerCLI.csproj"
dotnet run --project $StartPath/tools/Smartstore.PackagerCLI/Smartstore.PackagerCLI.csproj "$StartPath/src/Smartstore.Web/Modules/Smartstore.BTCPayServer" "$StartPath/build/packages"