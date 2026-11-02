dotnet publish Flow.Launcher.Plugin.Ghq -c Release -r win-x64 --no-self-contained
Compress-Archive -Path "Flow.Launcher.Plugin.Ghq/bin/Release/win-x64/publish/*" -DestinationPath "Flow.Launcher.Plugin.Ghq/bin/Ghq.zip" -Force
