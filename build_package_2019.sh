mv "/Applications/Visual Studio.app" "/Applications/Visual Studio 2022.app"
mv "/Applications/Visual Studio 2019.app" "/Applications/Visual Studio.app"

msbuild VS4Mac_Meadow_Extension.sln /t:Restore /p:Configuration=Release
msbuild VS4Mac_Meadow_Extension.sln /t:Build /p:Configuration=Release /p:CreatePackage=true

mv "/Applications/Visual Studio.app" "/Applications/Visual Studio 2019.app"
mv "/Applications/Visual Studio 2022.app" "/Applications/Visual Studio.app"
