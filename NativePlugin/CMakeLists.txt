cmake_minimum_required(VERSION 3.20)
project(QuadBufferPlugin LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Where the built DLL is copied so Unity picks it up.
set(QUADBUFFER_PACKAGE_PLUGIN_DIR
    "${CMAKE_CURRENT_SOURCE_DIR}/../Packages/com.xra.quadbuffer/Runtime/Plugins/x86_64"
    CACHE PATH "Destination folder for the built plugin inside the Unity package")

add_library(QuadBufferPlugin SHARED
    src/QuadBufferPlugin.cpp
    src/D3D11StereoPresenter.cpp
    src/D3D11StereoPresenter.h
    src/Log.cpp
    src/Log.h
    include/QuadBufferPlugin.h
)

target_include_directories(QuadBufferPlugin PRIVATE
    include
    external/UnityPluginAPI
    src
)

target_compile_definitions(QuadBufferPlugin PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(QuadBufferPlugin PRIVATE d3d11 dxgi)

if(MSVC)
    target_compile_options(QuadBufferPlugin PRIVATE /W4 /permissive-)
endif()

add_custom_command(TARGET QuadBufferPlugin POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${QUADBUFFER_PACKAGE_PLUGIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "$<TARGET_FILE:QuadBufferPlugin>" "${QUADBUFFER_PACKAGE_PLUGIN_DIR}"
    COMMENT "Copying QuadBufferPlugin.dll into the Unity package"
)
