[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)

# ClipTrayPro

ClipTrayPro is a small tray app for Windows and macOS that makes copied paths, links, and text a little easier to work with.

It sits quietly in the system tray/menu bar. Copy a file path, folder path, or web address, then open the tray menu to act on it.

## Features

- Open copied files, folders, and web addresses.
- Reveal copied files or folders in Explorer or Finder.
- Remove rich-text formatting from copied text.
- Clear the clipboard manually.
- Optionally clear the clipboard automatically one minute after new content is copied.

## How It Works

Copy something useful, then click the ClipTrayPro tray icon.

If the clipboard contains a file, folder, or web address, the first menu item changes to open it directly. The next item reveals its location when that makes sense. Tooltips show the full path plus a little extra detail, such as file size or folder contents.

The auto-clear option is off by default. When enabled, ClipTrayPro waits one minute after clipboard content changes, then clears it. If the clipboard changes again during that minute, the timer starts over.

## Platforms

ClipTrayPro is intended for Windows and macOS.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
