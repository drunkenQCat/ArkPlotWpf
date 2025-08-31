dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=false /p:TrimUnusedDependencies=true
# IncludeAllContentForSelfExtract=false：不要把所有文件保留下来，只生成单文件可执行。
#
# TrimUnusedDependencies=true：开启 IL Linker，删除未使用的程序集和依赖（可以大幅减小体积）。
#
# 保持 --self-contained false，这样不带 .NET Runtime，只要目标机器有 .NET 安装即可。
cd .\bin\Release\net9.0\win-x64\publish\ArkPlot.Avalonia.exe

# dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=false /p:TrimUnusedDependencies=true
# --self-contained true：把 .NET Runtime 打包进去，单文件就能运行。
#
# dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=false /p:TrimUnusedDependencies=true /p:PublishAot=true
# /p:PublishAot=true：启用 AOT 编译。
# --self-contained true：必须自包含才能做 AOT（因为需要编译完整运行时）。
