#!/bin/bash

if [ -f 'msbuild/MSBuild.exe' ]; then
	echo 'MSBuild already existing.'
else
	echo 'Unzipping MSBuild...'
	unzip msbuild.zip
fi

LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$(dirname $0)/msbuild-lib

echo 'Building...'
#./msbuild/corerun ./msbuild/MSBuild.exe /p:Configuration=Release /p:TravisCore=true

