# Playnite - Game Update Status

A [Playnite](https://playnite.link/) extension that displays the update status of installed Steam games directly in Playnite's Desktop theme.

## Features

Game Update Status displays a small status indicator next to games in Playnite:

| Indicator | Status           |
| --------- | ---------------- |
| 🟢        | Up to date       |
| 🟠        | Update available |
| ⚪        | Unknown          |

Games that are not installed do not display an indicator.

The extension currently supports **Steam games**.

## How it works

The extension checks the local Steam installation data against the current public build information available from Steam.

For each installed Steam game, it compares:

* the locally installed build ID;
* the current public build ID.

The result is cached locally to avoid performing unnecessary checks.

By default, the cache is refreshed every **30 minutes**.

## Requirements

* [Playnite](https://playnite.link/) **10.x**
* Windows
* Steam
* The **Game Update Status** Desktop theme

The extension is currently designed for **Playnite 10** and is not intended for Playnite 11.

## Installation

Download the latest release from the [GitHub Releases](https://github.com/) page.

Each release contains two files:

```text
GameUpdateStatus_<version>.pext
GameUpdateStatus_<version>.pthm
```

### 1. Install the extension and the Theme

1. Double-clic on the `.pext` file, the extension will be installed on Playnite, and it will restart
2. Double-clic on the `.pthm` file, the extension will be installed on Playnite, and it will restart

The extension and theme are designed to work together.

## Updating

When a new version is released, install the new `.pext` and `.pthm` files.

The extension and theme use the same version number and are released together.
The plugin performs the update checks and exposes the status to the theme. The Desktop theme is responsible for displaying the status indicator.


## Status information

The extension distinguishes between four states internally:

* **Not installed** — the game is not installed locally.
* **Unknown** — the game is installed, but its update status could not be determined yet.
* **Up to date** — the local build matches the current public Steam build.
* **Update available** — a newer public Steam build is available.

Only the last three states can result in a visible indicator, with games that are not installed intentionally hidden.

## Cache

Update information is stored in Playnite's extension data directory.

The cache is used to prevent checking Steam information every time Playnite refreshes the game view.

The default cache duration is:

```text
30 minutes
```

## Limitations

* Only **Steam** games are currently supported.
* Update information depends on the public Steam build information being available.
* The extension does not start Steam downloads or install updates.
* It only reports whether a newer build appears to be available.
* The indicator is currently available in the **Playnite Desktop theme** only.
* The extension targets **Playnite 10**.
