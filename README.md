# WpfXamlToPng

A command-line tool to render WPF XAML files to PNG images. This tool is useful for generating documentation, preview images, or automated UI testing screenshots from XAML source files without running the full application.

## Features

- **XAML Rendering**: Converts standard WPF Window/Control XAML to PNG.
- **Resources Support** (`-r`): Load and apply external resource dictionaries (e.g., `App.xaml`).
- **Mock Data Context** (`-c`): Inject JSON data as a ViewModel for data binding.
- **Tab Generation** (`--tabs`): Automatically generate separate screenshots for each tab in a TabControl.
- **Scenario Testing** (`-s`): Define complex rendering scenarios using a YAML configuration file.
- **Batch Processing** (`-b`): Process all XAML files in a directory.

## Usage

```cmd
XamlToPngRenderer.exe [options] <input.xaml> <output.png>
```

### Options

| Option | Description |
|--------|-------------|
| `-w`, `--width` | Force render width in pixels (overrides XAML). |
| `-h`, `--height` | Force render height in pixels (overrides XAML). |
| `-d`, `--dpi` | DPI for rendering (default: 96). |
| `-v`, `--verbose` | Enable verbose debug logging. |
| `-r`, `--resources` | Path to `App.xaml` or a `ResourceDictionary` to merge. |
| `-c`, `--context` | Path to a JSON file to use as `DataContext`. |
| `-t`, `--tabs` | Generate a separate PNG for each `TabItem` found. |
| `-s`, `--scenario` | Run scenarios defined in a YAML file (input/output arguments ignored). |
| `-b`, `--batch` | Batch process a directory of XAML files. |

## Examples

### Basic Rendering
```cmd
XamlToPngRenderer.exe MainWindow.xaml output.png
```

### Using Resources (Styles, Brushes)
Render a window that relies on styles defined in `App.xaml`:
```cmd
XamlToPngRenderer.exe -r App.xaml MainWindow.xaml output.png
```

### Mocking Data Binding
Render with data populated from a JSON file:
```cmd
XamlToPngRenderer.exe -c mockdata.json MainWindow.xaml output.png
```

**mockdata.json**:
```json
{
  "Title": "Hello World",
  "IsConnected": true,
  "Items": [
    { "Name": "Item 1", "Value": 100 },
    { "Name": "Item 2", "Value": 200 }
  ]
}
```

### Generating Tab Screenshots
Generate `output_Tab0_Name.png`, `output_Tab1_Settings.png`, etc.:
```cmd
XamlToPngRenderer.exe --tabs MainWindow.xaml output.png
```

### Scenario-based Testing
Run multiple defined scenarios:
```cmd
XamlToPngRenderer.exe -s scenarios.yaml
```

**scenarios.yaml**:
```yaml
output_dir: output/
base_resources: App.xaml

scenarios:
  - name: disconnected
    input: MainWindow.xaml
    context_overrides:
      IsConnected: false
      
  - name: connected
    input: MainWindow.xaml
    context_overrides:
      IsConnected: true
```

### Batch Processing
Render all `.xaml` files in `Views/` to `output/`:
```cmd
XamlToPngRenderer.exe -b Views/ -r App.xaml output/
```

## Requirements

- .NET Framework 4.8
- Windows OS (WPF dependency)

## Limitations

- Code-behind logic is stripped and ignored.
- Only supports basic primitive types and nested objects/lists for JSON data binding.
- Complex behaviors (animations, triggers requiring interaction) are not simulated.