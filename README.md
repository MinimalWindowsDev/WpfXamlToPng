# WpfXamlToPng

A command-line tool to render WPF XAML Windows to PNG images for documentation purposes.

## Build

```cmd
cd XamlToPngRenderer
dotnet build -c Release
```

## Usage

```cmd
XamlToPngRenderer.exe [options] <input.xaml> <output.png>

Options:
  -v, --verbose     Enable verbose debug logging
  -w, --width N     Render width in pixels (default: from XAML)
  -h, --height N    Render height in pixels (default: from XAML)
  -d, --dpi N       DPI for rendering (default: 96)
```

## License

CC BY-NC-SA 4.0