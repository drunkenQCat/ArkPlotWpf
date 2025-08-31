# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ArkPlotWpf** is a .NET WPF application that generates markdown/HTML files from Arknights game story data. It parses JSON story files from Kengxxiao/ArknightsGameData and converts them into readable formats using regex-based tag processing.

## Architecture

### Core Components

- **WPF UI Layer**: MainWindow.xaml with MainViewModel.cs using MVVM pattern with CommunityToolkit.Mvvm
- **Data Layer**: Repository pattern with SqlSugar ORM for database operations
- **Processing Pipeline**: 
  - `PrtsDataProcessor`: Downloads and processes PRTS data
  - `TagProcessor`: Handles regex-based tag replacement
  - `AkpParser`: Main workflow orchestrator
- **Storage**: SQLite database with entities for Acts, Plots, and FormattedTextEntries

### Key Directories

- `ArkPlotWpf/` - Main WPF application
- `ArkPlotWpf.DbTests/` - Unit tests for database operations
- `ArkPlotWpf/Data/` - Database context, repositories, and database service
- `ArkPlotWpf/Model/` - Entity classes (Plot, ActInfo, FormattedTextEntry, etc.)
- `ArkPlotWpf/Utilities/` - Core processing components
- `ArkPlotWpf/ViewModel/` - MVVM view models
- `ArkPlotWpf/View/` - WPF views

## Build Commands

```bash
# Build the main project
dotnet build ArkPlotWpf/ArkPlotWpf.csproj

# Run tests
dotnet test ArkPlotWpf.DbTests/ArkPlotWpf.DbTests.csproj

# Publish for Windows
dotnet publish ArkPlotWpf/ArkPlotWpf.csproj -c Release -r win-x64 --self-contained true

# Run the application
dotnet run --project ArkPlotWpf/ArkPlotWpf.csproj
```

## Development Setup

### Prerequisites
- .NET 9.0 SDK
- Windows OS (WPF requirement)

### Key Configuration Files
- `tags.json` - Tag definitions for regex processing (copied to output during build)
- `assets/head.html` - HTML template header
- `assets/tail.html` - HTML template footer with JavaScript

## Database Integration

The project is transitioning from file-based storage to a SQLite database using SqlSugar ORM:

- **Database Context**: `DatabaseContext.cs` in Data folder
- **Repositories**: BaseRepository pattern with specific repositories for PRTS data
- **Migration**: `DatabaseMigration.cs` handles schema updates
- **Test Database**: Unit tests use in-memory SQLite database

## Key Workflows

1. **Story Processing**: PRTS data → Tag processing → Markdown/HTML generation
2. **Database Operations**: Repository pattern with async CRUD operations
3. **UI Updates**: MVVM with CommunityToolkit.Mvvm for property change notifications

## Testing

Unit tests use xUnit framework and focus on database operations. Tests use in-memory SQLite database to avoid file system dependencies.