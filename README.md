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

## Menus and toolbar

**File menu**

| Command | Action |
|---------|--------|
| **Save** (Ctrl+S) | Export everything on the **current tab** as one image (PNG / JPEG / BMP) |
| **Copy** (Ctrl+C) | Copy the current tab composite to the clipboard (paste with Ctrl+V) |
| **Exit** | Close the application |

**Layout (top → bottom)**

1. **File** menu  
2. **Tabs** (dark pill chrome)  
3. **New Screenshot** / zoom toolbar  
4. Drawing toolbar  
5. Canvas  

**Toolbar** (under the tabs)

| Control | Action |
|--------|--------|
| **New Screenshot** | Minimize the app, show a full-screen dimmed overlay, drag to capture a region |
| **Copy** | Copy the current tab composite to the clipboard (Ctrl+C) |
| **−** / **100%** / **+** | Zoom out, reset to 100%, or zoom in the **selected** screenshot |

**Tabs** (light pill chrome)

| Control | Action |
|---------|--------|
| Circular **+** (after the last tab) | Open a new empty tab |
| Circular **×** on a tab | Close that tab (last tab is cleared instead of removed) |
| **Double-click** a tab title | Rename the tab |

**Keyboard**

| Shortcut | Action |
|----------|--------|
| **Ctrl+S** | Save current tab |
| **Ctrl+C** | Copy current tab to the clipboard |
| **Ctrl+Z** | Undo (draw, move, resize, zoom, pan, add, delete) |
| **Ctrl+Y** or **Ctrl+Shift+Z** | Redo |
| **Delete** or **Backspace** | Delete the selected screenshot |
| **Shift + mouse wheel** (over a screenshot) | Zoom that screenshot |
| **Ctrl + mouse wheel** (over a zoomed image) | Pan the zoomed image (vertical / side-tilt) |
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

- The first snip lands near the top-left of the **visible** canvas.
- Later snips go **to the right** of the previous one when their left edge still fits in the **current window width**.
- If the window is narrow (or the next snip would start off-screen to the right), it wraps to a **new row below** so you can see it without horizontal scrolling.
- The image may extend past the right edge; only the start of the snip is kept on-screen.

---

## Tabs

- Each tab has its own set of screenshots and drawing settings.
- Click the **+** tab to create a new tab. Names use the count: `Tab 1`, `Tab 2`, …  
  Closing a tab reuses the next free number (e.g. close Tab 2 → next new tab is Tab 2 again).
- **Double-click** a tab title to **rename** it.
- Click **×** on a tab to close it. The last remaining tab is cleared instead of removed.

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
- **Shift + mouse wheel** over a screenshot zooms it (under the cursor).
- **Double-click** a screenshot to reset zoom to **100%**.
- **Ctrl + wheel** (up/down or side-tilt) pans inside a **zoomed** image.
- **Shift + drag** (Pointer / when not drawing) or **middle-mouse drag** also pans when zoomed.

### Delete

- Select a screenshot → **Delete** or **Backspace**.

### Scroll the canvas

- **Wheel up/down** — vertical form scroll (even over a screenshot), unless **Shift** or **Ctrl** is held.
- **Side-tilt wheel** — horizontal form scroll (even over a screenshot), unless **Ctrl** pans a zoomed image.
- **Shift + wheel** over a screenshot — zoom that image.
- **Ctrl + wheel** over a zoomed screenshot — pan that image.
- **Shift + drag** while drawing still locks a horizontal stroke.
- First click on an inactive window both **activates** the app and performs the click (no double-click needed).

---

## Drawing toolbar (under the tab headers)

```
[ Pointer ] [ Draw ] | [ Highlighter ▼ ] Color  Opacity  Size
```

| Control | Action |
|--------|--------|
| **Pointer** | Move / resize screenshots; select and edit annotations |
| **Draw** | Enter draw mode with the tool selected in the dropdown |
| **Tool dropdown** | **Highlighter**, **Pen**, **Blur**, **Rectangle**, **Arrow**, or **Text** |
| **Color** | Open a color picker |
| **Opacity** | Transparency of new ink strokes (20%–100%; shapes/text are opaque) |
| **Size** | Stroke thickness, or font size when **Text** is selected |

### Tools

| Tool | Create | Edit (Pointer) |
|------|--------|----------------|
| **Highlighter / Pen** | Freehand drag | — |
| **Blur** | Freehand drag (redacts / softens under the brush) | — |
| **Rectangle** | Drag out a box | Move; drag border/corners to resize |
| **Arrow** | Drag from start → tip | Move; drag endpoints |
| **Text** | Click → enter text in a dialog | Drag to move; change Font size/color in the strip |

Each tool **remembers** its own color and size when you switch back and forth. With an annotation selected in **Pointer**, changing color/size updates that annotation.

### Drawing tips

- Click **Draw**, pick a tool, then drag (or click for Text) on a screenshot.
- Switch to **Pointer** and click a drawing (pen, highlighter, blur, rectangle, arrow, or text) to select it; the toolbar loads its tool/**color**/**size**. Drag freehand strokes to **move** them. **Delete** removes the selected drawing (otherwise Delete removes the screenshot). **Ctrl+Z** undoes drawings and other edits.
- **Rectangle:** drag the **inside** to move (four-way cursor); drag **corners/edges** to resize (diagonal/axis cursors). Change Color/Size on the strip to restyle the selection.
- **Right-click** a screenshot (or empty canvas) to jump back to the **Pointer** tool.
- **Ctrl + drag** still **moves** the screenshot while a draw tool is active.
- **Shift + draw** locks a pen/highlighter stroke to a **straight horizontal** line.
- You can hold **Shift** mid-stroke to snap the rest of the line horizontal.
- Edge/corner **resize** still works while Draw is selected.
- Drawings are included when you **File → Save**.

---

## Saving

1. Arrange screenshots and drawings on the current tab as you want them.
2. **File → Save** or **Ctrl+S**.
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
- **File → Save** often if you’re building a collage of multiple snips + annotations.
- Prefer **PNG** for sharp UI captures; use **JPEG** for smaller photos/screens.
- Large multi-monitor snips are full resolution; use zoom and scroll to navigate.

---

## Project layout

| Path | Role |
|------|------|
| `ScreenShotter/` | WinForms app (VB.NET, `net9.0-windows`) |
| `ScreenShotter/app.ico` | Application icon (exe + window) |
| `ScreenShotter/Assets/app-icon.png` | Source PNG for the icon |
| `ScreenShotter.Tests/` | Unit tests |
| `.gitignore` | Ignores build output, `.vs`, publish profiles, etc. |
