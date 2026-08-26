# Automatic updates

Harpoon's Windows build can discover, verify, install, and relaunch into a newer published GitHub Release.

## Release contract

Every installable release must have all of the following:

1. A semantic version tag in the form `vMAJOR.MINOR.PATCH`.
2. A published, non-prerelease GitHub Release for that tag.
3. A release asset named exactly `Harpoon-First-Island-Chain-Windows.zip`.
4. The player files at the root of the ZIP, including `HarpoonFirstIslandChain.exe`.
5. GitHub's `sha256:` digest for the uploaded asset.

The game checks GitHub's public `releases/latest` endpoint at startup and from **Match & System → Check for Update**. It compares the release tag with `Application.version`. A newer release appears in the collapsed section header.

Installation requires a deliberate **Install Update** click. The game downloads into its persistent-data directory, checks GitHub's SHA-256 digest, saves the current match, and starts a temporary PowerShell updater. The helper waits for the game to exit, backs up replaced files under `UpdateBackups`, installs only the verified archive's root entries, and relaunches the game. A failed verification is deleted and never executed.

## One-time GitHub setup

The tag workflow uses GameCI because GitHub-hosted runners do not include this project's Unity editor/module. Add an Actions repository secret named `UNITY_LICENSE` containing a Unity license accepted by GameCI. The workflow already has only the `contents: write` permission needed to publish the release.

For supply-chain protection, enable **Settings → General → Releases → Enable release immutability** after validating the first pipeline release. The workflow creates a draft, attaches both the ZIP and checksum, and only then publishes it so all assets are present before immutability applies.

## Publishing

1. Finish and validate the release commit.
2. Update `ProjectSetup.DefaultReleaseVersion` for local builds.
3. Create an annotated matching tag, for example `git tag -a v0.1.1 -m "Harpoon v0.1.1"`.
4. Push the commit and tag: `git push origin main v0.1.1`.
5. Watch **Actions → Publish Windows release**. Do not advertise the tag until the workflow publishes the Release successfully.

The workflow passes the tag to Unity as `-releaseVersion`; `ProjectSetup` strips the leading `v`, validates it, and stamps the Windows binary. This prevents a package tagged `v0.1.1` from reporting an unrelated application version.

## Recovery

Update logs are written to `%TEMP%\HarpoonUpdater.log`. Replaced files are copied to Unity's persistent-data directory under `UpdateBackups\<previous-version>`. Saved games remain outside the installation directory and are not removed during an update.

The automatic installer currently supports Windows players. Editor sessions and other platforms may check versions but cannot invoke the Windows installer.

Run `updater-test.cmd` after a Windows build to exercise archive validation, replacement, backup, and the non-relaunch test path entirely inside a disposable temporary installation.
