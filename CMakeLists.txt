# Файл сборки для проекта GCodeCorrector

cmake_minimum_required(VERSION 3.10)
set(CMAKE_MODULE_PATH ${CMAKE_MODULE_PATH} "${CMAKE_SOURCE_DIR}/YBuild")
include(YBuild)
if("${Y_SYSTEM}" STREQUAL "3RDPARTY")
    project("3rdparty")
else()
    project("GCodeCorrector")
endif()

YBUILD_FIND_MSBUILD()

YBUILD_CONFIGURE_BUILD()

if("${Y_SYSTEM}" STREQUAL "3RDPARTY")
    YBUILD_ADD_3RDPARTY_COMPONENTS()
else("${Y_SYSTEM}" STREQUAL "3RDPARTY")
    add_subdirectory(GCodeCorrector)
    add_subdirectory(YLocalization)
    add_subdirectory(YMugenExtensions)
    add_subdirectory(install)
endif("${Y_SYSTEM}" STREQUAL "3RDPARTY")
