# Interface media

The README images and animations were captured from the current English frontend
on 31 October 2026. The source baseline is v0.3.56, with the accompanying copy and
localization corrections from this documentation update.

| File | Content |
| --- | --- |
| `deadeye-library.jpg` | Fullscreen library with covers and a video preview |
| `deadeye-library.gif` | Library selection and actual preview playback |
| `deadeye-library.mp4` | Video version of the library walkthrough |
| `deadeye-details.jpg` | Blue Estate selected with its screenshot preview |
| `deadeye-gun-studio.jpg` | RS3 control illustration and action bindings |
| `deadeye-firing-range.jpg` | Built-in barrel targets and player control panel |

These are captures of the running browser preview, using a curated external media
selection. Device detection is shown as disconnected. No physical gun input,
tracking, recoil, calibration or game compatibility is certified by these images.
The browser firing range uses its mouse demonstration mode.

Games and original media are not distributed with the application. Artwork,
trailers and game screenshots belong to their respective owners. The Blue Estate
image is used to demonstrate the screenshot fallback in the library.

## Refreshing captures

Use the English interface and real rendered UI. Keep media selections, original
assets, local paths, credentials and hardware diagnostic fixtures outside Git.

The optional `scripts/record-readme.cjs` helper records a local frontend preview
with Playwright. It requires a running preview server and `DEADEYE_SHOWCASE`, the
path to an external JSON array. Each entry supplies `title`, `platform`, optional
`aspect`, and a `media` object with local `cover`, `previewVideo`, `screenshot` and
`logo` paths. The helper's navigation sequence expects the demonstration titles
named in the script. `REAPER_PREVIEW_URL` overrides its default local URL.

The helper writes still images here and WebM recordings under the ignored
`work/readme-recordings` directory. Encode GIF and MP4 versions separately and
check the resulting playback, English labels and relative README links before
publishing. Keep a visible distinction between browser previews and native
hardware tests.
