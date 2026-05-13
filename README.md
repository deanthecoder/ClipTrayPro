[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)

# ClipTrayPro

ClipTrayPro is a small tray app for Windows and macOS that makes copied paths, links, text, and images a little easier to work with.

It sits quietly in the system tray/menu bar. Copy a file path, folder path, web address, text, or image, then open the tray menu to act on it.

## Features

- Open copied files, folders, web addresses, and plain text.
- Reveal copied files or folders in Explorer or Finder.
- Open copied images in the default image viewer.
- Save copied images as PNG or JPEG.
- Remove rich-text formatting from copied text.
- Compare the last two copied text values with your preferred diff tool.
- Clear the clipboard manually.
- Optionally clear the clipboard automatically one minute after new content is copied.

## How It Works

Copy something useful, then click the ClipTrayPro tray icon.

If the clipboard contains a file, folder, web address, general text, or image, the first menu item changes to open it directly. Plain text opens in the default text viewer. The next item reveals a file or folder location when that makes sense. Tooltips show useful details, such as file size, folder contents, text previews, or image dimensions.

Image-only actions appear only when an image is on the clipboard. Text-only actions are hidden while working with an image.

The auto-clear option is off by default. When enabled, ClipTrayPro waits one minute after clipboard content changes, then clears it. If the clipboard changes again during that minute, the timer starts over.

## Comparing Text

Open **Settings** from the tray menu to choose a diff app and command line. Use `$1` and `$2` as placeholders for the two temporary text files.

Once configured, copy two different pieces of text. The **Compare Text** menu item will open them in your chosen diff tool.

## Platforms

ClipTrayPro is intended for Windows and macOS.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
