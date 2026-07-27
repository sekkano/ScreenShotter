# Screen Shotter

A Windows screenshot app (similar to Snip & Sketch) with multi-monitor capture, free-moving screenshots on a canvas, tabs, drawing tools, and tab export.

## Requirements

- **Windows** 10 or 11  
- **.NET 9** Desktop runtime **or** a self-contained published build (see [Building / publishing](#building--publishing))

## Quick start

1. Run `ScreenShotter.exe` (from Visual Studio **F5**, or a published folder).
2. Click **New Screenshot**.
3. Drag a rectangle on the dimmed screen (crosshair cursor).
4. The snip appears on the current tab — move it, draw on it, or capture more.

---

## Main toolbar

| Control | Action |
|--------|--------|
| **New Screenshot** | Minimize the app, show a full-screen dimmed overlay, drag to capture a region |
| **Save Tab** | Export everything on the **current tab** as one image (PNG / JPEG / BMP) |
| **New Tab** | Open a new empty tab |
| **Close Tab** | Close the active tab (last tab is cleared instead of removed) |
| **−** / **100%** / **+** | Zoom out, reset to 100%, or zoom in the **selected** screenshot |

**Keyboard**

| Shortcut | Action |
|----------|--------|
| **Ctrl+S** | Save Tab |
| **Delete** or **Backspace** | Delete the selected screenshot |
| **Ctrl+mouse wheel** | Zoom the screenshot under the cursor |
| **Esc** or **right-click** | Cancel capture (while snipping) |

---

## Capturing screenshots

1. Click **New Screenshot** (or use it after arranging your desktop).
2. The main window minimizes; every monitor is covered by a dim overlay.
3. Drag with the **left mouse button** to select a rectangle (any direction).
4. Release to capture. The app restores and places the image on the active tab.
5. **Esc** or **right-click** cancels.

### Multi-monitor

- Capture works across **all monitors**.
- You can drag a region that spans more than one display.

### Where new snips appear

- The first snip lands near the top-left of the canvas.
- Later snips are placed **to the right** of the previous one.
- If the row would get too wide, the next snip starts on a **new row below**.

---

## Tabs

- Each tab has its own set of screenshots and drawing settings.
- **New Tab** names tabs by count: `Tab 1`, `Tab 2`, …  
  Closing a tab reuses the next free number (e.g. close Tab 2 → next new tab is Tab 2 again).
- **Double-click** a tab title to **rename** it.
- **Close Tab** on the last remaining tab clears its content instead of removing the tab.

---

## Working with screenshots on the canvas

### Select

- Click a screenshot to select it (blue border and corner grips).

### Move

| Mode | How |
|------|-----|
| **Pointer** mode | Drag the body of the screenshot |
| **Any mode** | Hold **Ctrl** and drag (cursor becomes the move icon) |

### Resize

- Drag **edges** for free stretch (width/height independent).
- Drag **corners** to resize while keeping aspect ratio.

### Zoom

- Select a screenshot, then use toolbar **+** / **−** / **100%**.
- Or **Ctrl + mouse wheel** over the image (frame size stays the same; content zooms).
- **Double-click** a screenshot to reset zoom to **100%**.
- **Shift + drag** (Pointer / when not drawing) pans inside a zoomed image.
- **Middle-mouse drag** also pans when zoomed.

### Delete

- Select a screenshot → **Delete** or **Backspace**.

### Scroll the canvas

- **Mouse wheel up/down** — always vertical scroll (does **not** zoom unless **Ctrl** is held).
- **Side-tilt / horizontal wheel** — horizontal scroll only.
- **Shift** does not change scroll direction (Shift+draw still locks a horizontal stroke).
- First click on an inactive window both **activates** the app and performs the click (no double-click needed).

---

## Drawing toolbar (under the tab headers)

```
[ Pointer ] [ Draw ] | [ Highlighter ▼ ] Color  Opacity  Size
```

| Control | Action |
|--------|--------|
| **Pointer** | Move / resize screenshots |
| **Draw** | Enter draw mode with the tool selected in the dropdown |
| **Tool dropdown** | **Highlighter** or **Pen** |
| **Color** | Open a color picker |
| **Opacity** | Transparency of new strokes (20%–100%) |
| **Size** | Stroke thickness |

### Highlighter vs Pen

| | Highlighter | Pen |
|--|-------------|-----|
| Default look | Wide, translucent yellow | Thin, solid dark |
| Typical use | Mark areas | Write / outline |

Each tool **remembers** its own color, opacity, and size when you switch back and forth.

### Drawing tips

- Click **Draw**, pick a tool, then drag on a screenshot.
- **Ctrl + drag** still **moves** the screenshot while a draw tool is active.
- **Shift + draw** locks the stroke to a **straight horizontal** line.
- You can hold **Shift** mid-stroke to snap the rest of the line horizontal.
- Edge/corner **resize** still works while Draw is selected.
- Drawings are included when you **Save Tab**.

---

## Saving

1. Arrange screenshots and drawings on the current tab as you want them.
2. **Save Tab** or **Ctrl+S**.
3. Choose PNG, JPEG, or BMP.

The file is a **composite** of the tab:

- Positions and sizes  
- Overlap / stacking order (what’s on top on screen is on top in the file)  
- Zoom/pan as displayed  
- Highlighter and pen strokes  

Empty tabs cannot be saved (you’ll get a short message).

---

## Building / publishing

### Run from source (development)

```bat
cd ScreenShotter
dotnet build ScreenShotter.sln -c Debug
dotnet run --project ScreenShotter.vbproj
```

Or open `ScreenShotter\ScreenShotter.sln` in Visual Studio and press **F5**.

### Publish a portable app (recommended for daily use)

Self-contained (no separate .NET install on the target PC):

```bat
cd ScreenShotter
dotnet publish ScreenShotter.vbproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o %USERPROFILE%\Desktop\ScreenShotterApp
```

Then run `ScreenShotterApp\ScreenShotter.exe`.

Smaller build (requires [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)):

```bat
dotnet publish ScreenShotter.vbproj -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true ^
  -o %USERPROFILE%\Desktop\ScreenShotterApp
```

Visual Studio: right-click the project → **Publish** → Folder, choose self-contained / win-x64 as needed.

---

## Tests

```bat
cd ScreenShotter
dotnet test ScreenShotter.sln -c Debug
```

---

## Tips

- Use **tabs** to keep unrelated snips separate (e.g. one tab per task).
- **Save Tab** often if you’re building a collage of multiple snips + annotations.
- Prefer **PNG** for sharp UI captures; use **JPEG** for smaller photos/screens.
- Large multi-monitor snips are full resolution; use zoom and scroll to navigate.

---

## Project layout

| Path | Role |
|------|------|
| `ScreenShotter/` | WinForms app (VB.NET, `net9.0-windows`) |
| `ScreenShotter.Tests/` | Unit tests |
| `.gitignore` | Ignores build output, `.vs`, publish profiles, etc. |
