# ASUS Fan Profile Switcher

A Windows cooling dashboard for the XML profiles saved by ASUS Fan Xpert 2+/3.
The interface uses an Armoury Crate-inspired dark instrument-panel layout while
remaining an independent utility.

The default profile directory is:

```text
C:\ProgramData\ASUS\DIP\FanXpert\Profiles
```

## How it works

Fan Xpert 2+/3, as shipped with AI Suite 3, stores its live configuration in
`FanStore.xml` beside `AsusFanControlService.exe`. When a profile is selected,
this app:

1. verifies that the profile is valid XML;
2. detects the installed ASUS fan-control Windows service and its live store;
3. backs up the existing `FanStore.xml` under
   `C:\ProgramData\AsusFanProfileSwitcher\Backups`;
4. stops the service, copies the selected profile over the live store, and
   restarts the service;
5. restores the backup if any part of the operation fails.

The app deliberately refuses to write anything unless both the compatible
service and `FanStore.xml` are found.

## Interface and monitoring

- Square profile cards use a relevant fan, quiet, turbo, full-speed, or custom
  curve icon. A red edge and **ACTIVE** label identify the current profile.
- Select **MONITOR** on the left navigation rail to open the performance side
  panel.
- Live motherboard fan RPM and control percentage are read through
  [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
  Some ASUS boards expose RPM but not the associated control percentage; the
  app displays `N/A` in that case instead of estimating a value.
- Select a live telemetry card to show only that fan's curve from the selected
  profile XML. Matching uses the fan name first, the fan number second, and
  profile order as a fallback for older ASUS XML without friendly names.
- The curve chart parses temperature (`x`) and fan-duty (`y`) points. Profiles
  from an unrecognized Fan Xpert XML generation remain selectable, but the
  chart explains when it cannot find compatible curve points.

## Profile and fan names

- Select the pencil in the corner of a profile card to give that profile a
  display name. This changes only the label in this app and does not rewrite the
  ASUS XML.
- Select **+ New Profile** to duplicate the currently selected profile under a
  new XML file and assign a display name. Edit the actual curve in Fan Xpert,
  then press **Refresh** in this app.
- Select **RENAME** on a fan readout to assign a friendly name such as
  `Front intake` or `Radiator pump`. Selecting the rest of the card changes the
  curve shown below it.

Display names and fan aliases are stored separately in:

```text
C:\ProgramData\AsusFanProfileSwitcher\settings.json
```

## Compatibility

- Designed for desktop motherboards using **Fan Xpert 2+, Fan Xpert 3, or the
  legacy Fan Xpert component in AI Suite 3**.
- Newer Armoury Crate installations use Fan Xpert 4. ASUS documents its user
  interface but does not publish a supported third-party profile-switching API.
  Some installations retain the legacy service and work; installations without
  `AsusFanControlService` plus `FanStore.xml` are shown as unsupported and are
  not modified.
- This is not intended for ASUS laptop performance/thermal modes, which use
  different firmware interfaces.

Close the Fan Xpert page before switching. Do not uninstall, update, or tune
Fan Xpert while a switch is in progress.

## Build

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on
Windows, then run:

```powershell
.\build.ps1
```

The self-contained executable is written to:

```text
dist\AsusFanProfileSwitcher.exe
```

The build script runs the profile discovery, duplication, and curve-parser smoke
tests before publishing.

The executable requests administrator access because it must update a file
under Program Files and restart a Windows service.

## Use

1. Create and save the desired profiles in Fan Xpert.
2. Start `AsusFanProfileSwitcher.exe` and approve the Windows UAC prompt.
3. Confirm that the top status says it is connected to the ASUS fan service.
4. Select a profile button and confirm the change.
5. Open **Monitor** to inspect live RPM/duty values and the selected XML curve.

If ASUS saved profiles elsewhere, use **Choose folder**. The active ASUS store
is always auto-detected and cannot be selected manually.

## Important safety note

Fan curves protect hardware from overheating. Only select profiles you created
and tested in ASUS Fan Xpert, and verify fan speed and temperature after the
first switch. Backups are retained, but this software is an independent utility
and is not affiliated with or supported by ASUS.

## Third-party software

Live telemetry uses `LibreHardwareMonitorLib` 0.9.6, licensed under the Mozilla
Public License 2.0. Its source and license are available from the
[LibreHardwareMonitor project](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
