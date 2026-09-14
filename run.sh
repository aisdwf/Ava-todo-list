#!/bin/bash
set -e

# 设置 .NET 本地环境变量
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export NUGET_SCRATCH="$HOME/.nuget/scratch"
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
export MSBUILDDISABLENODEREUSE=1

# 定位到脚本所在的项目根目录
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

# 启动 FlowTask 桌面客户端
dotnet run --project src/FlowTask.Desktop/FlowTask.Desktop.csproj "$@"
