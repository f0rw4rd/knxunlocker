#!/bin/bash
tag=$(git describe --tags --abbrev=0)
# building release
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained

(cd  bin/Release/net6.0/win-x64/publish  && zip "../../../../../knxunlocker_win_$tag.zip" *)
(cd  bin/Release/net6.0/linux-x64/publish  && zip "../../../../../knxunlocker_linux_$tag.zip" *)
