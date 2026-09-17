# Playnite - Game Update Status

A [Playnite](https://playnite.link/) extension that displays the update status of installed games directly in Playnite's Desktop theme.

![screenshot-grid](/docs/images/screenshot-grid.png)

## Features

Game Update Status displays a small status indicator next to games in Playnite:

| Indicator | Status           | Details              |
| --------- | ---------------- | -------------------- |
| 🟢        | Up to date       | The local build matches the current public build |
| 🟠        | Update available | A newer public Steam build is available |
| ⚪        | Unknown          | The update status could not be determined yet (reason appears in tooltip) |
|           | Not installed     | The game is not installed locally |

The extension currently supports games on the following sources :
- Steam
- Epic


## Installation

The extension and theme are designed to work together.

1. [Install the extension](https://playnite.link/addons.html#GameUpdateStatus)
2. [Install the theme](https://playnite.link/addons.html#GameUpdateStatus_Theme)


![Téléchargements GitHub](https://img.shields.io/github/downloads/FunkyKwak/playnite-get-update-status/total)

## How it works

The extension checks the local Steam installation data against the current public build information available from Steam.

For each installed Steam game, it compares:

* the locally installed build ID;
* the current public build ID.

The result is cached locally in Playnite's extension data directory.
The cache is used to prevent checking public information every time Playnite refreshes the game view.

By default, the cache is refreshed every **30 minutes**.


## Limitations

* Only **Steam** and **Epic** games are currently supported.
* The extension does not start Steam downloads or install updates.
* It only reports whether a newer build appears to be available.
* The indicator is currently available in the **Playnite Desktop theme** only.
* The extension targets **Playnite 10**.
