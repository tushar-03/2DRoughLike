# Playing and hosting the web build

The game runs in a browser (WebGL 2) on desktop and on phones and tablets, with keyboard and mouse, gamepad, or on-screen touch controls. Progress is saved in the browser (IndexedDB), so it stays on the player's device and needs no server or account.

## Build it

In Unity: **Roughlike > Build > WebGL (for hosting)**. It switches the editor to WebGL, builds into `Builds/WebGL`, and switches back (the first build re-imports textures for WebGL and takes a while).

Or from a terminal (editor closed):

```bash
Unity -batchmode -quit -projectPath . -executeMethod Roughlike.Editor.WebBuild.BuildFromCommandLine -logFile build.log
```

The output folder contains `index.html` (our own page template: loading screen, fullscreen button, "rotate your device" prompt, no accidental scrolling or zooming), the `Build/` files (Brotli compressed, hashed names) and a `vercel.json`.

## Try it locally first

```bash
python tools/serve-web.py
```

Open <http://localhost:8080>. The script sends the same headers Vercel will. To test on a phone, open `http://<your computer's address>:8080` on the same Wi-Fi.

## Deploy to Vercel (free)

```bash
npm i -g vercel
cd Builds/WebGL
vercel --prod
```

Answer the prompts (no framework, no build command, output directory `.`). Vercel gives you a URL like `https://drowned-reliquary.vercel.app`. The included `vercel.json` sets the `Content-Encoding` and `Content-Type` headers that Brotli files need and caches the hashed files for a year. Any other static host (Netlify, GitHub Pages with a custom header rule, Cloudflare Pages, itch.io) works too if it serves the same headers; if a host cannot set them, build with compression set to **Disabled** in `WebBuild.ConfigurePlayerSettings`.

Size limits: Vercel's free plan allows files up to 100 MB each; the build is far below that.

## What is saved, and where

Settings (`settings.json`) and the current run (`run.json`) live in the browser's IndexedDB for your site's address, via Unity's persistent data path. Each save is flushed right after it is written (`WebBridge.SyncFileSystem`). Clearing site data, using a private window, or opening the game on a different address starts fresh. There is no cloud save.

## Mobile notes

- Touch controls appear automatically on touch devices (Settings > Gameplay > Touch controls: Automatic, Always on, Off). They drive a virtual gamepad, so your gamepad bindings apply.
- Vibration: Android browsers use `navigator.vibrate`. **iOS Safari has no vibration API**; the page fires the small system tick that iOS 17.4+ plays for a switch control on every touch, but hits and parries cannot vibrate on iPhone. Real gamepads rumble everywhere the browser exposes them.
- Fullscreen is available from the corner button (not on iPhone Safari; use *Add to Home Screen* for a chrome-free window).
- The game wants landscape; a prompt asks you to rotate in portrait.
- Phones are slower than desktops. If it stutters, turn Quality down in Settings > Video.

## Known limits

- First load downloads the whole game (it is cached afterwards).
- Audio does not exist yet (planned for M10).
- Resolution and display mode are controlled by the browser, so those settings are replaced by a Fullscreen toggle.
