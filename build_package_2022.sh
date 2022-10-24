
dotnet msbuild VS4Mac_Meadow_Extension/Meadow.Sdks.IdeExtensions.Vs4Mac.2022.csproj	 /t:Restore /p:Configuration=Release
dotnet msbuild VS4Mac_Meadow_Extension/Meadow.Sdks.IdeExtensions.Vs4Mac.2022.csproj	 /t:Build /p:Configuration=Release /p:CreatePackage=true

