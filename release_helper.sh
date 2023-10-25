#!/bin/bash
tag=$(git describe --tags --abbrev=0)
rm *.zip
dotnet clean
rm -rf bin/
# building release
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained

(cd  bin/Release/net6.0/win-x64/publish && rm knxunlocker.pdb  && zip -9 "../../../../../knxunlocker_win_$tag.zip" *)
(cd  bin/Release/net6.0/linux-x64/publish && rm knxunlocker.pdb && zip -9 "../../../../../knxunlocker_linux_$tag.zip" *)
