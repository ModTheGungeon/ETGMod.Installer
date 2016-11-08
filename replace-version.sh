#!/bin/bash
if ! TAG=`git describe --exact-match --tags 2>/dev/null`; then
  echo "This commit is not a tag so not replacing version"
  exit 0
fi
VERSION=${TAG#"v"}
perl -0777 -pi -e 's/\[assembly: AssemblyVersion\(".*"\)\]/\[assembly: AssemblyVersion\("'${VERSION}'"\)\]/gm' ./Properties/AssemblyInfo.cs
