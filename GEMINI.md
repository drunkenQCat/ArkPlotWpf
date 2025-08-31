# Gemini Project Context: ArkPlot

## Project Overview

This is a .NET 9 WPF application named ArkPlot, designed to generate "play scripts" from the mobile game Arknights' story data. The application fetches story text from a public GitHub repository, parses it using regular expressions, and formats it into Markdown and HTML files. It features a graphical user interface built with WPF and follows the MVVM (Model-View-ViewModel) design pattern. The application supports multiple languages and allows for custom parsing rules through a `tags.json` file. It also includes functionality to download related game assets.

### Core Technologies

- **.NET 9 & C#:** The application is built on the latest .NET framework.
- **WPF:** The user interface is built using Windows Presentation Foundation.
- **MVVM:** The project follows the MVVM architectural pattern, using the `CommunityToolkit.Mvvm` library for implementation.
- **SQLite & SqlSugar:** The application uses a local SQLite database for data storage, with SqlSugar as the ORM for data access.
- **Newtonsoft.Json:** Used for JSON serialization and deserialization, particularly for parsing the `tags.json` file.
- **HandyControl:** A UI library for WPF that provides additional controls and styling.
- **Markdig:** A Markdown processor for .NET.

### Architecture

The application is structured into several layers:

- **View:** The user interface, defined in XAML files (`.xaml`).
- **ViewModel:** The presentation logic, which connects the View to the Model. `MainViewModel.cs` is the primary view model.
- **Model:** The data structures and business logic. This includes classes for representing game data, such as `Plot`, `ActInfo`, and `PrtsData`.
- **Data:** The data access layer, which includes the `DatabaseContext` for interacting with the SQLite database.
- **Utilities:** A collection of helper classes for tasks such as network requests, data processing, and file I/O.
- **Services:** Services for cross-cutting concerns like notifications and window management.

## Building and Running

To build and run this project, you will need the .NET 9 SDK installed.

1. **Restore Dependencies:** Open a terminal in the root directory and run `dotnet restore`.
2. **Build the Project:** Run `dotnet build`.
3. **Run the Application:** The executable will be located in the `ArkPlotWpf\bin\Debug\net9.0-windows` directory.

### Testing

The solution includes a test project, `ArkPlotWpf.DbTests`. To run the tests, execute `dotnet test` in the root directory.

## Development Conventions

- **MVVM:** The project strictly follows the MVVM pattern. All UI logic should be in the View, presentation logic in the ViewModel, and business logic in the Model.
- **Asynchronous Programming:** The application makes extensive use of `async` and `await` for I/O-bound operations, such as network requests and file access.
- **Dependency Injection:** While not explicitly configured in a DI container, the application uses a form of service location with the `NotificationBlock` and `DatabaseContext` singletons.
- **Data Binding:** The View and ViewModel are connected via data binding, as is standard in WPF and MVVM.
- **Regular Expressions:** The core parsing logic relies on regular expressions defined in the `tags.json` file.

## Avalonia Development

### Creating `.axaml` Files

You can create `.axaml` files (for Windows, UserControls, etc.) using `dotnet` commands after installing the Avalonia UI templates.

1. **Install Avalonia Templates:**
    If you haven't already, you need to install the templates for Avalonia. This command will add templates for creating new projects, windows, user controls, and more.

    ```powershell
    dotnet new install Avalonia.Templates
    ```

2. **Create a New Control:**
    These commands must be run from within an Avalonia project directory (e.g., `ArkPlot.Avalonia`).

    - **To create a new Window:**

      ```powershell
      dotnet new avalonia.window -n MyNewWindow
      ```

      This creates `MyNewWindow.axaml` and `MyNewWindow.axaml.cs`.

    - **To create a new User Control:**

      ```powershell
      dotnet new avalonia.usercontrol -n MyUserControl
      ```

      This creates `MyUserControl.axaml` and `MyUserControl.axaml.cs`.

> [!CAUTION]
> the terminal env is powershell, please take care of what you run.

