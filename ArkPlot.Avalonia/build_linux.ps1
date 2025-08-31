dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
# -c Release：Release 模式
#
# -r linux-x64：目标运行时（根据你的 Linux 系统选择 x64 或 arm64）
#
# --self-contained true：包含 .NET 运行时，不依赖系统已装 .NET
#
# /p:PublishSingleFile=true：打包成单个可执行文件
#
# /p:IncludeAllContentForSelfExtract=true：确保资源文件也包含
cd ./bin/Release/net9.0/linux-x64/publish
