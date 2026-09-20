# FlowTask application icon

`flowtask-icon.svg` is the editable source. `flowtask-icon.ico` and
`flowtask-icon.png` are generated from it; do not edit them independently.

The desktop project previously had no application or window icon configuration,
so the brand SVG was never used by the executable or either window. The project
now uses the ICO as `ApplicationIcon` and embeds that same file as an
`AvaloniaResource`. Both windows reference `/Assets/Brand/flowtask-icon.ico`.
The SVG design and other UI icons are unchanged.

macOS does not inherit the Dock icon from `Window.Icon`. Rider and `dotnet run`
launch without an app bundle, so `MacOSApplicationIcon` sets AppKit's
`NSApplication.applicationIconImage` after Avalonia initializes the native
platform. It loads the 1024px PNG copied to the build/publish output, using
`AppContext.BaseDirectory` rather than the working directory. The OS guard
keeps native interop out of Windows startup. Allocated native objects are
released after AppKit retains the image.

## Regeneration

From the repository root, with Node.js, Sharp 0.35.4 and Python with Pillow 12.3.0
available, run:

```sh
node <<'JS'
const sharp = require('sharp');
sharp('src/FlowTask.Desktop/Assets/Brand/flowtask-icon.svg', { density: 288 })
  .resize(1024, 1024)
  .png()
  .toFile('src/FlowTask.Desktop/Assets/Brand/flowtask-icon.png')
  .catch(error => { console.error(error); process.exit(1); });
JS

python3 <<'PY'
from PIL import Image
with Image.open('src/FlowTask.Desktop/Assets/Brand/flowtask-icon.png') as image:
    image.save(
        'src/FlowTask.Desktop/Assets/Brand/flowtask-icon.ico',
        sizes=[(size, size) for size in (16, 24, 32, 48, 64, 128, 256)],
    )
PY
```

These tools are needed only when regenerating the checked-in ICO, not for
normal .NET builds.

## Verification — 2026-09-17

- Implementation authorized by the owner as a `simple` task.
- `dotnet build FlowTask.sln -v q --nologo`: passed, 0 warnings / 0 errors.
- `dotnet test FlowTask.sln --nologo -v q`: passed, 184 tests, 0 failures / skips.
- ICO validation: all seven frames decode; transparent padding is preserved
  (16px corner alpha is 1/255 from resampling). Multisize preview inspected.
- Built desktop assembly: exact ICO bytes and resource path are embedded.
- Windows Release publish: passed. PE resource inspection confirmed one icon
  group containing all seven frames, byte-for-byte identical to the source ICO.
- `git diff --check`: passed.
- Owner manual verification: pending.
- After macOS integration: build passed with 0 warnings / 0 errors; all 184
  tests passed. The 1024px PNG matches the build output byte-for-byte.
- macOS launch via `./run.sh --no-build`: no startup error was printed before
  the diagnostic process was stopped. This alone does not verify the UI.
- Dock visual inspection: inconclusive; the UI tool timed out for Dock and
  could not capture the unbundled application. Rider verification remains
  with the owner; no visual pass is claimed.
- Windows visual verification: deferred by the owner until a later Windows
  build is available; the earlier PE inspection does not replace that check.
- Commit: waiting for owner verification and commit instruction.

The checks above used the current working tree, including pre-existing changes
from other tasks; those changes are not part of the icon integration.

Windows verification build command (run from the repository root):

```sh
dotnet publish src/FlowTask.Desktop/FlowTask.Desktop.csproj \
  -c Release -r win-x64 --self-contained false \
  -o /tmp/flowtask-icon-integration/win-x64 --nologo -v minimal
```

This verification build requires .NET 8 on Windows. Copy the whole output
directory, not just the EXE. Distribution packaging is outside this change.

For manual verification, start the application on Windows and inspect its
taskbar / Alt+Tab icon and the published executable in File Explorer. Open and
close the quick-capture window to confirm that loading its icon does not cause
an error; that window remains hidden from the taskbar by design.

On macOS, fully stop the previous process and run the desktop project in Rider,
or use `./run.sh`. Verify that the main window opens and its running Dock item
uses the FlowTask icon. This configures the running process, not a Finder icon
for a distributable `.app` bundle; app-bundle packaging remains outside scope.
