#!/usr/bin/env bash

scriptPath="$( cd "$(dirname "$0")" ; pwd -P )"
_CMakeBuildType=Debug
_CMakeEnableBitcode=
_OutputPathPrefix=
_CMakeBuildTarget=veldrid-spirv
_CMakeOsxArchitectures=
_CMakeGenerator=
_CMakeExtraBuildArgs=
_OSDir=

while :; do
    if [ $# -le 0 ]; then
        break
    fi

    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        debug|-debug)
            _CMakeBuildType=Debug
            ;;
        release|-release)
            _CMakeBuildType=Release
            ;;
        osx)
            _CMakeOsxArchitectures=$2
            _OSDir=osx
            shift
            ;;
        linux-x64)
            _OSDir=linux-x64
            ;;
        linux-arm64)
            _OSDir=linux-arm64
            ;;
        ios)
            _CMakeEnableBitcode=-DENABLE_BITCODE=0
            _CMakeBuildTarget=veldrid-spirv
            _CMakeGenerator="-G Xcode"
            _CMakeExtraBuildArgs="--config Release"
            _OSDir=ios
            ;;
        *)
            __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
    esac

    shift
done

_OutputPath=$scriptPath/build/$_CMakeBuildType/$_OSDir
_PythonExePath=$(which python3)
if [[ $_PythonExePath == "" ]]; then
    echo Build failed: could not locate python executable.
    exit 1
fi

mkdir -p $_OutputPath
pushd $_OutputPath

if [[ $_OSDir == "ios" ]]; then
    mkdir -p device-build
    pushd device-build

    cmake ../../../.. -DIOS=ON -DCMAKE_BUILD_TYPE=$_CMakeBuildType $_CMakeGenerator -DPLATFORM=OS64 -DDEPLOYMENT_TARGET=13.4 $_CMakeEnableBitcode -DPYTHON_EXECUTABLE=$_PythonExePath -DCMAKE_OSX_ARCHITECTURES="$_CMakeOsxArchitectures"
    cmake --build . --target $_CMakeBuildTarget $_CMakeExtraBuildArgs

    popd

    mkdir -p simulator-build-arm64
    pushd simulator-build-arm64

    cmake ../../../.. -DIOS=ON -DCMAKE_BUILD_TYPE=$_CMakeBuildType $_CMakeGenerator -DPLATFORM=SIMULATORARM64 -DDEPLOYMENT_TARGET=13.4 $_CMakeEnableBitcode -DPYTHON_EXECUTABLE=$_PythonExePath -DCMAKE_OSX_ARCHITECTURES="$_CMakeOsxArchitectures"
    cmake --build . --target $_CMakeBuildTarget $_CMakeExtraBuildArgs

    popd

    mkdir -p simulator-build-x64
    pushd simulator-build-x64

    cmake ../../../.. -DIOS=ON -DCMAKE_BUILD_TYPE=$_CMakeBuildType $_CMakeGenerator -DPLATFORM=SIMULATOR64 -DDEPLOYMENT_TARGET=13.4 $_CMakeEnableBitcode -DPYTHON_EXECUTABLE=$_PythonExePath -DCMAKE_OSX_ARCHITECTURES="$_CMakeOsxArchitectures"
    cmake --build . --target $_CMakeBuildTarget $_CMakeExtraBuildArgs

    popd

    mkdir -p simulator-build-combined/veldrid-spirv.framework

    cp ./simulator-build-arm64/Release-iphonesimulator/veldrid-spirv.framework/Info.plist ./simulator-build-combined/veldrid-spirv.framework/Info.plist

    lipo -create \
	    ./simulator-build-arm64/Release-iphonesimulator/veldrid-spirv.framework/veldrid-spirv \
	    ./simulator-build-x64/Release-iphonesimulator/veldrid-spirv.framework/veldrid-spirv \
	 -output ./simulator-build-combined/veldrid-spirv.framework/veldrid-spirv

    xcodebuild -create-xcframework \
	    -framework ./device-build/Release-iphoneos/veldrid-spirv.framework \
	    -framework ./simulator-build-combined/veldrid-spirv.framework \
	    -output ./veldrid-spirv.xcframework
else
    cmake ../../.. -DCMAKE_BUILD_TYPE=$_CMakeBuildType $_CMakeGenerator $_CMakeEnableBitcode -DPYTHON_EXECUTABLE=$_PythonExePath -DCMAKE_OSX_ARCHITECTURES="$_CMakeOsxArchitectures"
    cmake --build . --target $_CMakeBuildTarget $_CMakeExtraBuildArgs
fi

popd
